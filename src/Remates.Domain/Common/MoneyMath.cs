namespace Remates.Domain.Common;

/// <summary>
/// Utilidades numéricas para los motores de cálculo.
/// Todo el dinero se maneja en <see cref="decimal"/> (CLP). Nunca double, nunca float.
/// </summary>
public static class MoneyMath
{
    /// <summary>Rentabilidad anualizada máxima que se reporta, para evitar overflow con plazos muy cortos.</summary>
    public const decimal MaxAnnualizedRoi = 100m; // 10.000%

    public static decimal Clamp(decimal value, decimal min, decimal max)
        => value < min ? min : value > max ? max : value;

    /// <summary>Clamp a [0,1], el rango usado por todos los factores normalizados.</summary>
    public static decimal Clamp01(decimal value) => Clamp(value, 0m, 1m);

    /// <summary>División que devuelve <paramref name="fallback"/> cuando el denominador es cero.</summary>
    public static decimal SafeDivide(decimal numerator, decimal denominator, decimal fallback = 0m)
        => denominator == 0m ? fallback : numerator / denominator;

    /// <summary>Redondeo al peso chileno (sin decimales).</summary>
    public static decimal RoundToPeso(decimal value)
        => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    /// <summary>Trunca hacia abajo al peso. Se usa en la puja máxima: nunca redondear hacia arriba un límite de compra.</summary>
    public static decimal FloorToPeso(decimal value) => Math.Floor(value);

    /// <summary>Porcentaje con 4 decimales (0,1234 = 12,34%).</summary>
    public static decimal RoundRate(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Percentil por interpolación lineal sobre una secuencia ya ordenada ascendentemente.
    /// <paramref name="percentile"/> va de 0 a 1.
    /// </summary>
    public static decimal Percentile(IReadOnlyList<decimal> sortedValues, decimal percentile)
    {
        if (sortedValues.Count == 0)
            throw new ArgumentException("Se requiere al menos un valor para calcular un percentil.", nameof(sortedValues));

        if (sortedValues.Count == 1) return sortedValues[0];

        var p = Clamp01(percentile);
        var position = p * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex) return sortedValues[lowerIndex];

        var fraction = position - lowerIndex;
        return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * fraction;
    }

    /// <summary>
    /// Anualiza una rentabilidad simple obtenida en <paramref name="days"/> días: (1+roi)^(365/d) - 1.
    /// Devuelve -1 (pérdida total) si el capital no se recupera, y se acota para evitar cifras absurdas.
    /// </summary>
    public static decimal Annualize(decimal simpleRoi, decimal days)
    {
        if (days <= 0m) return 0m;

        var growth = 1m + simpleRoi;
        if (growth <= 0m) return -1m;

        var exponent = 365d / (double)days;
        var result = Math.Pow((double)growth, exponent) - 1d;

        if (double.IsNaN(result) || double.IsInfinity(result))
            return result > 0 ? MaxAnnualizedRoi : -1m;

        return Clamp((decimal)result, -1m, MaxAnnualizedRoi);
    }
}
