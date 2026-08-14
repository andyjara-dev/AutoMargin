using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Remates.Infrastructure.Entities;

namespace Remates.Infrastructure.Persistence.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> b)
    {
        b.Property(x => x.InvoiceRef).HasMaxLength(80);
        b.Property(x => x.Note).HasMaxLength(500);

        b.HasOne(x => x.Vehicle).WithMany()
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.AuctionLot).WithMany()
            .HasForeignKey(x => x.AuctionLotId).OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.DealAnalysis).WithMany()
            .HasForeignKey(x => x.DealAnalysisId).OnDelete(DeleteBehavior.SetNull);

        // Un vehículo se compra una sola vez.
        b.HasIndex(x => x.VehicleId).IsUnique();

        b.HasQueryFilter(x => x.Vehicle!.DeletedAt == null);
    }
}

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.Supplier).HasMaxLength(160);
        b.Property(x => x.DocumentRef).HasMaxLength(80);

        b.HasOne(x => x.Vehicle).WithMany()
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.VehicleId, x.Category });

        b.HasQueryFilter(x => x.Vehicle!.DeletedAt == null);
    }
}

public class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> b)
    {
        b.Property(x => x.Channel).HasMaxLength(80).IsRequired();
        b.Property(x => x.Url).HasMaxLength(600);

        b.HasOne(x => x.Vehicle).WithMany()
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.VehicleId);

        b.HasQueryFilter(x => x.Vehicle!.DeletedAt == null);
    }
}

public class PriceChangeConfiguration : IEntityTypeConfiguration<PriceChange>
{
    public void Configure(EntityTypeBuilder<PriceChange> b)
    {
        b.Property(x => x.Reason).HasMaxLength(300);

        b.HasOne(x => x.Listing).WithMany(l => l.PriceChanges)
            .HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.ListingId, x.ChangedAt });

        b.HasQueryFilter(x => x.Listing!.Vehicle!.DeletedAt == null);
    }
}

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.Property(x => x.BuyerName).HasMaxLength(160);
        b.Property(x => x.PaymentMethod).HasMaxLength(80);
        b.Property(x => x.Note).HasMaxLength(500);

        b.HasOne(x => x.Vehicle).WithMany()
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);

        // Un vehículo se vende una sola vez.
        b.HasIndex(x => x.VehicleId).IsUnique();
        b.HasIndex(x => x.SaleDate);

        b.HasQueryFilter(x => x.Vehicle!.DeletedAt == null);
    }
}

public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> b)
    {
        b.Property(x => x.Note).HasMaxLength(300);

        b.HasOne(x => x.Vehicle).WithMany()
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.MovementDate);
        b.HasIndex(x => x.Type);

        // Sin filtro por vehículo a propósito: los aportes y retiros de capital no cuelgan de uno,
        // y el dinero que entró o salió no desaparece porque se dé de baja un vehículo.
    }
}

public class PredictionOutcomeConfiguration : IEntityTypeConfiguration<PredictionOutcome>
{
    public void Configure(EntityTypeBuilder<PredictionOutcome> b)
    {
        b.HasOne(x => x.Vehicle).WithMany()
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.DealAnalysis).WithMany()
            .HasForeignKey(x => x.DealAnalysisId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Sale).WithMany()
            .HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.VehicleId).IsUnique();
        b.HasIndex(x => x.ClosedAt);

        b.HasQueryFilter(x => x.Vehicle!.DeletedAt == null);
    }
}
