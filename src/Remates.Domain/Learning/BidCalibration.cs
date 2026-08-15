using Remates.Domain.Common;

namespace Remates.Domain.Learning;

/// <summary>Resultado de un remate al que se fue con una puja máxima calculada.</summary>
public sealed record BidOutcome
{
    /// <summary>La puja máxima que el sistema autorizó en su momento.</summary>
    public required decimal MaxBidAuthorized { get; init; }

    /// <summary>Precio al que se adjudicó el lote. Nulo si no se llegó a saber.</summary>
    public decimal? WinningPrice { get; init; }

    public required bool Won { get; init; }
}

/// <summary>
/// Qué tan afinada está la puja máxima, mirada sobre muchos remates.
///
/// Es la respuesta a la pregunta que ninguna operación aislada contesta: si ganamos todo vamos
/// demasiado arriba y estamos pagando de más; si no ganamos nunca vamos demasiado abajo y
/// estamos dejando pasar negocios que sí daban.
/// </summary>
public sealed record CalibrationReport
{
    public required int Total { get; init; }
    public required int Won { get; init; }
    public required int Lost { get; init; }

    /// <summary>Proporción de remates ganados sobre los que se pujó.</summary>
    public required decimal WinRate { get; init; }

    /// <summary>
    /// Cuánto faltó en promedio para ganar los perdidos, en pesos. Solo cuenta los perdidos de
    /// los que se supo el precio de adjudicación.
    /// </summary>
    public decimal? AverageGapWhenLost { get; init; }

    /// <summary>
    /// El mismo margen faltante, como proporción de la puja máxima. Es lo comparable entre autos
    /// de precios distintos: faltar cien mil en uno de cuatro millones no es lo mismo que en uno
    /// de veinte.
    /// </summary>
    public decimal? AverageGapPctWhenLost { get; init; }

    /// <summary>Cuántos perdidos se adjudicaron a un precio que el sistema sí autorizaba.</summary>
    public required int LostBelowOwnLimit { get; init; }

    /// <summary>Perdidos sin precio de adjudicación anotado: no aportan a la calibración.</summary>
    public required int LostWithoutPrice { get; init; }

    public required CalibrationVerdict Verdict { get; init; }
    public required string Explanation { get; init; }

    /// <summary>Cuántos remates hacen falta antes de que esto signifique algo.</summary>
    public bool IsConclusive => Total >= CalibrationCalculator.MinimumSample;
}

public enum CalibrationVerdict
{
    /// <summary>Muy pocos datos para decir nada.</summary>
    Insufficient,

    /// <summary>La proporción de victorias está en un rango sano.</summary>
    Balanced,

    /// <summary>Se gana casi todo: probablemente se está pagando de más.</summary>
    TooAggressive,

    /// <summary>No se gana casi nada: probablemente se está dejando pasar negocios buenos.</summary>
    TooConservative
}

/// <summary>
/// Mide la calibración a partir de los remates cerrados. Determinístico y sin dependencias, como
/// el resto de los motores.
/// </summary>
public static class CalibrationCalculator
{
    /// <summary>
    /// Por debajo de esto no se dice nada. Con tres remates cualquier proporción parece una
    /// tendencia, y actuar sobre ruido es peor que no actuar.
    /// </summary>
    public const int MinimumSample = 8;

    /// <summary>
    /// Ganar más de esto sugiere que la puja máxima va alta: si casi nadie te supera, es que
    /// estás ofreciendo por encima de lo que el mercado pide.
    /// </summary>
    public const decimal TooAggressiveWinRate = 0.65m;

    /// <summary>
    /// Por debajo de esto se está yendo demasiado corto. Perder es normal y sano — en un remate
    /// compite mucha gente — pero perder casi siempre significa no estar en el juego.
    /// </summary>
    public const decimal TooConservativeWinRate = 0.15m;

