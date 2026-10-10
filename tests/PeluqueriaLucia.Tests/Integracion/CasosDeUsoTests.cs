using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Application.Clientes;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Visitas;
using PeluqueriaLucia.Infrastructure.Almacenamiento;
using PeluqueriaLucia.Infrastructure.Exportacion;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Tests.Integracion;

/// <summary>Casos de uso completos contra SQL Server real (la semántica de NULL y los agregados dependen del motor).</summary>
public sealed class CasosDeUsoTests : IAsyncLifetime
{
    private BaseDeDatosPrueba _bd = null!;

    public async Task InitializeAsync() => _bd = await BaseDeDatosPrueba.CrearAsync();

    public async Task DisposeAsync() => await _bd.DisposeAsync();

    [Fact]
    public async Task Se_pueden_dar_de_alta_varios_clientes_sin_email_ni_telefono()
    {
        var (clientes, _, _, _) = Servicios();

        var primero = await clientes.CrearAsync(new DatosCliente("Ana", "García", null, null, Sexo.Mujer));
        var segundo = await clientes.CrearAsync(new DatosCliente("Marta", "López", " ", "", Sexo.Mujer));

        Assert.True(primero.Exito, primero.Error?.Mensaje);
        Assert.True(segundo.Exito, segundo.Error?.Mensaje);
    }

    [Fact]
    public async Task Editar_un_cliente_sin_cambiar_su_email_no_lo_detecta_como_duplicado()
    {
        var (clientes, _, _, _) = Servicios();
        var id = (await clientes.CrearAsync(new DatosCliente("Luis", "Pérez", "luis@ejemplo.es", "600111222", Sexo.Hombre))).Valor;

        var resultado = await Servicios().Clientes.ActualizarAsync(id, new DatosCliente("Luis", "Pérez Gil", "luis@ejemplo.es", "600111222", Sexo.Hombre));

        Assert.True(resultado.Exito, resultado.Error?.Mensaje);
    }

    [Fact]
    public async Task Un_email_de_otro_cliente_si_es_duplicado()
    {
        var (clientes, _, _, _) = Servicios();
        await clientes.CrearAsync(new DatosCliente("Luis", "Pérez", "luis@ejemplo.es", null, Sexo.Hombre));

        var resultado = await Servicios().Clientes.CrearAsync(new DatosCliente("Otro", "Luis", "LUIS@ejemplo.es", null, Sexo.Hombre));

        Assert.False(resultado.Exito);
        Assert.Equal("Email", resultado.Error!.Campo);
    }

    [Fact]
    public async Task Registrar_cobrar_y_resumir_un_dia()
    {
        var (clientes, catalogo, visitas, _) = Servicios();
        var ana = (await clientes.CrearAsync(new DatosCliente("Ana", "García", null, null, Sexo.Mujer))).Valor;
        var corte = (await catalogo.CrearAsync(new DatosServicio("Corte", 18.50m, Sexo.Mujer), null)).Valor;
        var tinte = (await catalogo.CrearAsync(new DatosServicio("Tinte", 35m, Sexo.Mujer), null)).Valor;
        var dia = new DateTime(2026, 10, 10, 10, 0, 0);

        var cobrada = (await visitas.RegistrarAsync(new NuevaVisita(ana, dia, FormaPago.Efectivo, [corte, tinte]))).Valor;
        var pendiente = (await visitas.RegistrarAsync(new NuevaVisita(ana, dia.AddHours(2), FormaPago.Tarjeta, [corte]))).Valor;
        await visitas.RegistrarAsync(new NuevaVisita(ana, dia.AddDays(1), FormaPago.Tarjeta, [tinte]));

        var cobro = await Servicios().Visitas.CobrarAsync(cobrada, FormaPago.Mixto, 20m, 33.50m);
        Assert.True(cobro.Exito, cobro.Error?.Mensaje);

        var segundoCobro = await Servicios().Visitas.CobrarAsync(cobrada, FormaPago.Efectivo, null, null);
        Assert.False(segundoCobro.Exito);

        var resumen = await Servicios().Visitas.ResumirAsync(FiltroVisitas.Dia(DateOnly.FromDateTime(dia)));
        Assert.Equal(new ResumenVisitas(Visitas: 2, Servicios: 3, Facturado: 72m, Cobrado: 53.50m, Efectivo: 20m, Tarjeta: 33.50m), resumen);
        Assert.Equal(18.50m, resumen.Pendiente);

        var pagina = await Servicios().Visitas.ListarAsync(FiltroVisitas.Dia(DateOnly.FromDateTime(dia)), 1);
        Assert.Equal([pendiente, cobrada], pagina.Elementos.Select(v => v.Id));
    }

    [Fact]
    public async Task Un_resumen_sin_visitas_es_cero()
    {
        var resumen = await Servicios().Visitas.ResumirAsync(FiltroVisitas.Dia(new DateOnly(2020, 1, 1)));
        Assert.Equal(ResumenVisitas.Vacio, resumen);
    }

