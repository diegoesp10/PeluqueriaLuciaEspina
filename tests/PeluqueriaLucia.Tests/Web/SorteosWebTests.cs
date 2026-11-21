using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>
/// Sorteos del panel a la web pública (sin datos de participantes), con premios de la peluquería y productos de sorteo, y el
/// coste de compra en la ficha del producto.
/// </summary>
public sealed class SorteosWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    [Fact]
    public async Task Crear_apuntar_anunciar_sortear_y_avisar_sin_que_la_web_enseñe_a_nadie()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        using var publico = fabrica.Cliente();
        await EntrarAsync(admin);
        var marca = Guid.NewGuid().ToString("N")[..6];
        var servicio = await ServicioAsync($"Hidratación {marca}");
        var ana = await ClienteAsync($"Ana{marca}", "+34611000001");
        var bea = await ClienteAsync($"Bea{marca}", "+34611000002");

        var lista = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/Sorteos"));
        Assert.Contains("href=\"/admin/Sorteos\"", lista);
        Assert.Contains("Nuevo sorteo", lista);

        // Sin el título en inglés no se crea
        var sinIngles = await admin.PostAsync("/admin/Sorteos/Nuevo", Formulario(await TokenAsync(admin, "/admin/Sorteos/Nuevo"),
            ("Titulo", $"Sorteo {marca}"), ("FechaSorteo", DateTime.Today.AddDays(5).ToString("yyyy-MM-ddT18:00"))));
        Assert.Equal(HttpStatusCode.OK, sinIngles.StatusCode);
        Assert.Contains("Escribe también el título en inglés", WebUtility.HtmlDecode(await sinIngles.Content.ReadAsStringAsync()));

        // Se crea con sus datos y lleva a añadir los premios
        var creado = await admin.PostAsync("/admin/Sorteos/Nuevo", Formulario(await TokenAsync(admin, "/admin/Sorteos/Nuevo"),
            ("Titulo", $"Sorteo {marca}"), ("TituloEn", $"Giveaway {marca}"), ("FechaSorteo", DateTime.Today.AddDays(5).ToString("yyyy-MM-ddT18:00"))));
        Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
        Assert.EndsWith("#premios", creado.Headers.Location!.OriginalString);
        var detalle = creado.Headers.Location!.OriginalString.Split('#')[0];
        var id = detalle[(detalle.LastIndexOf('/') + 1)..];
        Assert.Contains("Todavía no hay premios", WebUtility.HtmlDecode(await admin.GetStringAsync(detalle)));

        // Premios: un servicio del catálogo y un producto de sorteo creado desde el propio sorteo
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/admin/Sorteos/AgregarPremio/{id}", Formulario(await TokenAsync(admin, detalle),
            ("premio", $"Servicio-{servicio}"), ("cantidad", "1")))).StatusCode);
        var nuevoProducto = $"/admin/ProductosSorteo/Nuevo?sorteo={id}";
        Assert.Contains($"Al crearlo se añade como premio al sorteo «Sorteo {marca}»", WebUtility.HtmlDecode(await admin.GetStringAsync(nuevoProducto)));
        var producto = await admin.PostAsync(nuevoProducto, Formulario(await TokenAsync(admin, nuevoProducto),
            ("Nombre", $"Cena para dos {marca}"), ("NombreEn", $"Dinner for two {marca}"), ("Descripcion", "En Casa Ramón"), ("DescripcionEn", "At Casa Ramón")));
        Assert.Equal(HttpStatusCode.Redirect, producto.StatusCode);
        Assert.Equal($"{detalle}#premios", producto.Headers.Location!.OriginalString);
        var conPremios = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Premios · 2", conPremios);
        Assert.Contains($"Cena para dos {marca}", conPremios);
        Assert.Contains("Producto de sorteo", conPremios);

        foreach (var cliente in new[] { ana, bea })
            Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/admin/Sorteos/AgregarParticipante/{id}", Formulario(await TokenAsync(admin, detalle),
                ("clienteId", cliente.ToString(CultureInfo.InvariantCulture))))).StatusCode);
        var conParticipantes = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Participantes · 2", conParticipantes);
        Assert.Contains($"Ana{marca} Pérez", conParticipantes);

        var anuncio = await admin.PostAsync($"/admin/Sorteos/Anunciar/{id}", Formulario(await TokenAsync(admin, detalle),
            ("TextoAnuncio", $"Participa con tu visita {marca}"), ("TextoAnuncioEn", $"Join with your visit {marca}"),
            ("DesdeAnuncio", DateTime.Today.ToString("yyyy-MM-dd")), ("HastaAnuncio", DateTime.Today.AddDays(5).ToString("yyyy-MM-dd"))));
        Assert.Equal(HttpStatusCode.Redirect, anuncio.StatusCode);

        // La web lo anuncia (aviso flotante, menú y página) en los dos idiomas, sin nombres de clientes
        var portada = WebUtility.HtmlDecode(await publico.GetStringAsync("/"));
        Assert.Contains("data-aviso-sorteo", portada);
        Assert.Contains($"Sorteo {marca}", portada);
        Assert.Contains("href=\"/sorteos\"", portada);
        Assert.Contains("/js/aviso-sorteo.js", portada);
        var pagina = WebUtility.HtmlDecode(await publico.GetStringAsync("/sorteos"));
        Assert.Contains($"Participa con tu visita {marca}", pagina);
        Assert.Contains($"Hidratación {marca}", pagina);
        Assert.Contains($"Cena para dos {marca}", pagina);
        Assert.Contains("En Casa Ramón", pagina);
        Assert.DoesNotContain($"Ana{marca}", pagina);
        Assert.DoesNotContain("data-aviso-sorteo", pagina);
        var ingles = WebUtility.HtmlDecode(await publico.GetStringAsync("/en/sorteos"));
        Assert.Contains("Giveaways", ingles);
        Assert.Contains($"Giveaway {marca}", ingles);
        Assert.Contains($"Join with your visit {marca}", ingles);
        Assert.Contains($"Dinner for two {marca}", ingles);

        // Sortear: la pantalla descubre al ganador con la ruleta la primera vez
        var sorteo = await admin.PostAsync($"/admin/Sorteos/Sortear/{id}", Formulario(await TokenAsync(admin, detalle)));
        Assert.Equal(HttpStatusCode.Redirect, sorteo.StatusCode);
        var revelado = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("¡Tenemos ganador!", revelado);
        Assert.Contains("data-revelar=\"true\"", revelado);
        Assert.Contains("Al azar entre 2 participantes", revelado);
        Assert.Contains("data-revelar=\"false\"", WebUtility.HtmlDecode(await admin.GetStringAsync(detalle)));

        // WhatsApp: se anota el aviso y se abre la conversación con el mensaje
        var whatsApp = await admin.PostAsync($"/admin/Sorteos/AvisarWhatsApp/{id}", Formulario(await TokenAsync(admin, detalle),
            ("textoAviso", "¡Enhorabuena! Has ganado el sorteo.")));
        Assert.Equal(HttpStatusCode.Redirect, whatsApp.StatusCode);
        Assert.Matches(@"^https://wa\.me/3461100000[12]\?text=", whatsApp.Headers.Location!.AbsoluteUri);
        Assert.Contains("Avisado por WhatsApp", WebUtility.HtmlDecode(await admin.GetStringAsync(detalle)));

        // Entregado el premio: queda en el historial del sorteo y en el del producto de sorteo
        Assert.Contains("Premio sin entregar", WebUtility.HtmlDecode(await admin.GetStringAsync(detalle)));
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/admin/Sorteos/MarcarEntregado/{id}", Formulario(await TokenAsync(admin, detalle)))).StatusCode);
        var entregado = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains($"Premio entregado el {DateTime.Today:dd/MM/yyyy}", entregado);
        Assert.DoesNotContain("Repetir el sorteo o elegir otro ganador", entregado);
        var productos = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/ProductosSorteo"));
        Assert.Contains($"Cena para dos {marca}", productos);
        int idProducto;
        await using (var db = fixture.Bd.Contexto())
            idProducto = await db.ProductosSorteo.Where(p => p.Nombre == $"Cena para dos {marca}").Select(p => p.Id).SingleAsync();
        Assert.Contains($"href=\"/admin/ProductosSorteo/Ficha/{idProducto}\"", productos);
        var historialProducto = WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/ProductosSorteo/Ficha/{idProducto}"));
        Assert.Contains($"Sorteo {marca}", historialProducto);
        Assert.Matches($@"ganó Nº \d+ · (Ana|Bea){marca} Pérez", Regex.Replace(historialProducto, "<[^>]+>", ""));

        var realizado = WebUtility.HtmlDecode(await publico.GetStringAsync("/sorteos"));
        Assert.Contains("¡Ya tenemos ganador!", realizado);
        Assert.DoesNotContain($"Ana{marca}", realizado);
        Assert.DoesNotContain($"Bea{marca}", realizado);
        var historial = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/Sorteos?vista=Historial"));
        Assert.Contains($"Sorteo {marca}", historial);
        Assert.Matches($"(Ana|Bea){marca} Pérez", historial);
        Assert.Contains($"Hidratación {marca} y Cena para dos {marca}", historial);
        Assert.Contains("Premio entregado", historial);
    }

    [Fact]
    public async Task La_ficha_del_producto_enseña_y_anota_su_coste_de_compra()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var id = await ProductoAsync();
        var ficha = $"/admin/Productos/Ficha/{id}";
        Assert.Contains("Coste de compra", WebUtility.HtmlDecode(await admin.GetStringAsync(ficha)));

        var anotado = await admin.PostAsync($"/admin/Productos/RegistrarCoste/{id}", Formulario(await TokenAsync(admin, ficha),
            ("Coste", "12,40"), ("Desde", DateTime.Today.ToString("yyyy-MM-dd")), ("NotaCoste", "Proveedor nuevo")));
        var futuro = await admin.PostAsync($"/admin/Productos/RegistrarCoste/{id}", Formulario(await TokenAsync(admin, ficha),
            ("Coste", "1"), ("Desde", DateTime.Today.AddDays(3).ToString("yyyy-MM-dd"))));

        Assert.Equal(HttpStatusCode.Redirect, anotado.StatusCode);
        Assert.EndsWith("#costes", anotado.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, futuro.StatusCode);
        Assert.Contains("La fecha del coste no puede ser futura.", WebUtility.HtmlDecode(await futuro.Content.ReadAsStringAsync()));
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync(ficha));
        Assert.Contains("Proveedor nuevo", html);
        Assert.Contains("12,40", html);
        Assert.Contains("Actual", html);
    }

    [Fact]
    public async Task Los_numeros_se_meten_a_mano_se_cambian_y_se_elige_al_ganador_por_su_numero()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var marca = Guid.NewGuid().ToString("N")[..6];
        var servicio = await ServicioAsync($"Mechas {marca}");
        var ana = await ClienteAsync($"Ana{marca}", "+34611000003");
        var creado = await admin.PostAsync("/admin/Sorteos/Nuevo", Formulario(await TokenAsync(admin, "/admin/Sorteos/Nuevo"),
            ("Titulo", $"Sorteo {marca}"), ("TituloEn", $"Giveaway {marca}"), ("FechaSorteo", DateTime.Today.AddDays(5).ToString("yyyy-MM-ddT18:00"))));
        var detalle = creado.Headers.Location!.OriginalString.Split('#')[0];
        var id = detalle[(detalle.LastIndexOf('/') + 1)..];
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/admin/Sorteos/AgregarPremio/{id}", Formulario(await TokenAsync(admin, detalle),
            ("premio", $"Servicio-{servicio}"), ("cantidad", "1")))).StatusCode);
        var vacio = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Meter números a mano", vacio);
        Assert.Contains("name=\"numeros\"", vacio);

        // Ana con el número que ya tenía en el sorteo de fuera
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/admin/Sorteos/AgregarParticipante/{id}", Formulario(await TokenAsync(admin, detalle),
            ("clienteId", ana.ToString(CultureInfo.InvariantCulture)), ("numero", "10")))).StatusCode);

        // Un texto mal escrito no se pierde: vuelve con el error y con lo escrito
        var malo = await admin.PostAsync($"/admin/Sorteos/AgregarNumeros/{id}", Formulario(await TokenAsync(admin, detalle), ("numeros", "1-3\nMaría 12")));
        Assert.Equal(HttpStatusCode.OK, malo.StatusCode);
        var conError = WebUtility.HtmlDecode(await malo.Content.ReadAsStringAsync());
        Assert.Contains("«María 12» no empieza por un número", conError);
        Assert.Contains("<p class=\"field-validation-error\" id=\"numeros-error\">«María 12» no empieza por un número", conError);
        Assert.Contains("aria-invalid=\"true\"", conError);
        Assert.Matches(@"1-3\r?\nMaría 12</textarea>", conError);

        var bien = await admin.PostAsync($"/admin/Sorteos/AgregarNumeros/{id}", Formulario(await TokenAsync(admin, detalle), ("numeros", "1-3\n7 María López\n10")));
        Assert.Equal(HttpStatusCode.Redirect, bien.StatusCode);
        var conNumeros = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Se han añadido 4 números. Ya estaba el 10: se han dejado como estaban.", conNumeros);
        Assert.Contains("Participantes · 5", conNumeros);
        Assert.Contains("Número metido a mano, sin ficha", conNumeros);
        Assert.Contains($"Nº 10 · Ana{marca} Pérez", conNumeros);

        // Cambiar el 7 por el 70 (con el nombre completo) y quitar el 2
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/admin/Sorteos/CambiarNumero/{id}", Formulario(await TokenAsync(admin, detalle),
            ("numero", "7"), ("nuevo", "70"), ("nombre", "María López Gil")))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/admin/Sorteos/QuitarParticipante/{id}", Formulario(await TokenAsync(admin, detalle),
            ("numero", "2")))).StatusCode);
        var cambiado = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Participantes · 4", cambiado);
        Assert.Contains("<option value=\"70\">Nº 70 · María López Gil</option>", cambiado);
        Assert.DoesNotContain("<option value=\"2\">", cambiado);

        // Ganó el 70 en el sorteo de fuera: se elige por su número y se avisa por cuenta del salón
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/admin/Sorteos/ElegirGanador/{id}", Formulario(await TokenAsync(admin, detalle),
            ("numero", "70"), ("motivo", "Ganó en Instagram")))).StatusCode);
        var ganador = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Ganador elegido. Ya puedes avisarle.", ganador);
        Assert.Contains("<h2>Nº 70 · María López Gil</h2>", ganador);
        Assert.Contains("avísale por tu cuenta", ganador);
        Assert.Contains("¡Enhorabuena, María López Gil! Has ganado", ganador);
        Assert.Contains("Sin ficha: avísale por tu cuenta", ganador);
    }

    [Fact]
    public async Task El_tablero_enseña_los_numeros_guarda_el_talonario_y_apunta_uno_libre()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var marca = Guid.NewGuid().ToString("N")[..6];
        var (id, detalle) = await SorteoConPremioAsync(admin, marca);
        var ana = await ClienteAsync($"Ana{marca}", "+34611000004");
        var tablero = $"/admin/Sorteos/Tablero/{id}";

        // Vacío: invita a decir hasta dónde llega el talonario; el detalle enlaza al tablero
        var vacio = WebUtility.HtmlDecode(await admin.GetStringAsync(tablero));
        Assert.Contains("Tablero del sorteo", vacio);
        Assert.Contains("El tablero está vacío", vacio);
        Assert.Contains($"href=\"{tablero}\"", WebUtility.HtmlDecode(await admin.GetStringAsync(detalle)));

        // Ana con el 3: casillas del 1 al 3, con su nombre corto en la suya
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/admin/Sorteos/AgregarParticipante/{id}", Formulario(await TokenAsync(admin, detalle),
            ("clienteId", ana.ToString(CultureInfo.InvariantCulture)), ("numero", "3")))).StatusCode);
        var conAna = WebUtility.HtmlDecode(await admin.GetStringAsync(tablero));
        Assert.Contains("id=\"n-3\"", conAna);
        Assert.DoesNotContain("id=\"n-4\"", conAna);
        Assert.Contains($"Ana{marca} P.", conAna);
        Assert.Contains($"aria-label=\"Número 3: ocupado, Ana{marca} Pérez\"", conAna);
        Assert.Contains("aria-label=\"Número 1: libre\"", conAna);

        // Talonario del 1 al 20: aparecen los libres hasta el 20
        var talonario = await admin.PostAsync($"/admin/Sorteos/Talonario/{id}", Formulario(await TokenAsync(admin, tablero), ("hasta", "20")));
        Assert.Equal(HttpStatusCode.Redirect, talonario.StatusCode);
        Assert.Equal(tablero, talonario.Headers.Location!.OriginalString);
        var conTalonario = WebUtility.HtmlDecode(await admin.GetStringAsync(tablero));
        Assert.Contains("Talonario guardado: del 1 al 20.", conTalonario);
        Assert.Contains("id=\"n-20\"", conTalonario);
        Assert.Contains("números en el talonario", conTalonario);
        Assert.Contains("5 % del talonario ocupado", conTalonario);

        // Apuntar el 12 desde el tablero, con nombre; el mismo número otra vez no se pisa
        var apuntado = await admin.PostAsync($"/admin/Sorteos/ApuntarNumero/{id}", Formulario(await TokenAsync(admin, tablero), ("numero", "12"), ("nombre", "Lola Martín")));
        Assert.Equal(HttpStatusCode.Redirect, apuntado.StatusCode);
        Assert.Equal($"{tablero}#n-12", apuntado.Headers.Location!.OriginalString);
        var conLola = WebUtility.HtmlDecode(await admin.GetStringAsync(tablero));
        Assert.Contains("Número 12 apuntado para Lola Martín.", conLola);
        Assert.Contains("Lola M.", conLola);
        await admin.PostAsync($"/admin/Sorteos/ApuntarNumero/{id}", Formulario(await TokenAsync(admin, tablero), ("numero", "12"), ("nombre", "Otra persona")));
        Assert.Contains("El número 12 ya lo tiene alguien.", WebUtility.HtmlDecode(await admin.GetStringAsync(tablero)));
        Assert.Contains("Participantes · 2", WebUtility.HtmlDecode(await admin.GetStringAsync(detalle)));
    }

    [Fact]
    public async Task El_anuncio_enseña_su_estado_se_cambia_y_se_deja_de_mostrar_en_la_web()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        using var publico = fabrica.Cliente();
        await EntrarAsync(admin);
        var marca = Guid.NewGuid().ToString("N")[..6];
        var (id, detalle) = await SorteoConPremioAsync(admin, marca);
        var hoy = DateTime.Today;

        // Anunciado desde hoy: en lugar del formulario, el estado y los dos botones (dejar de mostrar y finalizar)
        Assert.Contains($"action=\"/admin/Sorteos/Anunciar/{id}\"", WebUtility.HtmlDecode(await admin.GetStringAsync(detalle)));
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/admin/Sorteos/Anunciar/{id}", Formulario(await TokenAsync(admin, detalle),
            ("TextoAnuncio", $"Participa con tu visita {marca}"), ("TextoAnuncioEn", $"Join with your visit {marca}"),
            ("DesdeAnuncio", hoy.ToString("yyyy-MM-dd")), ("HastaAnuncio", hoy.AddDays(5).ToString("yyyy-MM-dd"))))).StatusCode);
        var enLaWeb = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("En la web ahora", enLaWeb);
        Assert.Contains($"Se ve hasta el {hoy.AddDays(5):dd/MM/yyyy}", enLaWeb);
        Assert.Contains("Dejar de mostrar en la web", enLaWeb);
        Assert.Contains("Finalizar el sorteo", enLaWeb);
        Assert.Contains("data-confirmar-titulo=\"¿Dejar de mostrarlo en la web?\"", enLaWeb);
        Assert.DoesNotContain($"action=\"/admin/Sorteos/Anunciar/{id}\"", enLaWeb);
        var anuncioId = Regex.Match(enLaWeb, $@"/admin/Sorteos/TerminarAnuncio/{id}\?anuncioId=(\d+)").Groups[1].Value;
        Assert.NotEmpty(anuncioId);

        // Cambiar el texto y las fechas: la web enseña el nuevo; con las fechas al revés no se cambia
        var cambio = $"/admin/Sorteos/CambiarAnuncio/{id}?anuncioId={anuncioId}";
        var cambiado = await admin.PostAsync(cambio, Formulario(await TokenAsync(admin, detalle),
            ("TextoAnuncio", $"Nuevo texto {marca}"), ("TextoAnuncioEn", $"New text {marca}"),
            ("DesdeAnuncio", hoy.ToString("yyyy-MM-dd")), ("HastaAnuncio", hoy.AddDays(9).ToString("yyyy-MM-dd"))));
        Assert.Equal(HttpStatusCode.Redirect, cambiado.StatusCode);
        Assert.EndsWith("#anuncios", cambiado.Headers.Location!.OriginalString);
        var conCambio = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Anuncio cambiado. La web ya enseña el texto y las fechas nuevos.", conCambio);
        Assert.Contains($"Se ve hasta el {hoy.AddDays(9):dd/MM/yyyy}", conCambio);
        Assert.Contains($"Nuevo texto {marca}", WebUtility.HtmlDecode(await publico.GetStringAsync("/sorteos")));
        var alReves = await admin.PostAsync(cambio, Formulario(await TokenAsync(admin, detalle),
            ("TextoAnuncio", $"Otro texto {marca}"), ("TextoAnuncioEn", $"Other text {marca}"),
            ("DesdeAnuncio", hoy.AddDays(5).ToString("yyyy-MM-dd")), ("HastaAnuncio", hoy.ToString("yyyy-MM-dd"))));
        Assert.Equal(HttpStatusCode.OK, alReves.StatusCode);
        Assert.Contains("La fecha de fin tiene que ser posterior a la de inicio.", WebUtility.HtmlDecode(await alReves.Content.ReadAsStringAsync()));
        Assert.DoesNotContain($"Otro texto {marca}", WebUtility.HtmlDecode(await publico.GetStringAsync("/sorteos")));

        // Dejar de mostrarlo: la web ya no lo enseña y vuelve el formulario para anunciarlo otra vez
        var terminado = await admin.PostAsync($"/admin/Sorteos/TerminarAnuncio/{id}?anuncioId={anuncioId}", Formulario(await TokenAsync(admin, detalle)));
        Assert.Equal(HttpStatusCode.Redirect, terminado.StatusCode);
        var sinAnuncio = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Listo: la web ya no enseña este sorteo.", sinAnuncio);
        Assert.DoesNotContain("En la web ahora", sinAnuncio);
        Assert.Contains($"action=\"/admin/Sorteos/Anunciar/{id}\"", sinAnuncio);
        Assert.DoesNotContain($"Nuevo texto {marca}", WebUtility.HtmlDecode(await publico.GetStringAsync("/sorteos")));
    }

    /// <summary>Un sorteo abierto con un servicio de premio, para las pruebas del tablero y del anuncio.</summary>
    private async Task<(string Id, string Detalle)> SorteoConPremioAsync(HttpClient admin, string marca)
    {
        var servicio = await ServicioAsync($"Corte {marca}");
        var creado = await admin.PostAsync("/admin/Sorteos/Nuevo", Formulario(await TokenAsync(admin, "/admin/Sorteos/Nuevo"),
            ("Titulo", $"Sorteo {marca}"), ("TituloEn", $"Giveaway {marca}"), ("FechaSorteo", DateTime.Today.AddDays(5).ToString("yyyy-MM-ddT18:00"))));
        Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
        var detalle = creado.Headers.Location!.OriginalString.Split('#')[0];
        var id = detalle[(detalle.LastIndexOf('/') + 1)..];
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/admin/Sorteos/AgregarPremio/{id}", Formulario(await TokenAsync(admin, detalle),
            ("premio", $"Servicio-{servicio}"), ("cantidad", "1")))).StatusCode);
        return (id, detalle);
    }

    private async Task<int> ServicioAsync(string nombre)
    {
        await using var db = fixture.Bd.Contexto();
        var servicio = Servicio.Crear(nombre, 25m, Sexo.Mujer, DateTime.Now, nombre + " (en)");
        db.Add(servicio);
        await db.SaveChangesAsync();
        return servicio.Id;
    }

    private async Task<int> ClienteAsync(string nombre, string telefono)
    {
        await using var db = fixture.Bd.Contexto();
        var cliente = Cliente.Crear(nombre, "Pérez", null, telefono, Sexo.Mujer, DateTime.Now);
        db.Add(cliente);
        await db.SaveChangesAsync();
        return cliente.Id;
    }

    private async Task<int> ProductoAsync()
    {
        await using var db = fixture.Bd.Contexto();
        var categoria = CategoriaProducto.Crear("Costes " + Guid.NewGuid().ToString("N")[..8], DateTime.Now);
        var producto = Producto.Crear(new FichaProducto("COS-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), null, "Mascarilla de coste", null, null, 20m, 10m, 1), categoria, DateTime.Now);
        db.Add(producto);
        await db.SaveChangesAsync();
        return producto.Id;
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
