using PeluqueriaLucia.Domain.Sorteos;
using PeluqueriaLucia.Web.Areas.Admin.Models;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>
/// El tablero del sorteo: del 1 al último número (talonario o el más alto), libres y ocupados, quién ha salido, y el nombre
/// corto de cada casilla (nombre de pila e inicial, sin puntos suspensivos).
/// </summary>
public sealed class SorteoTableroTests
{
    private static readonly DateTime Ahora = new(2026, 11, 2, 10, 0, 0);

    private static Sorteo ConNumeros(params NumeroSorteo[] numeros)
    {
        var sorteo = Sorteo.Crear(new DatosSorteo("Sorteo de Navidad", "Christmas giveaway", null, null, new DateTime(2026, 12, 20, 18, 0, 0)), Ahora);
        sorteo.AgregarPremio(new DatosPremio(TipoPremio.Servicio, 7, "Tinte ecológico", "Organic dye"), 1, Ahora);
        sorteo.AgregarNumeros(numeros, Ahora);
        return sorteo;
    }

    [Fact]
    public void Enseña_libres_y_ocupados_hasta_el_talonario_y_quien_ha_salido()
    {
        var sorteo = ConNumeros(new NumeroSorteo(2, "María López"), new NumeroSorteo(5, "Rosa Gil"), new NumeroSorteo(7));
        sorteo.CambiarTalonario(10, Ahora);

        var tablero = new SorteoTableroModelo(sorteo, DateOnly.FromDateTime(Ahora));
        Assert.Equal((10, 10, 3, 7, 30), (tablero.Ultimo, tablero.Celdas, tablero.Ocupados, tablero.Libres, tablero.PorcentajeOcupado));
        Assert.Equal([EstadoCelda.Libre, EstadoCelda.Ocupado, EstadoCelda.Libre, EstadoCelda.Libre, EstadoCelda.Ocupado],
            Enumerable.Range(1, 5).Select(tablero.Estado));
        Assert.Empty(tablero.Sorteados);

        // Sale el 2; al repetir sale el 5: el 2 queda como «salió antes»
        sorteo.Sortear(_ => 0, Ahora, null);
        sorteo.Repetir(_ => 0, "No vino a recogerlo", Ahora.AddDays(1), null);
        var despues = new SorteoTableroModelo(sorteo, DateOnly.FromDateTime(Ahora));
        Assert.Equal((EstadoCelda.GanoAntes, EstadoCelda.Ganador, EstadoCelda.Ocupado), (despues.Estado(2), despues.Estado(5), despues.Estado(7)));
        Assert.Equal(["María López", "Rosa Gil"], despues.Sorteados.Select(despues.NombreDe));
        Assert.Equal(("is-gano-antes", "salió antes"), (SorteoTableroModelo.Clase(EstadoCelda.GanoAntes), SorteoTableroModelo.Texto(EstadoCelda.GanoAntes)));
    }

    [Fact]
    public void Sin_talonario_llega_hasta_el_numero_mas_alto_y_vacio_no_tiene_casillas()
    {
        var tablero = new SorteoTableroModelo(ConNumeros(new NumeroSorteo(3), new NumeroSorteo(12)), DateOnly.FromDateTime(Ahora));
        var vacio = new SorteoTableroModelo(Sorteo.Crear(new DatosSorteo("Vacío", "Empty", null, null, new DateTime(2026, 12, 20, 18, 0, 0)), Ahora), DateOnly.FromDateTime(Ahora));

        Assert.Equal((12, 2, 10, 17), (tablero.Ultimo, tablero.Ocupados, tablero.Libres, tablero.PorcentajeOcupado));
        Assert.Equal((0, 0, 0), (vacio.Ultimo, vacio.Celdas, vacio.PorcentajeOcupado));
    }

    [Theory]
    [InlineData("María López", "María L.")]
    [InlineData("Rosa (vecina)", "Rosa V.")]
    [InlineData("Maximiliana Castro", "Maximilia. C.")]
    [InlineData("Lucía", "Lucía")]
    [InlineData("Juan 22", "Juan")]
    public void El_nombre_corto_es_el_de_pila_y_la_inicial(string nombre, string corto)
    {
        var sorteo = ConNumeros(new NumeroSorteo(1, nombre), new NumeroSorteo(2));

        Assert.Equal(corto, FormatoSorteos.NombreCorto(sorteo.Participantes.Single(p => p.Numero == 1)));
        Assert.Null(FormatoSorteos.NombreCorto(sorteo.Participantes.Single(p => p.Numero == 2)));
    }
}
