using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Tests.Integracion;
using SkiaSharp;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>El recorrido completo en el panel: alta con foto, venta en una visita, cobro, ticket e historial de stock.</summary>
public sealed class ProductosWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>, IDisposable
{
    private readonly string _carpeta = Path.GetFullPath(Path.Combine(".artifacts", "productos-web-" + Guid.NewGuid().ToString("N")));

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
            Directory.Delete(_carpeta, recursive: true);
    }

    [Fact]
    public async Task Alta_con_foto_venta_en_una_visita_cobro_ticket_e_historial()
    {
        using var fabrica = Fabrica();
        using var publico = fabrica.Cliente();
        using var admin = fabrica.Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await publico.GetAsync("/admin/Productos")).StatusCode);
        await EntrarAsync(admin);
        var referencia = Referencia();

        var creado = await admin.PostAsync("/admin/Productos/Nuevo", await AltaAsync(admin, referencia, stock: 3, Foto()));
        Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
        var ficha = creado.Headers.Location!.OriginalString;
        var productoId = int.Parse(ficha[(ficha.LastIndexOf('/') + 1)..], System.Globalization.CultureInfo.InvariantCulture);
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync(ficha));
        Assert.Contains("Champú de prueba web", html);
        Assert.Contains(referencia, html);
        Assert.Contains("Alta", html);

        // La foto solo se sirve con sesión en el panel, como JPEG ya procesado
        var foto = Regex.Match(html, "/admin/productos/foto/([0-9a-f]{32})").Groups[1].Value;
        var miniatura = await admin.GetAsync($"/admin/productos/foto/{foto}?mini=true");
        Assert.Equal(HttpStatusCode.OK, miniatura.StatusCode);
        Assert.Equal("image/jpeg", miniatura.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Redirect, (await publico.GetAsync($"/admin/productos/foto/{foto}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/admin/productos/foto/{Guid.NewGuid():N}")).StatusCode);

        // El buscador del inventario lo encuentra por la referencia y «Nueva visita» lo ofrece
        Assert.Contains(referencia, await admin.GetStringAsync($"/admin/Productos?q={referencia}"));
        var nueva = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/Visitas/Nueva"));
        Assert.Matches($"<option value=\"{productoId}\"[^>]*data-stock=\"3\"", nueva);

        // Visita solo con productos: 2 unidades a 12,50 €
        var clienteId = await ClienteAsync();
        var token = await TokenAsync(admin, "/admin/Visitas/Nueva");
        var registrada = await admin.PostAsync("/admin/Visitas/Nueva", Formulario(token,
            ("ClienteId", clienteId.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("Fecha", DateTime.Now.ToString("yyyy-MM-ddTHH:mm")),
            ("FormaPago", "Efectivo"), ("Productos[0].ProductoId", productoId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("Productos[0].Cantidad", "2"), ("Productos[0].Precio", "12,50"), ("Accion", "cobrar")));
        Assert.Equal(HttpStatusCode.Redirect, registrada.StatusCode);
        var detalle = registrada.Headers.Location!.OriginalString;
        var visitaId = detalle[(detalle.LastIndexOf('/') + 1)..];
        var paginaDetalle = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Champú de prueba web", paginaDetalle);
        Assert.Contains("3 en stock", paginaDetalle);
        Assert.Contains("Cobrar 25,00", paginaDetalle);
        Assert.Equal(3, await StockAsync(productoId)); // pendiente de cobro: el stock no cambia

        var cobro = await admin.PostAsync($"/admin/Visitas/Cobrar/{visitaId}", Formulario(await TokenAsync(admin, detalle), ("FormaPago", "Efectivo")));
        Assert.Equal(HttpStatusCode.Redirect, cobro.StatusCode);
        Assert.Equal(1, await StockAsync(productoId));

        var ticket = WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Visitas/Ticket/{visitaId}"));
        Assert.Matches("<td class=\"uds\">2</td><td>Champú de prueba web", ticket);
        Assert.Contains("25,00", ticket);

        var historial = WebUtility.HtmlDecode(await admin.GetStringAsync(ficha));
        Assert.Contains($"visita {visitaId}", historial);
        Assert.Contains("−2", historial);
    }

    [Fact]
    public async Task Vender_y_usar_mas_de_lo_que_hay_deja_el_stock_en_negativo_y_lo_avisa()
    {
        using var fabrica = Fabrica();
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var productoId = await ProductoAsync(stock: 1);
        var tinteId = await ProductoAsync(stock: 3, nombre: "Tinte de prueba");
        var clienteId = await ClienteAsync();

        // Dos unidades cuando solo hay una: se registra, y el cobro avisa de que el stock quedará en negativo
        var token = await TokenAsync(admin, "/admin/Visitas/Nueva");
        var registrada = await admin.PostAsync("/admin/Visitas/Nueva", Formulario(token,
            ("ClienteId", clienteId.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("Fecha", DateTime.Now.ToString("yyyy-MM-ddTHH:mm")),
            ("FormaPago", "Efectivo"), ("Productos[0].ProductoId", productoId.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("Productos[0].Cantidad", "2"),
            ("Accion", "cobrar")));
        Assert.Equal(HttpStatusCode.Redirect, registrada.StatusCode);
        var detalle = registrada.Headers.Location!.OriginalString;
        var visitaId = detalle[(detalle.LastIndexOf('/') + 1)..];
        var pendiente = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Al cobrar quedará en negativo: «Mascarilla de prueba» (−1)", pendiente);
        Assert.Contains("¿Qué productos has utilizado?", pendiente);
        Assert.Contains("/js/cobro-productos.js", pendiente);

        // Al cobrar se indican los productos utilizados en el servicio: no se cobran, pero salen del stock
        var invalido = await admin.PostAsync($"/admin/Visitas/Cobrar/{visitaId}", Formulario(await TokenAsync(admin, detalle),
            ("FormaPago", "Efectivo"), ("Usados[0].ProductoId", tinteId.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("Usados[0].Cantidad", "0")));
        Assert.Equal(HttpStatusCode.OK, invalido.StatusCode);
        Assert.Contains("Indica entre 1 y 99 unidades de cada producto utilizado.", WebUtility.HtmlDecode(await invalido.Content.ReadAsStringAsync()));
        Assert.Equal(3, await StockAsync(tinteId));

        var cobro = await admin.PostAsync($"/admin/Visitas/Cobrar/{visitaId}", Formulario(await TokenAsync(admin, detalle),
            ("FormaPago", "Efectivo"), ("Usados[0].ProductoId", tinteId.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("Usados[0].Cantidad", "4")));
        Assert.Equal(HttpStatusCode.Redirect, cobro.StatusCode);
        Assert.Equal((-1, -1), (await StockAsync(productoId), await StockAsync(tinteId)));
        var cobrada = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Productos utilizados", cobrada);
        Assert.Contains("Tinte de prueba", cobrada);
        Assert.Contains("Faltan 1 por reponer", WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Productos/Ficha/{tinteId}")));
        Assert.Contains("−1 ud", WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/Productos?estado=PorReponer")));

        // Los movimientos a mano tampoco se bloquean; una entrada y un recuento lo devuelven a lo que hay
        var ficha = $"/admin/Productos/Ficha/{productoId}";
        var gasto = await admin.PostAsync($"/admin/Productos/Movimiento/{productoId}", Formulario(await TokenAsync(admin, ficha),
            ("Tipo", "UsoInterno"), ("Cantidad", "2")));
        Assert.Equal(HttpStatusCode.Redirect, gasto.StatusCode);
        Assert.Equal(-3, await StockAsync(productoId));
        var entrada = await admin.PostAsync($"/admin/Productos/Movimiento/{productoId}", Formulario(await TokenAsync(admin, ficha),
            ("Tipo", "Entrada"), ("Cantidad", "5"), ("Nota", "Pedido de prueba")));
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
        var recuento = await admin.PostAsync($"/admin/Productos/Movimiento/{productoId}", Formulario(await TokenAsync(admin, ficha),
            ("Tipo", "Recuento"), ("Cantidad", "4")));
        Assert.Equal(HttpStatusCode.Redirect, recuento.StatusCode);

        Assert.Equal(4, await StockAsync(productoId));
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync(ficha));
        Assert.Contains("Pedido de prueba", html);
        Assert.Contains("Recuento", html);
        Assert.Contains($"visita {visitaId}", html);
    }

    [Fact]
    public async Task Productos_en_el_detalle_de_una_visita_pendiente()
    {
        using var fabrica = Fabrica();
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var productoId = await ProductoAsync(stock: 4);
        int visitaId;
        await using (var db = fixture.Bd.Contexto())
        {
            var cliente = Cliente.Crear("Visita", "Productos", null, null, Sexo.Mujer, DateTime.Now);
            var servicio = Domain.Catalogo.Servicio.Crear("Corte de prueba", 15m, Sexo.Mujer, DateTime.Now);
            db.AddRange(cliente, servicio);
            await db.SaveChangesAsync();
            var visita = Domain.Visitas.Visita.Registrar(cliente, DateTime.Now, Domain.Visitas.FormaPago.Efectivo, [servicio]);
            db.Add(visita);
            await db.SaveChangesAsync();
            visitaId = visita.Id;
        }

        var detalle = $"/admin/Visitas/Detalle/{visitaId}";
        Assert.Contains("Añadir un producto", WebUtility.HtmlDecode(await admin.GetStringAsync(detalle)));
        var anadido = await admin.PostAsync($"/admin/Visitas/AgregarProducto/{visitaId}", Formulario(await TokenAsync(admin, detalle),
            ("ProductoId", productoId.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("Cantidad", "2")));
        Assert.Equal(HttpStatusCode.Redirect, anadido.StatusCode);
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Servicios y productos", html);
        Assert.Contains("Cobrar 39,80", html); // 15 + 2 × 12,40

        var lineaId = Regex.Match(html, "/admin/Visitas/QuitarProducto/" + visitaId + "\\?lineaId=(\\d+)").Groups[1].Value;
        var quitado = await admin.PostAsync($"/admin/Visitas/QuitarProducto/{visitaId}?lineaId={lineaId}", Formulario(await TokenAsync(admin, detalle)));
        Assert.Equal(HttpStatusCode.Redirect, quitado.StatusCode);
        Assert.Contains("Cobrar 15,00", WebUtility.HtmlDecode(await admin.GetStringAsync(detalle)));
    }

    [Fact]
    public async Task Una_foto_que_no_es_una_imagen_no_da_de_alta_el_producto()
    {
        using var fabrica = Fabrica();
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var referencia = Referencia();
        var falsa = new ByteArrayContent("<svg onload='alert(1)'/>"u8.ToArray());
        falsa.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var respuesta = await admin.PostAsync("/admin/Productos/Nuevo", await AltaAsync(admin, referencia, stock: 1, (falsa, "falsa.png")));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Sube una foto JPG, PNG o WebP", WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync()));
        await using var db = fixture.Bd.Contexto();
        Assert.False(await db.Productos.AnyAsync(p => p.Referencia == referencia));
    }

    private FabricaWeb Fabrica() => new(fixture.Bd.Cadena, new() { ["FotosProductos:Carpeta"] = _carpeta });

    /// <summary>La base de datos de la clase se comparte entre pruebas: cada producto lleva su propia referencia.</summary>
    private static string Referencia() => "WEB-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static async Task<MultipartFormDataContent> AltaAsync(HttpClient admin, string referencia, int stock, params (HttpContent Contenido, string Nombre)[] fotos)
    {
        var formulario = new MultipartFormDataContent
        {
            { new StringContent(await TokenAsync(admin, "/admin/Productos/Nuevo")), "__RequestVerificationToken" },
            { new StringContent(referencia), "Referencia" },
            { new StringContent("Champú de prueba web"), "Nombre" },
            { new StringContent("Eco Natura"), "Marca" },
            { new StringContent("Champús"), "Categoria" },
            { new StringContent("12,50"), "PrecioVenta" },
            { new StringContent("6"), "PrecioCoste" },
            { new StringContent("1"), "StockMinimo" },
            { new StringContent(stock.ToString(System.Globalization.CultureInfo.InvariantCulture)), "StockInicial" },
        };
        foreach (var (contenido, nombre) in fotos)
            formulario.Add(contenido, "Fotos", nombre);
        return formulario;
    }

    private static (HttpContent, string) Foto()
    {
        using var bitmap = new SKBitmap(900, 600);
        using (var lienzo = new SKCanvas(bitmap))
            lienzo.Clear(SKColors.MediumPurple);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 100);
        var contenido = new ByteArrayContent(datos.ToArray());
        contenido.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return (contenido, "frente.png");
    }

    private async Task<int> ProductoAsync(int stock, string nombre = "Mascarilla de prueba")
    {
        await using var db = fixture.Bd.Contexto();
        var producto = Producto.Crear(new FichaProducto(Referencia(), null, nombre, null, null, null, 12.40m, null, 1), DateTime.Now);
        var alta = producto.Alta(stock, DateTime.Now, null);
        db.Add(producto);
        if (alta is not null)
            db.Add(alta);
        await db.SaveChangesAsync();
        return producto.Id;
    }

    private async Task<int> ClienteAsync()
    {
        await using var db = fixture.Bd.Contexto();
        var cliente = Cliente.Crear("Compra", "Tienda", null, null, Sexo.Mujer, DateTime.Now);
        db.Add(cliente);
        await db.SaveChangesAsync();
        return cliente.Id;
    }

    private async Task<int> StockAsync(int productoId)
    {
        await using var db = fixture.Bd.Contexto();
        return await db.Productos.Where(p => p.Id == productoId).Select(p => p.Stock).SingleAsync();
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
