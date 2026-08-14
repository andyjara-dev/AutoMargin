using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Remates.Infrastructure.Entities;

namespace Remates.Infrastructure.Persistence.Configurations;

public class MakeConfiguration : IEntityTypeConfiguration<Make>
{
    public void Configure(EntityTypeBuilder<Make> b)
    {
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public class VehicleModelConfiguration : IEntityTypeConfiguration<VehicleModel>
{
    public void Configure(EntityTypeBuilder<VehicleModel> b)
    {
        b.ToTable("model");
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.BodyType).HasMaxLength(40);

        b.HasOne(x => x.Make).WithMany(m => m.Models)
            .HasForeignKey(x => x.MakeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.MakeId, x.Name }).IsUnique();
    }
}

public class TrimConfiguration : IEntityTypeConfiguration<Trim>
{
    public void Configure(EntityTypeBuilder<Trim> b)
    {
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();

        b.HasOne(x => x.Model).WithMany(m => m.Trims)
            .HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.ModelId, x.Name }).IsUnique();
    }
}

public class RepairCostBaselineConfiguration : IEntityTypeConfiguration<RepairCostBaseline>
{
    public void Configure(EntityTypeBuilder<RepairCostBaseline> b)
    {
        b.HasIndex(x => new { x.Category, x.Severity, x.ValidFrom });
    }
}

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.Property(x => x.DisplayName).HasMaxLength(160);
        b.Property(x => x.Plate).HasMaxLength(12);
        b.Property(x => x.Vin).HasMaxLength(32);
        b.Property(x => x.Color).HasMaxLength(40);
        b.Property(x => x.Region).HasMaxLength(80);
        b.Property(x => x.Comuna).HasMaxLength(80);
        b.Property(x => x.BodyType).HasMaxLength(40);
        b.Property(x => x.SourceType).HasMaxLength(40);
        b.Property(x => x.ExternalRef).HasMaxLength(120);
        b.Property(x => x.Url).HasMaxLength(600);
        b.Property(x => x.EquipmentJson).HasColumnType("jsonb");

        b.HasOne(x => x.Make).WithMany().HasForeignKey(x => x.MakeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Model).WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Trim).WithMany().HasForeignKey(x => x.TrimId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.MakeId, x.ModelId, x.Year });
        b.HasIndex(x => x.Plate);

        // Los vehículos eliminados no desaparecen: se ocultan, para no perder el historial de análisis.
        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}

public class VehicleStatusHistoryConfiguration : IEntityTypeConfiguration<VehicleStatusHistory>
{
    public void Configure(EntityTypeBuilder<VehicleStatusHistory> b)
    {
        b.HasOne(x => x.Vehicle).WithMany(v => v.StatusHistory)
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.VehicleId, x.ChangedAt });

        // Debe repetir el filtro del padre: sin esto, el historial de un vehículo dado de baja
        // seguiría apareciendo al consultar esta tabla directamente.
        b.HasQueryFilter(x => x.Vehicle!.DeletedAt == null);
    }
}