    public static CalibrationReport Analyze(IReadOnlyList<BidOutcome> outcomes)
    {
        var total = outcomes.Count;

        if (total == 0)
        {
            return new CalibrationReport
            {
                Total = 0, Won = 0, Lost = 0, WinRate = 0m,
                LostBelowOwnLimit = 0, LostWithoutPrice = 0,
                Verdict = CalibrationVerdict.Insufficient,
                Explanation = "Todavía no hay remates cerrados. Marca el resultado de cada lote " +
                              "en la sala, incluidos los que pierdas: son los que más enseñan."
            };
        }

        var won = outcomes.Count(o => o.Won);
        var lost = total - won;

        var lostWithPrice = outcomes
            .Where(o => !o.Won && o.WinningPrice is > 0)
            .ToList();

        decimal? averageGap = null;
        decimal? averageGapPct = null;

        if (lostWithPrice.Count > 0)
        {
            averageGap = MoneyMath.RoundToPeso(
                lostWithPrice.Average(o => o.WinningPrice!.Value - o.MaxBidAuthorized));

            // Se promedian las proporciones, no se divide un promedio por otro: así un auto caro
            // no pesa más que uno barato al medir qué tan cerca se estuvo.
            averageGapPct = MoneyMath.RoundRate(lostWithPrice
                .Where(o => o.MaxBidAuthorized > 0)
                .Select(o => (o.WinningPrice!.Value - o.MaxBidAuthorized) / o.MaxBidAuthorized)
                .DefaultIfEmpty(0m)
                .Average());
        }

        // Perdidos que se adjudicaron por debajo de nuestro propio techo: ahí no fue la puja
        // máxima la que falló, fue que no se ofreció lo que ya estaba autorizado.
        var lostBelowOwnLimit = lostWithPrice.Count(o => o.WinningPrice!.Value <= o.MaxBidAuthorized);

        var winRate = MoneyMath.RoundRate((decimal)won / total);
        var verdict = Judge(total, winRate);

        return new CalibrationReport
        {
            Total = total,
            Won = won,
            Lost = lost,
            WinRate = winRate,
            AverageGapWhenLost = averageGap,
            AverageGapPctWhenLost = averageGapPct,
            LostBelowOwnLimit = lostBelowOwnLimit,
            LostWithoutPrice = lost - lostWithPrice.Count,
            Verdict = verdict,
            Explanation = Explain(verdict, total, lostBelowOwnLimit)
        };
    }

    private static CalibrationVerdict Judge(int total, decimal winRate)
    {
        if (total < MinimumSample) return CalibrationVerdict.Insufficient;

        if (winRate > TooAggressiveWinRate) return CalibrationVerdict.TooAggressive;
        if (winRate < TooConservativeWinRate) return CalibrationVerdict.TooConservative;

        return CalibrationVerdict.Balanced;
    }

    private static string Explain(CalibrationVerdict verdict, int total, int lostBelowOwnLimit)
    {
        var aviso = lostBelowOwnLimit > 0
            ? $" Además, {lostBelowOwnLimit} de los perdidos se adjudicaron por debajo de tu " +
              "propia puja máxima: ahí no falló el cálculo, faltó ofrecer lo que ya tenías autorizado."
            : string.Empty;

        return verdict switch
        {
            CalibrationVerdict.Insufficient =>
                $"Van {total} de los {MinimumSample} remates que hacen falta para que esto " +
                "signifique algo. Antes de eso, cualquier proporción es ruido." + aviso,

            CalibrationVerdict.TooAggressive =>
                "Estás ganando casi todos los remates a los que vas. Suena bien, pero suele " +
                "significar que ofreces por encima de lo que el mercado pide: sube la utilidad " +
                "mínima o el ROI exigido y verás bajar la puja máxima." + aviso,

            CalibrationVerdict.TooConservative =>
                "Casi no estás ganando. La puja máxima va corta y se te están pasando negocios " +
                "que sí daban. Revisa si la utilidad mínima o el margen de seguridad están más " +
                "altos de lo necesario." + aviso,

            _ => "La proporción de remates ganados está en un rango sano: ganas algunos y pierdes " +
                 "otros, que es como se ve una puja máxima bien puesta." + aviso
        };
    }
}
