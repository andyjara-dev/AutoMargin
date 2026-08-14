using Remates.Domain.Market;

namespace Remates.Domain.Tests;

public class ListingParserTests
{
    private static readonly string[] Makes =
        ["Toyota", "Chevrolet", "Hyundai", "Kia", "Mazda", "Mercedes-Benz", "Suzuki", "Nissan"];

    [Fact]
    public void Reconoce_un_aviso_tipico_chileno()
    {
        var result = ListingParser.Parse(
            "Toyota Yaris Sport 2018 1.5 GLI\n$ 12.400.000\n80.000 km\nAutomático · Bencina",
            Makes);

        Assert.Equal(12_400_000m, result.Price);
        Assert.Equal(2018, result.Year);
        Assert.Equal(80_000, result.MileageKm);
        Assert.Equal("Toyota", result.Make);
        Assert.Equal("Automatic", result.Transmission);
        Assert.Equal("Gasoline", result.Fuel);
        Assert.True(result.IsUsable);
        Assert.Empty(result.Missing);
    }

    /// <summary>
    /// El error más caro posible: tomar el kilometraje como precio invertiría la valuación
    /// entera y el sistema recomendaría comprar cualquier cosa.
    /// </summary>
    [Fact]
    public void No_confunde_el_kilometraje_con_el_precio()
    {
        var result = ListingParser.Parse("Kia Morning 2019 $ 7.300.000 · 63.000 km", Makes);

        Assert.Equal(7_300_000m, result.Price);
        Assert.Equal(63_000, result.MileageKm);
    }

    [Fact]
    public void Reconoce_el_precio_aunque_no_lleve_simbolo()
    {
        var result = ListingParser.Parse("Hyundai Accent 2019, precio 9.800.000, 54.000 km", Makes);

        Assert.Equal(9_800_000m, result.Price);
        Assert.Equal(54_000, result.MileageKm);
    }

    [Fact]
    public void Sin_simbolo_ni_palabra_toma_la_cifra_mayor_que_no_sea_el_kilometraje()
    {
        var result = ListingParser.Parse("Mazda 3 2017 10.600.000 105.000 km", Makes);

        Assert.Equal(10_600_000m, result.Price);
        Assert.Equal(105_000, result.MileageKm);
    }

    [Fact]
    public void Interpreta_el_punto_como_separador_de_miles_y_no_como_decimal()
    {
        var result = ListingParser.Parse("Suzuki Swift 2020 $ 11.200.000 41.000 km", Makes);

        // Leyéndolo como decimal daría 11,2 en vez de once millones doscientos mil.
        Assert.Equal(11_200_000m, result.Price);
    }

    [Fact]
    public void Entiende_el_kilometraje_escrito_en_miles()
    {
        var result = ListingParser.Parse("Nissan Versa 2018 $ 8.400.000, 92 mil km", Makes);

        Assert.Equal(92_000, result.MileageKm);
    }

    /// <summary>
    /// Los avisos mencionan otros años: revisión técnica, permiso de circulación.
    /// El del vehículo es el más antiguo de los plausibles.
    /// </summary>
    [Fact]
    public void Elige_el_ano_del_vehiculo_entre_varios_mencionados()
    {
        var result = ListingParser.Parse(
            "Chevrolet Sail 2018, revisión técnica al día 2026, permiso 2026. $ 6.900.000, 78.000 km",
            Makes);

        Assert.Equal(2018, result.Year);
    }

    [Fact]
    public void Prefiere_la_marca_mas_larga_ante_coincidencias_parciales()
    {
        var result = ListingParser.Parse("Mercedes-Benz C200 2019 $ 22.000.000 45.000 km", Makes);

        Assert.Equal("Mercedes-Benz", result.Make);
    }

    [Fact]
    public void Extrae_el_modelo_que_sigue_a_la_marca()
    {
        var result = ListingParser.Parse("Toyota Yaris Sport 2018 $ 12.400.000 80.000 km", Makes);

        Assert.Equal("Yaris Sport", result.Model);
    }

    [Fact]
    public void Recoge_la_direccion_del_aviso_si_viene_en_el_texto()
    {
        var result = ListingParser.Parse(
            "Kia Morning 2019 $ 7.300.000 63.000 km https://ejemplo.cl/aviso/123.", Makes);

        Assert.Equal("https://ejemplo.cl/aviso/123", result.Url);
    }

