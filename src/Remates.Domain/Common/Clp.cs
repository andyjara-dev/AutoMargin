using System.Globalization;

namespace Remates.Domain.Common;

/// <summary>
/// Formato de moneda y porcentaje para los mensajes generados por los motores.
/// Se fija la cultura explícitamente para que el texto no dependa de la configuración del servidor.
/// </summary>
public static class Clp
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-CL");

    public static string Format(decimal amount) => "$" + amount.ToString("N0", Culture);

    public static string Percent(decimal rate, int decimals = 1)
        => rate.ToString("P" + decimals, Culture);

    /// <summary>
    /// La rentabilidad anualizada de una operación corta llega a cuatro dígitos. Es correcta,
    /// pero mostrarla cruda no comunica nada, así que se acota en la presentación.
    /// </summary>
    public static string AnnualizedPercent(decimal rate)
        => rate >= 10m ? "más de 1.000%" : Percent(rate, 0);
}
