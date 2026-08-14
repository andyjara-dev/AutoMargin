namespace Remates.Domain.Bidding;

public sealed record SafetyMarginComponent
{
    public required string Key { get; init; }
    public required string Label { get; init; }

    /// <summary>Valor crudo del factor de incertidumbre (0..1).</summary>
    public required decimal RawValue { get; init; }

    /// <summary>Puntos porcentuales que este factor agrega al margen de seguridad.</summary>
    public required decimal Contribution { get; init; }
}

public sealed record MaxBidResult
{
    /// <summary>Precio de adjudicación donde la utilidad es cero.</summary>
    public required decimal BreakevenBid { get; init; }

    /// <summary>Utilidad mínima exigida a esta operación (U), en CLP.</summary>
    public required decimal RequiredProfit { get; init; }

    /// <summary>Cuál de los dos criterios definió la utilidad mínima: monto absoluto o rentabilidad anual.</summary>
    public required string RequiredProfitDriver { get; init; }

    /// <summary>Puja máxima antes de aplicar el margen de seguridad.</summary>
    public required decimal TheoreticalMaxBid { get; init; }

    /// <summary>Margen de seguridad efectivo, calculado a partir de la incertidumbre real de este vehículo.</summary>
    public required decimal SafetyMarginPct { get; init; }

    public required IReadOnlyList<SafetyMarginComponent> SafetyMarginBreakdown { get; init; }

    /// <summary>El número que importa: no pujar por sobre esto.</summary>
    public required decimal MaxBid { get; init; }

    /// <summary>False cuando ni siquiera a precio cero la operación alcanza la utilidad exigida.</summary>
    public required bool IsViable { get; init; }
}
