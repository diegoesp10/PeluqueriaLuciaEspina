using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Application.Publico;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Domain.Visitas;
using PeluqueriaLucia.Infrastructure.Almacenamiento;
using PeluqueriaLucia.Infrastructure.Persistencia;
using PeluqueriaLucia.Tests.Web;

namespace PeluqueriaLucia.Tests.Integracion;

/// <summary>
/// Contenido de la web en los dos idiomas: lo nuevo nace en español e inglés; lo anterior al inglés se traduce cuando se
/// pueda y, mientras, la web en inglés muestra el español.
/// </summary>
public sealed class TextosInglesTests
{
    private static readonly DateTime Ahora = new(2026, 10, 14, 10, 0, 0);

    private static CatalogoService Catalogo(PeluqueriaDbContext db) => new(new ServicioRepositorio(db),
        new AlmacenImagenesLocal(Options.Create(new OpcionesImagenes { Carpeta = Path.Combine(Path.GetTempPath(), "peluqueria-pruebas") }), NullLogger<AlmacenImagenesLocal>.Instance),
        db, TimeProvider.System, new ContenidoRepositorio(db));

    [Fact]
    public void El_dominio_muestra_el_ingles_si_existe_y_el_espanol_si_no()
    {
        var sinTraducir = Servicio.Crear("Recogido", 50m, Sexo.Mujer, Ahora);
        Assert.Null(sinTraducir.NombreEn);
        Assert.Equal("Recogido", sinTraducir.NombreEnIdioma(true));

        var traducido = Servicio.Crear("Recogido", 50m, Sexo.Mujer, Ahora, " Updo ");
        Assert.Equal("Updo", traducido.NombreEn);
        Assert.Equal("Updo", traducido.NombreEnIdioma(true));
        Assert.Equal("Recogido", traducido.NombreEnIdioma(false));
        traducido.Traducir("Updo", "Wedding hairstyle");
        Assert.Equal("Wedding hairstyle", traducido.DescripcionEnIdioma(true));
        Assert.Null(traducido.DescripcionEnIdioma(false));
        Assert.Throws<ReglaDeNegocioException>(() => traducido.Traducir(new string('a', Servicio.LongitudNombre + 1), null));

        var categoria = CategoriaTratamiento.Crear("Novias", 1);
        Assert.Equal("Novias", categoria.NombreEnIdioma(true));
        categoria.Traducir("Brides");
        Assert.Equal("Brides", categoria.NombreEnIdioma(true));
        Assert.Equal("Novias", categoria.NombreEnIdioma(false));

        var resena = Resena.Crear(Guid.NewGuid(), "Marta", "Me encantó el color vegetal", 5, null, Ahora);
        resena.Moderar(EstadoResena.Publicada, "Gracias, Marta", "Thank you, Marta");
        Assert.Equal("Thank you, Marta", resena.RespuestaEnIdioma(true));
        Assert.Equal("Gracias, Marta", resena.RespuestaEnIdioma(false));
        resena.Moderar(EstadoResena.Publicada, null, "Sin respuesta en español");
        Assert.Null(resena.Respuesta);
        Assert.Null(resena.RespuestaEn);
    }

    [Fact]
    public async Task Un_servicio_nuevo_exige_el_ingles_y_uno_anterior_no()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await using var db = bd.Contexto();
        var catalogo = Catalogo(db);
        var categoria = (await new ContenidoRepositorio(db).CategoriasAsync(default))[0];

        var sinIngles = await catalogo.CrearAsync(new DatosServicio("Recogido", 50m, Sexo.Mujer), null);
        Assert.False(sinIngles.Exito);
        Assert.Equal("NombreEn", sinIngles.Error!.Campo);

        var sinDescripcion = await catalogo.CrearAsync(new DatosServicio("Recogido", 50m, Sexo.Mujer,
            new(categoria.Id, "Peinado para bodas", TipoPrecioPublico.Fijo, 60, 0, true, false), "Updo"), null);
        Assert.False(sinDescripcion.Exito);
        Assert.Equal("DescripcionPublicaEn", sinDescripcion.Error!.Campo);

