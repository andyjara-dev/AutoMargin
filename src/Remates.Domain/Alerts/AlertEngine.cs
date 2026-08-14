using Remates.Domain.Common;
using Remates.Domain.Parameters;

namespace Remates.Domain.Alerts;

/// <summary>Estado de un vehículo en inventario, tal como lo necesita el motor de alertas.</summary>
public sealed record InventorySnapshot
{
    public required long VehicleId { get; init; }
    public required string Label { get; init; }

    /// <summary>Efectivo comprometido en este vehículo.</summary>
    public required decimal CashInvested { get; init; }

    public required int DaysInInventory { get; init; }

    /// <summary>Días desde que se publicó. Cero si aún no se publica.</summary>
    public int DaysListed { get; init; }

    public bool IsSold { get; init; }
    public bool HasAnalysis { get; init; }

    /// <summary>Valor de venta conservador según el último análisis.</summary>
    public decimal ExpectedSaleValue { get; init; }

    public decimal RepairBudgeted { get; init; }
    public decimal RepairActual { get; init; }
}

public sealed record AlertContext
{
    public required IReadOnlyList<InventorySnapshot> Inventory { get; init; }

    /// <summary>Capital total del negocio: aportes menos retiros.</summary>
    public decimal TotalCapital { get; init; }

    /// <summary>Efectivo libre, sin comprometer en vehículos.</summary>
    public decimal AvailableCapital { get; init; }
}

/// <summary>
/// Genera las alertas del dashboard aplicando reglas determinísticas sobre el inventario.
///
/// Cada alerta trae una acción sugerida a propósito: una lista de problemas sin qué hacer al
/// respecto se vuelve ruido que se aprende a ignorar, y entonces deja de servir.
/// </summary>
public static class AlertEngine
{
    public const string EngineVersion = "1.0.0";

    public static IReadOnlyList<Alert> Evaluate(AlertContext context, AnalysisParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);

        var alerts = new List<Alert>();

        foreach (var vehicle in context.Inventory.Where(v => !v.IsSold))
        {
            AddStaleInventory(alerts, vehicle, parameters);
            AddPriceAdjustment(alerts, vehicle, parameters);
            AddLowMargin(alerts, vehicle, parameters);
            AddCapitalConcentration(alerts, vehicle, context, parameters);
        }

        // La reparación sobre presupuesto importa también en los vendidos: es de donde sale
        // el aprendizaje sobre qué se subestima.
        foreach (var vehicle in context.Inventory)
            AddRepairOverBudget(alerts, vehicle, parameters);

        foreach (var vehicle in context.Inventory.Where(v => !v.HasAnalysis))
            AddMissingAnalysis(alerts, vehicle);

        AddLowCapital(alerts, context);

