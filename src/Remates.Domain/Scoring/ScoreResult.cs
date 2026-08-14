namespace Remates.Domain.Scoring;

public enum TrafficLight
{
    Red = 1,
    Yellow = 2,
    Green = 3
}

/// <summary>
/// Condiciones que fuerzan ROJO sin importar el puntaje. Un score alto nunca puede rescatar
/// una operación que viola alguna de estas.
/// </summary>
public enum GateCode
{
    PriceAboveMaxBid = 1,
    RoiBelowMinimum = 2,
    InsufficientMarketData = 3,
    CriticalDocumentRisk = 4,
    PessimisticLossExceedsLimit = 5,
    CapitalConcentration = 6,
    NotViable = 7
}

public sealed record TriggeredGate
{
    public required GateCode Code { get; init; }
    public required string Message { get; init; }
}

public sealed record ScoreComponent
{
    public required string Key { get; init; }
    public required string Label { get; init; }

    /// <summary>Peso configurado del componente.</summary>
    public required decimal Weight { get; init; }

    /// <summary>Puntaje normalizado del componente, 0..100.</summary>
    public required decimal Normalized { get; init; }

    /// <summary>Puntos que este componente aporta al score final.</summary>
    public required decimal Points { get; init; }

    /// <summary>Puntos que se perdieron en este componente respecto de su máximo.</summary>
    public required decimal PointsLost { get; init; }

    /// <summary>Explicación en lenguaje natural, generada por código, no por un modelo.</summary>
    public required string Explanation { get; init; }
}

public sealed record ScoreResult
{
    /// <summary>Score 0..100.</summary>
    public required decimal Score { get; init; }

    public required TrafficLight TrafficLight { get; init; }

    /// <summary>Etiqueta corta de la recomendación.</summary>
    public required string Recommendation { get; init; }

    public required IReadOnlyList<ScoreComponent> Components { get; init; }
    public required IReadOnlyList<TriggeredGate> Gates { get; init; }

    /// <summary>Componentes que más aportan al score.</summary>
    public required IReadOnlyList<string> Strengths { get; init; }

    /// <summary>Componentes que más puntos restan.</summary>
    public required IReadOnlyList<string> Weaknesses { get; init; }
}