    [Fact]
    public async Task Cambiar_el_precio_del_catalogo_no_altera_visitas_anteriores()
    {
        var (clientes, catalogo, visitas, _) = Servicios();
        var ana = (await clientes.CrearAsync(new DatosCliente("Ana", "García", null, null, Sexo.Mujer))).Valor;
        var corte = (await catalogo.CrearAsync(new DatosServicio("Corte", 18.50m, Sexo.Mujer), null)).Valor;
        var visita = (await visitas.RegistrarAsync(new NuevaVisita(ana, DateTime.Today, FormaPago.Efectivo, [corte]))).Valor;

        await Servicios().Catalogo.ActualizarAsync(corte, new DatosServicio("Corte y peinado", 22m, Sexo.Mujer), null, false);

        var guardada = await Servicios().Visitas.ObtenerAsync(visita);
        Assert.Equal(18.50m, guardada!.Total);
        Assert.Equal("Corte", guardada.Lineas.Single().Nombre);
    }

    [Fact]
    public async Task Un_servicio_usado_en_visitas_se_archiva_en_vez_de_borrarse()
    {
        var (clientes, catalogo, visitas, _) = Servicios();
        var ana = (await clientes.CrearAsync(new DatosCliente("Ana", "García", null, null, Sexo.Mujer))).Valor;
        var corte = (await catalogo.CrearAsync(new DatosServicio("Corte", 18.50m, Sexo.Mujer), null)).Valor;
        var suelto = (await catalogo.CrearAsync(new DatosServicio("Sin usar", 5m, Sexo.Mujer), null)).Valor;
        await visitas.RegistrarAsync(new NuevaVisita(ana, DateTime.Today, FormaPago.Efectivo, [corte]));

        Assert.Equal(Eliminacion.Archivado, (await Servicios().Catalogo.EliminarAsync(corte)).Valor);
        Assert.Equal(Eliminacion.Eliminado, (await Servicios().Catalogo.EliminarAsync(suelto)).Valor);

        var activos = await Servicios().Catalogo.ListarAsync(null, incluirArchivados: false);
        Assert.DoesNotContain(activos,s => s.Id == corte || s.Id == suelto);
        Assert.Equal(1, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM ServiciosGenericosRealizados"));
    }

    [Fact]
    public async Task Un_cliente_con_visitas_no_se_puede_eliminar()
    {
        var (clientes, catalogo, visitas, _) = Servicios();
        var ana = (await clientes.CrearAsync(new DatosCliente("Ana", "García", null, null, Sexo.Mujer))).Valor;
        var corte = (await catalogo.CrearAsync(new DatosServicio("Corte", 18.50m, Sexo.Mujer), null)).Valor;
        await visitas.RegistrarAsync(new NuevaVisita(ana, DateTime.Today, FormaPago.Efectivo, [corte]));

        var resultado = await Servicios().Clientes.EliminarAsync(ana);

        Assert.False(resultado.Exito);
        Assert.Equal(1, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM Clientes"));
    }

    [Fact]
    public async Task La_busqueda_de_clientes_encuentra_por_varias_palabras_y_escapa_comodines()
    {
        var (clientes, _, _, _) = Servicios();
        await clientes.CrearAsync(new DatosCliente("Ana", "García Ruiz", null, "600 111 222", Sexo.Mujer));
        await clientes.CrearAsync(new DatosCliente("Juana", "Pérez", null, null, Sexo.Mujer));

        Assert.Equal(["Ana García Ruiz"], (await clientes.SugerirAsync("ana ruiz")).Select(c => c.NombreCompleto));
        Assert.Equal(2, (await clientes.SugerirAsync("ana")).Count);
        Assert.Single(await clientes.SugerirAsync("111"));
        Assert.Empty(await clientes.SugerirAsync("%"));
    }

    [Fact]
    public async Task Exportar_genera_un_libro_de_excel()
    {
        var (clientes, catalogo, visitas, _) = Servicios();
        var ana = (await clientes.CrearAsync(new DatosCliente("Ana", "García", null, null, Sexo.Mujer))).Valor;
        var corte = (await catalogo.CrearAsync(new DatosServicio("Corte", 18.50m, Sexo.Mujer), null)).Valor;
        await visitas.RegistrarAsync(new NuevaVisita(ana, new DateTime(2026, 10, 10, 10, 0, 0), FormaPago.Efectivo, [corte]));

        var archivo = await Servicios().Visitas.ExportarAsync(FiltroVisitas.Mes(2026, 10));
        var vacio = await Servicios().Visitas.ExportarAsync(FiltroVisitas.Mes(2026, 9));

        Assert.True(archivo.Exito);
        Assert.StartsWith("PK", System.Text.Encoding.ASCII.GetString(archivo.Valor!.Contenido, 0, 2));
        Assert.Contains("octubre de 2026", archivo.Valor.NombreArchivo);
        Assert.False(vacio.Exito);
    }

    /// <summary>Servicios de aplicación con un contexto nuevo, como en una petición web.</summary>
    private (ClientesService Clientes, CatalogoService Catalogo, VisitasService Visitas, PeluqueriaDbContext Db) Servicios()
    {
        var db = _bd.Contexto();
        var reloj = TimeProvider.System;
        var clientes = new ClienteRepositorio(db);
        var servicios = new ServicioRepositorio(db);
        var imagenes = new AlmacenImagenesLocal(
            Options.Create(new OpcionesImagenes { Carpeta = Path.Combine(Path.GetTempPath(), "peluqueria-pruebas") }),
            NullLogger<AlmacenImagenesLocal>.Instance);
        return (
            new ClientesService(clientes, db, reloj),
            new CatalogoService(servicios, imagenes, db, reloj),
            new VisitasService(new VisitaRepositorio(db), clientes, servicios, new ExportadorExcelVisitas(reloj), db),
            db);
    }
}