        var completo = await catalogo.CrearAsync(new DatosServicio("Recogido", 50m, Sexo.Mujer,
            new(categoria.Id, "Peinado para bodas", TipoPrecioPublico.Fijo, 60, 0, true, false, "Wedding hairstyle"), "Updo"), null);
        Assert.True(completo.Exito, completo.Error?.Mensaje);
        db.ChangeTracker.Clear();
        var guardado = await db.Servicios.FindAsync(completo.Valor);
        Assert.Equal("Updo", guardado!.NombreEn);
        Assert.Equal("Wedding hairstyle", guardado.DescripcionPublicaEn);

        // Anterior al inglés: editarlo sin traducción es válido y la web en inglés sigue mostrando el español
        var anterior = Servicio.Crear("Trenza", 20m, Sexo.Mujer, Ahora);
        db.Add(anterior);
        await db.SaveChangesAsync();
        var editado = await catalogo.ActualizarAsync(anterior.Id, new DatosServicio("Trenza larga", 22m, Sexo.Mujer), null, false);
        Assert.True(editado.Exito, editado.Error?.Mensaje);
        db.ChangeTracker.Clear();
        var trenza = await db.Servicios.FindAsync(anterior.Id);
        Assert.Null(trenza!.NombreEn);
        Assert.Equal("Trenza larga", trenza.NombreEnIdioma(true));

        Assert.True((await catalogo.ActualizarAsync(anterior.Id, new DatosServicio("Trenza larga", 22m, Sexo.Mujer, NombreEn: "Long braid"), null, false)).Exito);
        db.ChangeTracker.Clear();
        Assert.Equal("Long braid", (await db.Servicios.FindAsync(anterior.Id))!.NombreEn);
    }

    [Fact]
    public async Task Una_categoria_nueva_exige_el_ingles_y_una_anterior_no()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await using var db = bd.Contexto();
        var contenido = new ContenidoService(new ContenidoRepositorio(db), new ServicioRepositorio(db), db);

        var sinIngles = await contenido.GuardarCategoriaAsync(null, "Novias");
        Assert.False(sinIngles.Exito);
        Assert.Equal("NombreEn", sinIngles.Error!.Campo);

        Assert.True((await contenido.GuardarCategoriaAsync(null, "Novias", "Brides")).Exito);
        db.ChangeTracker.Clear();
        Assert.Equal("Brides", (await db.CategoriasPublicas.SingleAsync(c => c.Slug == "novias")).NombreEn);

        var anterior = await db.CategoriasPublicas.FirstAsync(c => c.NombreEn == null);
        Assert.True((await contenido.GuardarCategoriaAsync(anterior.Id, anterior.Nombre + " y más")).Exito);
        db.ChangeTracker.Clear();
        Assert.Null((await db.CategoriasPublicas.FindAsync(anterior.Id))!.NombreEn);
    }

    [Fact]
    public async Task La_web_en_ingles_muestra_lo_traducido_y_el_espanol_de_lo_demas()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await using (var db = bd.Contexto())
        {
            var categoria = CategoriaTratamiento.Crear("Novias", 99, "Brides");
            db.Add(categoria);
            await db.SaveChangesAsync();
            var recogido = Servicio.Crear("Recogido de novia", 50m, Sexo.Mujer, Ahora, "Bridal updo");
            recogido.ConfigurarPublicacion(categoria.Id, "Peinado para bodas", TipoPrecioPublico.Fijo, 60, 1, true, false);
            recogido.Traducir("Bridal updo", "Wedding hairstyle");
            var trenza = Servicio.Crear("Trenza de espiga", 20m, Sexo.Mujer, Ahora);
            trenza.ConfigurarPublicacion(categoria.Id, "Trenza clásica", TipoPrecioPublico.Fijo, 30, 2, true, false);
            db.AddRange(recogido, trenza);
            await db.SaveChangesAsync();
        }

        await using var fabrica = new FabricaWeb(bd.Cadena);
        using var cliente = fabrica.Cliente();

        var en = WebUtility.HtmlDecode(await cliente.GetStringAsync("/en/servicios"));
        Assert.Contains("Brides", en);
        Assert.Contains("Bridal updo", en);
        Assert.Contains("Wedding hairstyle", en);
        Assert.DoesNotContain("Recogido de novia", en);
        Assert.Contains("Trenza de espiga", en);
        Assert.Contains("Trenza clásica", en);

        var es = WebUtility.HtmlDecode(await cliente.GetStringAsync("/servicios"));
        Assert.Contains("Novias", es);
        Assert.Contains("Recogido de novia", es);
        Assert.DoesNotContain("Bridal updo", es);

        var citas = WebUtility.HtmlDecode(await cliente.GetStringAsync("/en/citas"));
        Assert.Contains("Bridal updo", citas);
        Assert.Contains("Trenza de espiga", citas);
        Assert.Contains("Recogido de novia", WebUtility.HtmlDecode(await cliente.GetStringAsync("/citas")));
    }
}

