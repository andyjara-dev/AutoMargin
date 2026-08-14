using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Remates.Infrastructure.Entities;

namespace Remates.Infrastructure.Persistence.Configurations;

public class AuctionHouseConfiguration : IEntityTypeConfiguration<AuctionHouse>
{
    public void Configure(EntityTypeBuilder<AuctionHouse> b)
    {
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.TermsUrl).HasMaxLength(600);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public class AuctionConfiguration : IEntityTypeConfiguration<Auction>
{
    public void Configure(EntityTypeBuilder<Auction> b)
    {
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.Region).HasMaxLength(80);
        b.Property(x => x.TermsUrl).HasMaxLength(600);

        b.HasOne(x => x.AuctionHouse).WithMany(h => h.Auctions)
            .HasForeignKey(x => x.AuctionHouseId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.AuctionDate);
    }
}

public class AuctionLotConfiguration : IEntityTypeConfiguration<AuctionLot>
{
    public void Configure(EntityTypeBuilder<AuctionLot> b)
    {
        b.Property(x => x.LotNumber).HasMaxLength(40);

        b.HasOne(x => x.Auction).WithMany(a => a.Lots)
            .HasForeignKey(x => x.AuctionId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Vehicle).WithMany(v => v.Lots)
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.AuctionId, x.LotNumber });

        b.HasQueryFilter(x => x.Vehicle!.DeletedAt == null);
    }
}

public class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> b)
    {
        b.HasOne(x => x.AuctionLot).WithMany(l => l.Bids)
            .HasForeignKey(x => x.AuctionLotId).OnDelete(DeleteBehavior.Cascade);

        b.Property(x => x.Note).HasMaxLength(500);

        // Consultado al calibrar: cuántas pujas ganamos y a qué precio se fueron las perdidas.
        b.HasIndex(x => x.Result);

        // El filtro se hereda a través del lote hasta el vehículo.
        b.HasQueryFilter(x => x.AuctionLot!.Vehicle!.DeletedAt == null);
    }
}

public class MarketComparableConfiguration : IEntityTypeConfiguration<MarketComparableEntity>
{
    public void Configure(EntityTypeBuilder<MarketComparableEntity> b)
    {
        b.ToTable("market_comparable");

        b.Property(x => x.Source).HasMaxLength(80);
        b.Property(x => x.Url).HasMaxLength(600);
        b.Property(x => x.Region).HasMaxLength(80);
        b.Property(x => x.Condition).HasMaxLength(80);
        b.Property(x => x.OutlierReason).HasMaxLength(240);

        b.HasOne(x => x.Vehicle).WithMany(v => v.Comparables)
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.VehicleId);

        // Repite el filtro de borrado lógico del vehículo, para que los comparables de uno
        // dado de baja no reaparezcan al consultar esta tabla.
        b.HasQueryFilter(x => x.Vehicle!.DeletedAt == null);
    }
}

public class DamageItemConfiguration : IEntityTypeConfiguration<DamageItemEntity>
{
    public void Configure(EntityTypeBuilder<DamageItemEntity> b)
    {
        b.ToTable("damage_item");

        b.Property(x => x.Description).HasMaxLength(400);

        b.HasOne(x => x.Vehicle).WithMany(v => v.Damages)
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.VehicleId);

        b.HasQueryFilter(x => x.Vehicle!.DeletedAt == null);
    }
}

public class ParameterSetConfiguration : IEntityTypeConfiguration<ParameterSet>
{
    public void Configure(EntityTypeBuilder<ParameterSet> b)
    {
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);

        // Solo puede haber un conjunto activo a la vez; lo garantiza la base, no el código.
        b.HasIndex(x => x.IsActive).IsUnique().HasFilter("is_active");
    }
}

public class ParameterValueConfiguration : IEntityTypeConfiguration<ParameterValue>
{
    public void Configure(EntityTypeBuilder<ParameterValue> b)
    {
        b.Property(x => x.Key).HasMaxLength(80).IsRequired();
        b.Property(x => x.TextValue).HasMaxLength(240);

        b.HasOne(x => x.ParameterSet).WithMany(s => s.Values)
            .HasForeignKey(x => x.ParameterSetId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.ParameterSetId, x.Key }).IsUnique();
    }
}

public class DealAnalysisSnapshotConfiguration : IEntityTypeConfiguration<DealAnalysisSnapshot>
{
    public void Configure(EntityTypeBuilder<DealAnalysisSnapshot> b)
    {
        b.ToTable("deal_analysis");

        b.Property(x => x.FinancialEngineVersion).HasMaxLength(20).IsRequired();
        b.Property(x => x.ScoringEngineVersion).HasMaxLength(20).IsRequired();

        b.Property(x => x.GatesJson).HasColumnType("jsonb");
        b.Property(x => x.ScoreBreakdownJson).HasColumnType("jsonb");
        b.Property(x => x.CostBreakdownJson).HasColumnType("jsonb");
        b.Property(x => x.ScenariosJson).HasColumnType("jsonb");
        b.Property(x => x.InputsJson).HasColumnType("jsonb");

        b.HasOne(x => x.Vehicle).WithMany(v => v.Analyses)
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.AuctionLot).WithMany()
            .HasForeignKey(x => x.AuctionLotId).OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.ParameterSet).WithMany()
            .HasForeignKey(x => x.ParameterSetId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.VehicleId, x.ComputedAt }).IsDescending(false, true);

        b.HasQueryFilter(x => x.Vehicle!.DeletedAt == null);
    }
}
