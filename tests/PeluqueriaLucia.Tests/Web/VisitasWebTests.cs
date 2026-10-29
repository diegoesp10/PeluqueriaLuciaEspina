using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Domain.Visitas;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>
/// La visita en el panel: precios que se cambian justo antes de cobrar, servicios que se añaden sin perder lo escrito, citas
/// confirmadas que se cobran desde la agenda y el editor de servicios con sus textos en inglés.
/// </summary>
public sealed class VisitasWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    [Fact]
    public async Task Los_precios_se_cambian_en_la_visita_sin_perderlos_al_anadir_y_se_cobra_lo_escrito()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var (clienteId, corte, nombreCorte) = await ClienteYServicioAsync("Corte", 18m);
        var (_, tinte, _) = await ClienteYServicioAsync("Tinte", 35m);

        var registrada = await admin.PostAsync("/admin/Visitas/Nueva", Formulario(await TokenAsync(admin, "/admin/Visitas/Nueva"),
            ("ClienteId", Texto(clienteId)), ("Fecha", DateTime.Now.ToString("yyyy-MM-ddTHH:mm")), ("FormaPago", "Efectivo"),
            ("Servicios", Texto(corte)), ("Accion", "cobrar")));
        Assert.Equal(HttpStatusCode.Redirect, registrada.StatusCode);
        var detalle = registrada.Headers.Location!.OriginalString;
        var visitaId = detalle[(detalle.LastIndexOf('/') + 1)..];
        var pendiente = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        var linea = Regex.Match(pendiente, @"name=""PreciosServicios\[(\d+)\]"" value=""18,00""").Groups[1].Value;
        Assert.NotEmpty(linea);
        Assert.Contains("Cobrar 18,00", pendiente);
        Assert.Contains("/js/visita-editor.js", pendiente);

        // Añadir un servicio guarda también el precio cambiado del primero
        var anadido = await admin.PostAsync($"/admin/Visitas/AgregarServicio/{visitaId}", Formulario(await TokenAsync(admin, detalle),
            ($"PreciosServicios[{linea}]", "21,50"), ("ServicioId", Texto(tinte)), ("PrecioServicio", ""), ("FormaPago", "Efectivo")));
        Assert.Equal(HttpStatusCode.Redirect, anadido.StatusCode);
        var conTinte = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains($"name=\"PreciosServicios[{linea}]\" value=\"21,50\"", conTinte);
        var lineaTinte = Regex.Match(conTinte, @"name=""PreciosServicios\[(\d+)\]"" value=""35,00""").Groups[1].Value;
        Assert.Contains("Cobrar 56,50", conTinte);

        // Un precio que no vale no cobra nada y se vuelve a mostrar tal cual, con el nombre del servicio
        var malo = await admin.PostAsync($"/admin/Visitas/Cobrar/{visitaId}", Formulario(await TokenAsync(admin, detalle),
            ($"PreciosServicios[{linea}]", "veinte"), ($"PreciosServicios[{lineaTinte}]", "35,00"), ("FormaPago", "Efectivo")));
        Assert.Equal(HttpStatusCode.OK, malo.StatusCode);
        var paginaMala = WebUtility.HtmlDecode(await malo.Content.ReadAsStringAsync());
        Assert.Contains($"Escribe el precio de «{nombreCorte}», por ejemplo 12,50.", paginaMala);
        Assert.Contains("value=\"veinte\"", paginaMala);
        Assert.False(await CobradaAsync(int.Parse(visitaId, CultureInfo.InvariantCulture)));

        // Quitar un servicio también guarda el precio escrito en el otro
        var quitado = await admin.PostAsync($"/admin/Visitas/QuitarServicio/{visitaId}?lineaId={lineaTinte}", Formulario(await TokenAsync(admin, detalle),
            ($"PreciosServicios[{linea}]", "25"), ($"PreciosServicios[{lineaTinte}]", "35,00"), ("FormaPago", "Efectivo")));
        Assert.Equal(HttpStatusCode.Redirect, quitado.StatusCode);
        var sinTinte = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.DoesNotContain($"PreciosServicios[{lineaTinte}]", sinTinte);
        Assert.Contains("Cobrar 25,00", sinTinte);

        // Lo escrito a medias para añadir un producto no impide cobrar; se cobra con el precio escrito en ese momento
        var cobro = await admin.PostAsync($"/admin/Visitas/Cobrar/{visitaId}", Formulario(await TokenAsync(admin, detalle),
            ($"PreciosServicios[{linea}]", "30,5"), ("FormaPago", "Tarjeta"), ("Cantidad", "muchas"), ("Precio", "x")));
        Assert.Equal(HttpStatusCode.Redirect, cobro.StatusCode);
        Assert.EndsWith($"/admin/Visitas/Ticket/{visitaId}", cobro.Headers.Location!.OriginalString);
        var ticket = WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Visitas/Ticket/{visitaId}"));
        Assert.Contains("30,50", ticket);
        Assert.DoesNotContain("35,00", ticket);
    }

    [Fact]
    public async Task La_cita_confirmada_se_cobra_desde_la_agenda_y_desde_las_visitas_del_dia()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var (clienteId, corte, nombreCorte) = await ClienteYServicioAsync("Corte de cita", 18m);
        var hoy = DateTime.Today;
        var cita = await CitaAsync(clienteId, corte, nombreCorte, hoy.AddHours(10));
        var sinFicha = await CitaAsync(null, corte, nombreCorte, hoy.AddHours(11));
        var agenda = $"/admin/agenda?dia={hoy:yyyy-MM-dd}";

        var visitasDelDia = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/Visitas"));
        Assert.Contains("Citas por cobrar", visitasDelDia);
        Assert.Contains(cita.ToString(), visitasDelDia);
        var paginaAgenda = WebUtility.HtmlDecode(await admin.GetStringAsync(agenda));
        Assert.Matches($"action=\"/admin/Visitas/DesdeCita\".*?name=\"reservaId\" value=\"{cita}\"", paginaAgenda);
        Assert.Contains("Completar y cobrar", paginaAgenda);
        // También la cita dada sin ficha (al cobrarla recibe una)
        Assert.Matches($"action=\"/admin/Visitas/DesdeCita\".*?name=\"reservaId\" value=\"{sinFicha}\"", paginaAgenda);
        // Completar ya no cierra la cita sin cobrarla
        Assert.DoesNotContain("value=\"Completada\"", paginaAgenda);
        Assert.DoesNotContain("Registrar visita", paginaAgenda);

        var abierta = await admin.PostAsync("/admin/Visitas/DesdeCita", Formulario(await TokenAsync(admin, agenda),
            ("reservaId", cita.ToString()), ("volver", agenda)));
        Assert.Equal(HttpStatusCode.Redirect, abierta.StatusCode);
        var detalle = abierta.Headers.Location!.OriginalString;
        Assert.Matches(@"/admin/Visitas/Detalle/\d+$", detalle);
        var visita = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains(nombreCorte, visita);
        Assert.Contains("Cita de la agenda", visita);
        Assert.Contains("Cobrar · 18,00", WebUtility.HtmlDecode(await admin.GetStringAsync(agenda)));
        Assert.DoesNotContain(cita.ToString(), WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/Visitas")));

        var visitaId = detalle[(detalle.LastIndexOf('/') + 1)..];
        var cobro = await admin.PostAsync($"/admin/Visitas/Cobrar/{visitaId}", Formulario(await TokenAsync(admin, detalle), ("FormaPago", "Efectivo")));
        Assert.Equal(HttpStatusCode.Redirect, cobro.StatusCode);
        var completadas = WebUtility.HtmlDecode(await admin.GetStringAsync(agenda + "&estado=Completadas"));
        Assert.Contains("Cobrada · 18,00", completadas);
        Assert.Contains("Completada y cobrada", completadas);
        await using var db = fixture.Bd.Contexto();
        Assert.Equal(EstadoCita.Completada, (await db.ReservasCitas.SingleAsync(c => c.Id == cita)).Estado);
    }

    [Fact]
    public async Task Un_producto_usado_en_el_servicio_pide_si_se_ha_terminado_y_va_a_cero()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var (clienteId, corte, _) = await ClienteYServicioAsync("Mechas", 40m);
        var tinte = await ProductoAsync("Tinte usado", 3);
        var registrada = await admin.PostAsync("/admin/Visitas/Nueva", Formulario(await TokenAsync(admin, "/admin/Visitas/Nueva"),
            ("ClienteId", Texto(clienteId)), ("Fecha", DateTime.Now.ToString("yyyy-MM-ddTHH:mm")), ("FormaPago", "Efectivo"),
            ("Servicios", Texto(corte)), ("Productos[0].ProductoId", Texto(tinte)), ("Productos[0].Cantidad", "1"), ("Productos[0].Uso", "ServicioEnUso"),
            ("Accion", "cobrar")));
        var detalle = registrada.Headers.Location!.OriginalString;
        var visitaId = detalle[(detalle.LastIndexOf('/') + 1)..];

        var sinContestar = await admin.PostAsync($"/admin/Visitas/AgregarProducto/{visitaId}", Formulario(await TokenAsync(admin, detalle),
            ("ProductoId", Texto(tinte)), ("Cantidad", "2"), ("EnServicio", "true")));
        Assert.Equal(HttpStatusCode.OK, sinContestar.StatusCode);
        Assert.Contains("¿Se ha terminado el producto usado en el servicio?", WebUtility.HtmlDecode(await sinContestar.Content.ReadAsStringAsync()));

        var terminado = await admin.PostAsync($"/admin/Visitas/AgregarProducto/{visitaId}", Formulario(await TokenAsync(admin, detalle),
            ("ProductoId", Texto(tinte)), ("Cantidad", "2"), ("EnServicio", "true"), ("Terminado", "true"), ("Precio", "99")));
        Assert.Equal(HttpStatusCode.Redirect, terminado.StatusCode);
        var pagina = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Incluido en el servicio · terminado, sale del stock", pagina);
        Assert.Contains("Incluido en el servicio · sigue en uso, no sale del stock", pagina);
        Assert.Contains("Cobrar 40,00", pagina);
        Assert.DoesNotContain("name=\"PreciosProductos[", pagina);

        var cobro = await admin.PostAsync($"/admin/Visitas/Cobrar/{visitaId}", Formulario(await TokenAsync(admin, detalle), ("FormaPago", "Efectivo")));
        Assert.Equal(HttpStatusCode.Redirect, cobro.StatusCode);
        await using var db = fixture.Bd.Contexto();
        Assert.Equal(1, await db.Productos.Where(p => p.Id == tinte).Select(p => p.Stock).SingleAsync());
        var ticket = WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Visitas/Ticket/{visitaId}"));
        Assert.Contains("Tinte usado <span class=\"ticket-tenue\"> (incluido en el servicio)</span>", ticket);
        Assert.Contains("40,00", ticket);
    }

    [Fact]
    public async Task Editar_un_servicio_muestra_su_nombre_y_su_descripcion_en_ingles()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        int id;
        await using (var db = fixture.Bd.Contexto())
        {
            var servicio = Servicio.Crear("Corte bilingüe " + Guid.NewGuid().ToString("N")[..6], 18m, Sexo.Mujer, DateTime.Now, "Bilingual cut");
            servicio.ConfigurarPublicacion(null, "Corte con lavado", Domain.Publico.TipoPrecioPublico.Fijo, 30, 0, false, false);
            servicio.Traducir("Bilingual cut", "Cut with wash");
            db.Add(servicio);
            await db.SaveChangesAsync();
            id = servicio.Id;
        }

        var html = WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Catalogo/Editar/{id}"));

        Assert.Matches("id=\"NombreEn\" name=\"NombreEn\" value=\"Bilingual cut\"", html);
        Assert.Contains("lang=\"en\">Cut with wash</textarea>", html);
    }

    private static string Texto(int numero) => numero.ToString(CultureInfo.InvariantCulture);

    private async Task<(int Cliente, int Servicio, string Nombre)> ClienteYServicioAsync(string servicio, decimal precio)
    {
        await using var db = fixture.Bd.Contexto();
        var cliente = Cliente.Crear("Visita", "Web", null, null, Sexo.Mujer, DateTime.Now);
        var nombre = $"{servicio} {Guid.NewGuid().ToString("N")[..6]}";
        var catalogo = Servicio.Crear(nombre, precio, Sexo.Mujer, DateTime.Now, nombre + " (en)");
        db.AddRange(cliente, catalogo);
        await db.SaveChangesAsync();
        return (cliente.Id, catalogo.Id, nombre);
    }

    private async Task<int> ProductoAsync(string nombre, int stock)
    {
        await using var db = fixture.Bd.Contexto();
        var categoria = CategoriaProducto.Crear("Pruebas " + Guid.NewGuid().ToString("N")[..8], DateTime.Now);
        var producto = Producto.Crear(new FichaProducto("WEB-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), null, nombre, null, null, 12m, null, 1),
            categoria, DateTime.Now);
        var alta = producto.Alta(stock, DateTime.Now, null);
        db.Add(producto);
        if (alta is not null)
            db.Add(alta);
        await db.SaveChangesAsync();
        return producto.Id;
    }

    private async Task<bool> CobradaAsync(int visitaId)
    {
        await using var db = fixture.Bd.Contexto();
        return await db.Visitas.Where(v => v.Id == visitaId).Select(v => v.Estado == EstadoVisita.Cobrada).SingleAsync();
    }

    private async Task<Guid> CitaAsync(int? clienteId, int servicioId, string servicio, DateTime inicio)
    {
        await using var db = fixture.Bd.Contexto();
        var cita = new ReservaCita
        {
            Id = Guid.NewGuid(), SolicitudId = Guid.NewGuid(), Inicio = inicio, Fin = inicio.AddMinutes(30), Nombre = "Visita", Apellido = "Web",
            Origen = OrigenCita.Panel, Estado = EstadoCita.Confirmada, ClienteId = clienteId, ServicioId = servicioId, ServicioNombre = servicio,
            TelefonoValidado = true, CreadaUtc = DateTime.UtcNow, ActualizadaUtc = DateTime.UtcNow, CaducaUtc = DateTime.UtcNow, IpHash = "",
        };
        db.ReservasCitas.Add(cita);
        await db.SaveChangesAsync();
        return cita.Id;
    }

    private static FormUrlEncodedContent Formulario(string token, params (string Clave, string Valor)[] campos) =>
        new([new("__RequestVerificationToken", token), .. campos.Select(c => new KeyValuePair<string, string>(c.Clave, c.Valor))]);

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