/// <summary>La respuesta del salón a una reseña: nueva en los dos idiomas; en la web inglesa, la inglesa o la española.</summary>
public sealed class TextosInglesResenasTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    [Fact]
    public async Task Una_respuesta_nueva_exige_el_ingles_y_la_web_la_muestra_en_cada_idioma()
    {
        Guid resenaId;
        await using (var db = fixture.Bd.Contexto())
        {
            var cliente = Cliente.Crear("Marta", "Prueba", null, null, Sexo.Mujer, DateTime.UtcNow);
            var servicio = Servicio.Crear("Color vegetal prueba", 40m, Sexo.Mujer, DateTime.UtcNow, "Plant-based colour test");
            db.AddRange(cliente, servicio);
            await db.SaveChangesAsync();
            var visita = Visita.Registrar(cliente, DateTime.Now, FormaPago.Efectivo, [servicio]);
            visita.Cobrar(FormaPago.Efectivo, null, null);
            db.Add(visita);
            await db.SaveChangesAsync();
            var permiso = PermisoResena.Crear(visita.Id, "hash-ingles", "protegido", DateTime.UtcNow);
            db.Add(permiso);
            var resena = Resena.Crear(permiso.Id, "Marta", "Me encantó el color vegetal de verdad", 5, null, DateTime.UtcNow);
            db.Resenas.Add(resena);
            await db.SaveChangesAsync();
            resenaId = resena.Id;
        }

        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        using var publico = fabrica.Cliente();
        await EntrarAsync(admin);
        var ruta = $"/admin/Resenas/Moderar/{resenaId}";

        // Sin inglés no se guarda la respuesta nueva
        await admin.PostAsync(ruta, new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Estado"] = "Publicada", ["Respuesta"] = "Gracias, Marta", ["__RequestVerificationToken"] = await TokenAsync(admin, "/admin/Resenas") }));
        await using (var db = fixture.Bd.Contexto())
            Assert.Null((await db.Resenas.SingleAsync(r => r.Id == resenaId)).Respuesta);

        await admin.PostAsync(ruta, new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Estado"] = "Publicada", ["Respuesta"] = "Gracias, Marta", ["RespuestaEn"] = "Thank you, Marta", ["__RequestVerificationToken"] = await TokenAsync(admin, "/admin/Resenas") }));
        await using (var db = fixture.Bd.Contexto())
        {
            var guardada = await db.Resenas.SingleAsync(r => r.Id == resenaId);
            Assert.Equal("Gracias, Marta", guardada.Respuesta);
            Assert.Equal("Thank you, Marta", guardada.RespuestaEn);
        }

        Assert.Contains("Thank you, Marta", WebUtility.HtmlDecode(await publico.GetStringAsync("/en/opiniones")));
        var es = WebUtility.HtmlDecode(await publico.GetStringAsync("/opiniones"));
        Assert.Contains("Gracias, Marta", es);
        Assert.DoesNotContain("Thank you, Marta", es);
    }

    private async Task EntrarAsync(HttpClient cliente)
    {
        var recuperacion = await fixture.RecuperacionAsync("prueba");
        await cliente.PostAsync("/admin/acceso", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Usuario"] = "prueba", ["Contrasena"] = SeguridadWebFixture.ClavePrueba, ["__RequestVerificationToken"] = await TokenAsync(cliente, "/admin/acceso") }));
        var r = await cliente.PostAsync("/admin/acceso/verificar", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Codigo"] = recuperacion, ["__RequestVerificationToken"] = await TokenAsync(cliente, "/admin/acceso/verificar") }));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
    }

    private static async Task<string> TokenAsync(HttpClient cliente, string ruta)
    {
        var pagina = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        var campo = Regex.Match(await pagina.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(campo.Success);
        return WebUtility.HtmlDecode(campo.Groups[1].Value);
    }
}
