namespace Remates.Infrastructure.Entities;

/// <summary>
/// Conjunto de parámetros versionado. Cada análisis persistido apunta al conjunto que usó,
/// de modo que cambiar un parámetro hoy no altera lo que se decidió el mes pasado.
/// </summary>
public class ParameterSet : AuditableEntity
{
    public required string Name { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public string? Note { get; set; }

    public ICollection<ParameterValue> Values { get; set; } = [];
}

public class ParameterValue : AuditableEntity
{
    public long ParameterSetId { get; set; }
    public ParameterSet? ParameterSet { get; set; }

    public required string Key { get; set; }
    public decimal? NumericValue { get; set; }
    public string? TextValue { get; set; }
    public bool? BoolValue { get; set; }
}