    [Fact]
    public void Informa_que_campos_faltan_en_vez_de_inventarlos()
    {
        var result = ListingParser.Parse("Auto en buen estado, conversable", Makes);

        Assert.False(result.IsUsable);
        Assert.Contains("precio", result.Missing);
        Assert.Contains("año", result.Missing);
        Assert.Contains("kilometraje", result.Missing);
        Assert.Contains("marca", result.Missing);
    }

    [Fact]
    public void Un_aviso_con_precio_y_ano_ya_sirve_como_comparable()
    {
        var result = ListingParser.Parse("Toyota 2018 $ 12.400.000", Makes);

        Assert.True(result.IsUsable);
        Assert.Contains("kilometraje", result.Missing);
    }

    [Theory]
    [InlineData("Caja mecánica", "Manual")]
    [InlineData("Transmisión automática", "Automatic")]
    [InlineData("Caja CVT", "Cvt")]
    public void Reconoce_la_transmision(string text, string expected)
    {
        var result = ListingParser.Parse($"Toyota Corolla 2018 $ 12.000.000 50.000 km. {text}", Makes);

        Assert.Equal(expected, result.Transmission);
    }

    [Theory]
    [InlineData("Bencinero", "Gasoline")]
    [InlineData("Motor diésel", "Diesel")]
    [InlineData("Híbrido enchufable", "Hybrid")]
    public void Reconoce_el_combustible(string text, string expected)
    {
        var result = ListingParser.Parse($"Toyota Corolla 2018 $ 12.000.000 50.000 km. {text}", Makes);

        Assert.Equal(expected, result.Fuel);
    }

    [Fact]
    public void Un_texto_vacio_no_revienta()
    {
        var result = ListingParser.Parse(null, Makes);

        Assert.False(result.IsUsable);
        Assert.NotEmpty(result.Missing);
    }

    [Fact]
    public void Sin_catalogo_de_marcas_igual_extrae_las_cifras()
    {
        var result = ListingParser.Parse("Vehículo 2018 $ 12.400.000 80.000 km");

        Assert.Equal(12_400_000m, result.Price);
        Assert.Equal(2018, result.Year);
        Assert.Null(result.Make);
        Assert.True(result.IsUsable);
    }

    [Fact]
    public void Descarta_kilometrajes_absurdos()
    {
        var result = ListingParser.Parse("Toyota Corolla 2018 $ 12.000.000 5.000.000 km", Makes);

        Assert.Null(result.MileageKm);
        Assert.Contains("kilometraje", result.Missing);
    }

    /// <summary>
    /// La región no es un adorno: un auto de Punta Arenas no es comparable con uno de Santiago,
    /// y el traslado se paga. Se reconoce escrita de cualquiera de las formas usuales.
    /// </summary>
    [Theory]
    [InlineData("Toyota Yaris 2018 $ 9.800.000 50.000 km, Región Metropolitana", "Metropolitana")]
    [InlineData("Toyota Yaris 2018 $ 9.800.000 50.000 km, Region Metropolitana", "Metropolitana")]
    [InlineData("Toyota Yaris 2018 $ 9.800.000 50.000 km, Santiago centro", "Metropolitana")]
    [InlineData("Toyota Yaris 2018 $ 9.800.000 50.000 km, Viña del Mar", "Valparaíso")]
    [InlineData("Toyota Yaris 2018 $ 9.800.000 50.000 km, Vina del Mar", "Valparaíso")]
    [InlineData("Toyota Yaris 2018 $ 9.800.000 50.000 km, Concepción", "Biobío")]
    [InlineData("Toyota Yaris 2018 $ 9.800.000 50.000 km, Punta Arenas", "Magallanes")]
    [InlineData("Toyota Yaris 2018 $ 9.800.000 50.000 km, Chillán", "Ñuble")]
    public void Reconoce_la_region_con_o_sin_tildes(string text, string expected)
    {
        var result = ListingParser.Parse(text, Makes);

        Assert.Equal(expected, result.Region);
    }

    [Fact]
    public void No_inventa_una_region_cuando_el_aviso_no_la_menciona()
    {
        var result = ListingParser.Parse("Toyota Yaris 2018 $ 9.800.000 50.000 km", Makes);

        Assert.Null(result.Region);
    }
}
