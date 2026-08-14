namespace Remates.Domain.Damage;

public enum DamageCategory
{
    Bodywork = 1,
    Paint = 2,
    Mechanical = 3,
    Electrical = 4,
    Tires = 5,
    Interior = 6,
    Glass = 7,
    Lights = 8,
    Suspension = 9,
    Structural = 10,
    Airbags = 11,
    Other = 99
}

public enum DamageSeverity
{
    Minor = 1,
    Moderate = 2,
    Severe = 3,
    Critical = 4
}

/// <summary>De dónde salió la estimación. Lo detectado por IA requiere confirmación humana antes de entrar al cálculo.</summary>
public enum DamageSource
{
    Manual = 1,
    Ai = 2,
    Workshop = 3
}

/// <summary>
/// Cuánto sabemos realmente del estado mecánico. En un remate lo habitual es no poder encender el vehículo,
/// y eso por sí solo es riesgo aunque no haya daños visibles registrados.
/// </summary>
public enum MechanicalInspectionLevel
{
    /// <summary>No se pudo encender ni revisar.</summary>
    None = 0,

    /// <summary>Solo inspección visual, sin encender.</summary>
    VisualOnly = 1,

    /// <summary>Encendió y/o se pudo escuchar el motor.</summary>
    EngineRun = 2,

    /// <summary>Hubo prueba de manejo.</summary>
    TestDrive = 3,

    /// <summary>Informe de taller o escáner.</summary>
    WorkshopReport = 4
}

/// <summary>
/// Riesgo documental: encargo por robo, prendas o gravámenes no alzados, limitación al dominio,
/// multas TAG impagas, permiso de circulación y revisión técnica.
/// </summary>
public enum DocumentRiskLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

public static class RiskLevels
{
    /// <summary>Factor 0..1 usado en el margen de seguridad y en el score.</summary>
    public static decimal ToFactor(this DocumentRiskLevel level) => level switch
    {
        DocumentRiskLevel.None => 0m,
        DocumentRiskLevel.Low => 0.25m,
        DocumentRiskLevel.Medium => 0.50m,
        DocumentRiskLevel.High => 1.00m,
        _ => 0m
    };

    /// <summary>Piso de riesgo mecánico (0..100) impuesto por lo poco que pudimos inspeccionar.</summary>
    public static decimal BaselineMechanicalRisk(this MechanicalInspectionLevel level) => level switch
    {
        MechanicalInspectionLevel.None => 60m,
        MechanicalInspectionLevel.VisualOnly => 40m,
        MechanicalInspectionLevel.EngineRun => 25m,
        MechanicalInspectionLevel.TestDrive => 15m,
        MechanicalInspectionLevel.WorkshopReport => 5m,
        _ => 40m
    };

    /// <summary>Aporte al riesgo mecánico (0..100) según la gravedad del daño.</summary>
    public static decimal ToRiskPoints(this DamageSeverity severity) => severity switch
    {
        DamageSeverity.Minor => 15m,
        DamageSeverity.Moderate => 40m,
        DamageSeverity.Severe => 70m,
        DamageSeverity.Critical => 100m,
        _ => 0m
    };
}