        // Primero lo grave, y dentro de cada nivel lo de mayor monto.
        return alerts
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.Magnitude)
            .ToList();
    }

    private static void AddStaleInventory(
        List<Alert> alerts, InventorySnapshot v, AnalysisParameters parameters)
    {
        if (v.DaysInInventory <= parameters.MaxDaysInInventory) return;

        var excess = v.DaysInInventory - parameters.MaxDaysInInventory;

        alerts.Add(new Alert
        {
            Type = AlertType.StaleInventory,
            // Al doble del umbral deja de ser un retraso y pasa a ser capital atrapado.
            Severity = v.DaysInInventory > parameters.MaxDaysInInventory * 2
                ? AlertSeverity.Critical
                : AlertSeverity.Warning,
            Message = $"Lleva {v.DaysInInventory} días en inventario, {excess} sobre el límite " +
                      $"de {parameters.MaxDaysInInventory}.",
            Suggestion = "Revisar precio de publicación o considerar venta a un comerciante para " +
                         "liberar el capital.",
            VehicleId = v.VehicleId,
            VehicleLabel = v.Label,
            Magnitude = v.CashInvested
        });
    }

    private static void AddPriceAdjustment(
        List<Alert> alerts, InventorySnapshot v, AnalysisParameters parameters)
    {
        if (v.DaysListed <= parameters.ListedTooLongDays) return;

        // Si ya se avisó por inventario estancado, esta sería la misma noticia dos veces.
        if (v.DaysInInventory > parameters.MaxDaysInInventory) return;

        alerts.Add(new Alert
        {
            Type = AlertType.PriceNeedsAdjustment,
            Severity = AlertSeverity.Warning,
            Message = $"Publicado hace {v.DaysListed} días sin venderse.",
            Suggestion = "Un aviso sin movimiento suele significar precio alto. Comparar contra " +
                         "publicaciones actuales del mismo modelo.",
            VehicleId = v.VehicleId,
            VehicleLabel = v.Label,
            Magnitude = v.CashInvested
        });
    }

    private static void AddLowMargin(
        List<Alert> alerts, InventorySnapshot v, AnalysisParameters parameters)
    {
        if (v.ExpectedSaleValue <= 0m || v.CashInvested <= 0m) return;

        var margin = MoneyMath.SafeDivide(v.ExpectedSaleValue - v.CashInvested, v.ExpectedSaleValue);
        if (margin >= parameters.MinMarginPct) return;

        alerts.Add(new Alert
        {
            Type = AlertType.LowMargin,
            Severity = margin < 0m ? AlertSeverity.Critical : AlertSeverity.Warning,
            Message = margin < 0m
                ? $"Lo invertido ({Clp.Format(v.CashInvested)}) ya supera el valor de venta " +
                  $"estimado ({Clp.Format(v.ExpectedSaleValue)})."
                : $"Margen proyectado de {Clp.Percent(margin)}, bajo el mínimo de " +
                  $"{Clp.Percent(parameters.MinMarginPct)}.",
            Suggestion = "Detener gastos que no sean imprescindibles para vender y revisar el " +
                         "precio objetivo.",
            VehicleId = v.VehicleId,
            VehicleLabel = v.Label,
            Magnitude = v.CashInvested
        });
    }

    private static void AddRepairOverBudget(
        List<Alert> alerts, InventorySnapshot v, AnalysisParameters parameters)
    {
        if (v.RepairBudgeted <= 0m || v.RepairActual <= 0m) return;

        var overrun = MoneyMath.SafeDivide(v.RepairActual - v.RepairBudgeted, v.RepairBudgeted);
        if (overrun <= parameters.RepairOverBudgetTolerancePct) return;

        alerts.Add(new Alert
        {
            Type = AlertType.RepairOverBudget,
            Severity = overrun > 0.5m ? AlertSeverity.Critical : AlertSeverity.Warning,
            Message = $"La reparación superó el presupuesto en {Clp.Percent(overrun)}: " +
                      $"{Clp.Format(v.RepairActual)} contra {Clp.Format(v.RepairBudgeted)} estimados.",
            Suggestion = "Revisar si el tipo de daño se subestima de forma sistemática y ajustar " +
                         "los costos base de reparación.",
            VehicleId = v.VehicleId,
            VehicleLabel = v.Label,
            Magnitude = v.RepairActual - v.RepairBudgeted
        });
    }

    private static void AddCapitalConcentration(
        List<Alert> alerts, InventorySnapshot v, AlertContext context, AnalysisParameters parameters)
    {
        if (context.TotalCapital <= 0m) return;

        var share = v.CashInvested / context.TotalCapital;
        if (share <= parameters.MaxCapitalPerUnitPct) return;

        alerts.Add(new Alert
        {
            Type = AlertType.CapitalConcentration,
            Severity = AlertSeverity.Critical,
            Message = $"Concentra {Clp.Percent(share)} del capital total, sobre el límite de " +
                      $"{Clp.Percent(parameters.MaxCapitalPerUnitPct)}.",
            Suggestion = "Priorizar su venta antes de comprometer capital en nuevas compras.",
            VehicleId = v.VehicleId,
            VehicleLabel = v.Label,
            Magnitude = v.CashInvested
        });
    }

    private static void AddMissingAnalysis(List<Alert> alerts, InventorySnapshot v)
    {
        alerts.Add(new Alert
        {
            Type = AlertType.PurchasedWithoutAnalysis,
            Severity = AlertSeverity.Info,
            Message = "Se registró sin un análisis previo asociado.",
            Suggestion = "Al venderlo no habrá con qué comparar el resultado, así que esta " +
                         "operación no aportará al aprendizaje del sistema.",
            VehicleId = v.VehicleId,
            VehicleLabel = v.Label,
            Magnitude = v.CashInvested
        });
    }

    private static void AddLowCapital(List<Alert> alerts, AlertContext context)
    {
        if (context.TotalCapital <= 0m) return;

        var share = context.AvailableCapital / context.TotalCapital;
        if (share >= 0.15m) return;

        alerts.Add(new Alert
        {
            Type = AlertType.LowAvailableCapital,
            Severity = context.AvailableCapital < 0m ? AlertSeverity.Critical : AlertSeverity.Warning,
            Message = context.AvailableCapital < 0m
                ? $"El capital disponible es negativo ({Clp.Format(context.AvailableCapital)})."
                : $"Solo queda {Clp.Percent(share)} del capital disponible " +
                  $"({Clp.Format(context.AvailableCapital)}).",
            Suggestion = "Casi todo el capital está inmovilizado en vehículos. Conviene cerrar " +
                         "ventas antes de comprometerse en un remate nuevo.",
            Magnitude = Math.Abs(context.AvailableCapital)
        });
    }
}
