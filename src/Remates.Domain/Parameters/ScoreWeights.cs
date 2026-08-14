namespace Remates.Domain.Parameters;

/// <summary>
/// Pesos de los componentes del score. Se normalizan al usarse, de modo que no es obligatorio que sumen 100.
/// Son configurables y versionados: un score no es comparable entre versiones distintas de pesos.
/// </summary>
public sealed record ScoreWeights
{
    public decimal Profitability { get; init; } = 30m;
    public decimal BidHeadroom { get; init; } = 15m;
    public decimal Liquidity { get; init; } = 15m;
    public decimal MechanicalRisk { get; init; } = 12m;
    public decimal DocumentRisk { get; init; } = 10m;
    public decimal EstimateCertainty { get; init; } = 10m;
    public decimal EvidenceQuality { get; init; } = 8m;

    public decimal Total => Profitability + BidHeadroom + Liquidity + MechanicalRisk
                          + DocumentRisk + EstimateCertainty + EvidenceQuality;

    public static ScoreWeights Default => new();
}
