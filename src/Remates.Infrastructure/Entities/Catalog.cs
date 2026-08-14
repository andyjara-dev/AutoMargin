using Remates.Domain.Damage;

namespace Remates.Infrastructure.Entities;

public class Make : AuditableEntity
{
    public required string Name { get; set; }
    public ICollection<VehicleModel> Models { get; set; } = [];
}

/// <summary>Modelo del catálogo. Se llama VehicleModel para no chocar con el término «modelo» de EF.</summary>
public class VehicleModel : AuditableEntity
{
    public long MakeId { get; set; }
    public Make? Make { get; set; }

    public required string Name { get; set; }
    public string? BodyType { get; set; }

    public ICollection<Trim> Trims { get; set; } = [];
}

public class Trim : AuditableEntity
{
    public long ModelId { get; set; }
    public VehicleModel? Model { get; set; }

    public required string Name { get; set; }
}

/// <summary>
/// Tabla de costos base de reparación por categoría y gravedad. Es la semilla que reemplaza
/// al conocimiento del taller mientras no haya historial propio.
/// </summary>
public class RepairCostBaseline : AuditableEntity
{
    public DamageCategory Category { get; set; }
    public DamageSeverity Severity { get; set; }

    public decimal CostMin { get; set; }
    public decimal CostExpected { get; set; }
    public decimal CostMax { get; set; }

    public DateOnly ValidFrom { get; set; }
    public string? Notes { get; set; }
}
