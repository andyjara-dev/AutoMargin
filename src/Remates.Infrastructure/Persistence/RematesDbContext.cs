using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Remates.Infrastructure.Entities;

namespace Remates.Infrastructure.Persistence;

public class RematesDbContext(DbContextOptions<RematesDbContext> options)
    : IdentityDbContext<AppUser, AppRole, long>(options)
{
    public DbSet<Make> Makes => Set<Make>();
    public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
    public DbSet<Trim> Trims => Set<Trim>();
    public DbSet<RepairCostBaseline> RepairCostBaselines => Set<RepairCostBaseline>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleStatusHistory> VehicleStatusHistory => Set<VehicleStatusHistory>();

    public DbSet<AuctionHouse> AuctionHouses => Set<AuctionHouse>();
    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<AuctionLot> AuctionLots => Set<AuctionLot>();
    public DbSet<Bid> Bids => Set<Bid>();

    public DbSet<MarketComparableEntity> MarketComparables => Set<MarketComparableEntity>();
    public DbSet<DamageItemEntity> DamageItems => Set<DamageItemEntity>();

    public DbSet<ParameterSet> ParameterSets => Set<ParameterSet>();
    public DbSet<ParameterValue> ParameterValues => Set<ParameterValue>();

    public DbSet<DealAnalysisSnapshot> DealAnalyses => Set<DealAnalysisSnapshot>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(RematesDbContext).Assembly);

        ApplyMoneyPrecision(builder);
        ApplySnakeCaseNames(builder);
    }

    /// <summary>
    /// Todo el dinero y las tasas usan numeric, nunca punto flotante. Un error de redondeo en
    /// una puja máxima es dinero real.
    /// </summary>
    private static void ApplyMoneyPrecision(ModelBuilder builder)
    {
        foreach (var property in builder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            // Las tasas y porcentajes necesitan más decimales que los montos.
            var isRate = property.Name.EndsWith("Pct", StringComparison.Ordinal)
                      || property.Name.EndsWith("Rate", StringComparison.Ordinal)
                      || property.Name.EndsWith("Factor", StringComparison.Ordinal)
                      || property.Name is "RoiSimple" or "RoiAnnualized" or "Confidence" or "Score";

            property.SetPrecision(isRate ? 12 : 14);
            property.SetScale(isRate ? 6 : 2);
        }
    }

    /// <summary>
    /// Convierte los nombres de EF a snake_case, que es la convención natural de PostgreSQL.
    /// Evita tener que citar identificadores en cada consulta escrita a mano.
    /// </summary>
    private static void ApplySnakeCaseNames(ModelBuilder builder)
    {
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName is not null) entity.SetTableName(ToSnakeCase(tableName));

            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));

            foreach (var key in entity.GetKeys())
                key.SetName(ToSnakeCase(key.GetName()!));

            foreach (var fk in entity.GetForeignKeys())
                fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName()!));

            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
        }
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var builder = new System.Text.StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (char.IsUpper(c))
            {
                var previousIsLower = i > 0 && char.IsLower(name[i - 1]);
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

                if (i > 0 && (previousIsLower || nextIsLower) && builder[^1] != '_')
                    builder.Append('_');

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
