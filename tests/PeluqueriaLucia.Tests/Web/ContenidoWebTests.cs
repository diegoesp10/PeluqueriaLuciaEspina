using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Application.Publico;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Tests.Web;

public sealed class ContenidoWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    private static async Task<string> Token(HttpClient cliente, string ruta)
    { var r = await cliente.GetAsync(ruta); Assert.Equal(HttpStatusCode.OK,r.StatusCode); return WebUtility.HtmlDecode(Regex.Match(await r.Content.ReadAsStringAsync(),"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value); }
    private async Task Entrar(HttpClient cliente)
    {
        var codigo = await fixture.RecuperacionAsync("prueba");
        await cliente.PostAsync("/admin/acceso", new FormUrlEncodedContent(new Dictionary<string,string> { ["Usuario"]="prueba",["Contrasena"]=SeguridadWebFixture.ClavePrueba,["__RequestVerificationToken"]=await Token(cliente,"/admin/acceso") }));
        var r = await cliente.PostAsync("/admin/acceso/verificar", new FormUrlEncodedContent(new Dictionary<string,string> { ["Codigo"]=codigo,["__RequestVerificationToken"]=await Token(cliente,"/admin/acceso/verificar") }));
        Assert.Equal(HttpStatusCode.Redirect,r.StatusCode);
    }
    private async Task<int> Visita(bool cobrada, DateTime? fecha = null)
    {
        await using var db = fixture.Bd.Contexto();
        var cliente = Cliente.Crear("Nombre privado","Apellido privado", "persona@example.test", "600123456", Sexo.Mujer,DateTime.UtcNow);
        var servicio = Servicio.Crear("Servicio interno privado",18.50m,Sexo.Mujer,DateTime.UtcNow);
        db.Add(cliente); db.Add(servicio); await db.SaveChangesAsync();
        var visita = Domain.Visitas.Visita.Registrar(cliente,fecha ?? DateTime.Now,FormaPago.Efectivo,[servicio]);
        if(cobrada) visita.Cobrar(FormaPago.Efectivo,null,null);
        db.Add(visita); await db.SaveChangesAsync(); return visita.Id;
    }

    [Fact]
    public async Task Buscar_por_anio_incluye_enero_bisiesto_y_diciembre_y_exporta_solo_ese_anio()
    {
        var previo = await Visita(true,new DateTime(2023,12,31,23,59,59));
        int[] incluidos = [await Visita(true,new DateTime(2024,1,1)),await Visita(true,new DateTime(2024,2,29,12,0,0)),await Visita(true,new DateTime(2024,12,31,23,59,59))];
        var siguiente = await Visita(true,new DateTime(2025,1,1));
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena); using var admin = fabrica.Cliente(); await Entrar(admin);
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/Visitas?periodo=Anio&fecha=2024-02-29"));
        Assert.Contains("Año 2024",html); Assert.Contains("55,50",html); Assert.Contains("Elegir año",html);
        foreach(var id in incluidos) Assert.Contains($"href=\"/admin/Visitas/Detalle/{id}\"",html);
        foreach(var id in new[]{previo,siguiente}) Assert.DoesNotContain($"href=\"/admin/Visitas/Detalle/{id}\"",html);
        var exportado = await admin.GetAsync("/admin/Visitas/Exportar?periodo=Anio&fecha=2024-12-31"); Assert.Equal(HttpStatusCode.OK,exportado.StatusCode);
        using var memoria = new MemoryStream(await exportado.Content.ReadAsByteArrayAsync()); using var libro = new ClosedXML.Excel.XLWorkbook(memoria);
        var filas = libro.Worksheet("Visitas").Table("Visitas").DataRange.Rows().ToArray(); Assert.Equal(3,filas.Length);
        Assert.All(filas,fila => Assert.Equal(2024,fila.Cell(1).GetDateTime().Year));
        Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync("/admin/Visitas?periodo=Anio&fecha=9999-12-31")).StatusCode);
    }

    [Fact]
    public async Task Editar_catalogo_actualiza_la_web_con_el_mismo_servicio_y_precio()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena); using var admin = fabrica.Cliente(); using var publico = fabrica.Cliente();
        Assert.Equal(HttpStatusCode.Redirect,(await publico.GetAsync("/admin/Tratamientos")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await publico.GetAsync("/admin/Resenas")).StatusCode);
        await Entrar(admin);
        await using var db = fixture.Bd.Contexto();
        var tratamiento = await db.Servicios.FirstAsync(x => x.Nombre=="Oxigenación");
        var internos = await db.Servicios.CountAsync();
        var ruta = $"/admin/Catalogo/Editar/{tratamiento.Id}";
        Assert.Equal(ruta,(await admin.GetAsync($"/admin/Tratamientos/Editar/{tratamiento.Id}")).Headers.Location!.ToString());
        var campos = new Dictionary<string,string> { ["Nombre"]="Oxigenación actualizada",["CategoriaPublicaId"]=tratamiento.CategoriaPublicaId!.Value.ToString(),["Sexo"]="Mujer",["Precio"]="49,90",["TipoPrecioPublico"]="Desde",["DuracionMinutos"]="40",["OrdenPublico"]="10",["Publicado"]="true" };
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsync(ruta,new FormUrlEncodedContent(campos))).StatusCode);
        campos["__RequestVerificationToken"] = await Token(admin,ruta);
        Assert.Equal(HttpStatusCode.Redirect,(await admin.PostAsync(ruta,new FormUrlEncodedContent(campos))).StatusCode);
        using var otraFabrica = new FabricaWeb(fixture.Bd.Cadena); using var otraCarga = otraFabrica.Cliente();
        var html = WebUtility.HtmlDecode(await otraCarga.GetStringAsync("/servicios"));
        Assert.Contains("Oxigenación actualizada",html); Assert.Contains("49,90",html); Assert.Contains("id=\"tratamientos\"",html);
        db.ChangeTracker.Clear();
        Assert.Equal(49.90m,(await db.Servicios.FindAsync(tratamiento.Id))!.Precio);
        Assert.Equal(internos,await db.Servicios.CountAsync());
        campos.Remove("Publicado"); campos["__RequestVerificationToken"] = await Token(admin,ruta);
        await admin.PostAsync(ruta,new FormUrlEncodedContent(campos));
        Assert.DoesNotContain("Oxigenación actualizada",await publico.GetStringAsync("/api/tratamientos"));
    }

    [Fact]
    public async Task Publicar_un_servicio_interno_reutiliza_la_ficha_y_retirarlo_conserva_el_catalogo()
    {
        using var fabrica=new FabricaWeb(fixture.Bd.Cadena);using var admin=fabrica.Cliente();using var publico=fabrica.Cliente();await Entrar(admin);
        var datos=new Dictionary<string,string> { ["Nombre"]="Extra solo interno",["NombreEn"]="Internal extra",["Precio"]="9,50",["Sexo"]="Otro",["DuracionMinutos"]="30",["__RequestVerificationToken"]=await Token(admin,"/admin/Catalogo/Nuevo") };
        Assert.Equal(HttpStatusCode.Redirect,(await admin.PostAsync("/admin/Catalogo/Nuevo",new FormUrlEncodedContent(datos))).StatusCode);
        await using var db=fixture.Bd.Contexto();var servicio=await db.Servicios.SingleAsync(x=>x.Nombre==datos["Nombre"]);var total=await db.Servicios.CountAsync();
        Assert.DoesNotContain(datos["Nombre"],await publico.GetStringAsync("/servicios"));
        var campos=new Dictionary<string,string> { ["ServicioId"]=servicio.Id.ToString(),["CategoriaId"]=(await db.CategoriasPublicas.OrderBy(c=>c.Id).FirstAsync()).Id.ToString(),["TipoPrecio"]="Fijo",["DuracionMinutos"]="30",["Publicado"]="true",["__RequestVerificationToken"]=await Token(admin,"/admin/Tratamientos/Nuevo") };
        Assert.Equal(HttpStatusCode.Redirect,(await admin.PostAsync("/admin/Tratamientos/Nuevo",new FormUrlEncodedContent(campos))).StatusCode);
        Assert.Equal(total,await db.Servicios.CountAsync());Assert.Contains(datos["Nombre"],await publico.GetStringAsync("/servicios"));
        Assert.Contains(datos["Nombre"],await admin.GetStringAsync("/admin/Visitas/Nueva"));
        Assert.Equal(HttpStatusCode.Redirect,(await admin.PostAsync($"/admin/Tratamientos/Eliminar/{servicio.Id}",new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"]=await Token(admin,"/admin/Tratamientos") }))).StatusCode);
        Assert.DoesNotContain(datos["Nombre"],await publico.GetStringAsync("/servicios"));db.ChangeTracker.Clear();Assert.True((await db.Servicios.FindAsync(servicio.Id))!.Activo);
    }

    [Fact]
    public async Task Servicio_consultar_exige_importe_al_registrar_y_conserva_el_precio_de_la_visita()
    {
        using var fabrica=new FabricaWeb(fixture.Bd.Cadena);using var admin=fabrica.Cliente();await Entrar(admin);
        var datos=new Dictionary<string,string> { ["Nombre"]="Peinado a medida prueba",["NombreEn"]="Custom styling test",["Precio"]="0",["Sexo"]="Mujer",["Publicado"]="true",["TipoPrecioPublico"]="Consultar",["CategoriaPublicaId"]="1",["DuracionMinutos"]="60",["__RequestVerificationToken"]=await Token(admin,"/admin/Catalogo/Nuevo") };
        Assert.Equal(HttpStatusCode.Redirect,(await admin.PostAsync("/admin/Catalogo/Nuevo",new FormUrlEncodedContent(datos))).StatusCode);
        var anterior=await Visita(true);await using var db=fixture.Bd.Contexto();var cliente=(await db.Visitas.FindAsync(anterior))!.ClienteId;
        var servicio=await db.Servicios.SingleAsync(x=>x.Nombre==datos["Nombre"]);
        var registro=new Dictionary<string,string> { ["ClienteId"]=cliente!.Value.ToString(),["Fecha"]=DateTime.Now.ToString("yyyy-MM-ddTHH:mm"),["Servicios"]=servicio.Id.ToString(),["FormaPago"]="Efectivo",["__RequestVerificationToken"]=await Token(admin,"/admin/Visitas/Nueva") };
        var sinPrecio=await admin.PostAsync("/admin/Visitas/Nueva",new FormUrlEncodedContent(registro));Assert.Equal(HttpStatusCode.OK,sinPrecio.StatusCode);Assert.Contains("Indica un precio",await sinPrecio.Content.ReadAsStringAsync());
        registro[$"Precios[{servicio.Id}]"]="67,50";
        Assert.Equal(HttpStatusCode.Redirect,(await admin.PostAsync("/admin/Visitas/Nueva",new FormUrlEncodedContent(registro))).StatusCode);
        var linea=await db.Set<LineaVisita>().SingleAsync(x=>x.ServicioId==servicio.Id);Assert.Equal(67.50m,linea.Precio);
        servicio.Actualizar(servicio.Nombre,80,servicio.Sexo);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        Assert.Equal(67.50m,(await db.Set<LineaVisita>().FindAsync(linea.Id))!.Precio);
    }

    [Fact]
    public async Task Ticket_importado_sin_cliente_muestra_el_iva_historico()
    {
        var id = await Visita(true);
        await fixture.Bd.EjecutarAsync($"UPDATE ServiciosClientes SET ClienteId=NULL,PorcentajeIva=0 WHERE Id={id};");
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await Entrar(admin);
        var detalle = WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Visitas/Detalle/{id}"));
        Assert.Contains("Sin cliente", detalle);
        var ticket = WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Visitas/Ticket/{id}"));
        Assert.Contains("<td>0 %</td>", ticket);
        Assert.DoesNotContain("<td>21 %</td>", ticket);
        Assert.Contains("18,50", ticket);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/admin/Visitas?periodo=Todo")).StatusCode);
    }

    [Fact]
    public async Task Ticket_pagado_permiso_unico_y_moderacion_no_revelan_datos_de_la_visita()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena); using var cliente = fabrica.Cliente();
        var visita = await Visita(true); var pendiente = await Visita(false);
        string codigo;
        await using(var scope = fabrica.Services.CreateAsyncScope()) {
            var servicio = scope.ServiceProvider.GetRequiredService<ResenasService>();
            Assert.Null(await servicio.CodigoTicketAsync(pendiente));
            codigo = (await servicio.CodigoTicketAsync(visita))!;
            Assert.Equal(codigo,await servicio.CodigoTicketAsync(visita));
        }
        var campos = new Dictionary<string,string> { ["Codigo"]=codigo,["Alias"]="Mi experiencia",["Comentario"]="Un cuidado muy agradable en el salón.",["Estrellas"]="4",["Consentimiento"]="true" };
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.PostAsync("/opiniones",new FormUrlEncodedContent(campos))).StatusCode);
        campos["__RequestVerificationToken"] = await Token(cliente,"/opiniones");
        Assert.Equal(HttpStatusCode.Redirect,(await cliente.PostAsync("/opiniones",new FormUrlEncodedContent(campos))).StatusCode);
        var html = await cliente.GetStringAsync("/opiniones");
        Assert.DoesNotContain("Un cuidado muy agradable",html);
        Assert.DoesNotContain("Nombre privado",html); Assert.DoesNotContain("600123456",html); Assert.DoesNotContain("persona@example.test",html);
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.PostAsync("/opiniones",new FormUrlEncodedContent(campos))).StatusCode);
        await using var db = fixture.Bd.Contexto(); var resena = await db.Resenas.SingleAsync(x => x.Comentario==campos["Comentario"]);
        Assert.Equal(HttpStatusCode.NotFound,(await cliente.GetAsync($"/opiniones/foto/{resena.Id}")).StatusCode);
        using var admin = fabrica.Cliente(); await Entrar(admin);
        Assert.Equal(HttpStatusCode.Redirect,(await admin.PostAsync($"/admin/Resenas/Moderar/{resena.Id}",new FormUrlEncodedContent(new Dictionary<string,string> { ["Estado"]="Publicada",["Respuesta"]="Gracias por venir.",["RespuestaEn"]="Thanks for coming.",["__RequestVerificationToken"]=await Token(admin,"/admin/Resenas") }))).StatusCode);
        html = WebUtility.HtmlDecode(await cliente.GetStringAsync("/opiniones"));
        Assert.Contains(campos["Comentario"],html); Assert.Contains("Gracias por venir.",html);
        Assert.DoesNotContain("Nombre privado",html); Assert.DoesNotContain("Servicio interno privado",html); Assert.DoesNotContain(codigo,html);
        await db.Resenas.Where(x => x.Id==resena.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado,EstadoResena.Rechazada));
        Assert.DoesNotContain(campos["Comentario"],await cliente.GetStringAsync("/opiniones"));
    }

    [Fact]
    public async Task Foto_pendiente_privada_publicada_visible_retirada_inaccesible_y_textos_no_ejecutables()
    {
        var carpeta = Path.Combine(Path.GetTempPath(),"LuciaResenasPrueba_"+Guid.NewGuid().ToString("N"));
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena,new() { ["FotosResenas:Carpeta"]=carpeta });
        var id = await Visita(true);
        async Task<string?> CodigoTicket() { await using var scope=fabrica.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<ResenasService>().CodigoTicketAsync(id); }
        var codigos=await Task.WhenAll(CodigoTicket(),CodigoTicket()); Assert.Equal(codigos[0],codigos[1]);
        using var bitmap = new SkiaSharp.SKBitmap(64,64); bitmap.Erase(SkiaSharp.SKColors.Olive);
        using var imagen=SkiaSharp.SKImage.FromBitmap(bitmap); using var png=imagen.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);
        using var publico=fabrica.Cliente();
        using var formulario=new MultipartFormDataContent();
        foreach(var campo in new Dictionary<string,string> { ["Codigo"]=codigos[0]!, ["Alias"]="Foto prueba",["Comentario"]="Un corte bonito <script>alert('XSS')</script>",["Estrellas"]="4",["Consentimiento"]="true",["__RequestVerificationToken"]=await Token(publico,"/opiniones") }) formulario.Add(new StringContent(campo.Value),campo.Key);
        formulario.Add(new ByteArrayContent(png.ToArray()),"Foto","peinado.png");
        Assert.Equal(HttpStatusCode.Redirect,(await publico.PostAsync("/opiniones",formulario)).StatusCode);
        await using var db=fixture.Bd.Contexto();var resena=await db.Resenas.SingleAsync(x=>x.Alias=="Foto prueba");
        var ruta=$"/opiniones/foto/{resena.Id}";
        try {
            Assert.Equal(HttpStatusCode.NotFound,(await publico.GetAsync(ruta)).StatusCode);
            using var admin=fabrica.Cliente();await Entrar(admin);
            Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync($"/admin/Resenas/Foto/{resena.Id}")).StatusCode);
            await db.Resenas.Where(x=>x.Id==resena.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Estado,EstadoResena.Publicada));
            var respuesta=await publico.GetAsync(ruta);Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);Assert.Equal("image/jpeg",respuesta.Content.Headers.ContentType!.MediaType);
            var html=await publico.GetStringAsync("/opiniones");Assert.DoesNotContain("<script>alert",html);Assert.Contains("&lt;script&gt;",html);
            await db.Resenas.Where(x=>x.Id==resena.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Estado,EstadoResena.Rechazada));
            Assert.Equal(HttpStatusCode.NotFound,(await publico.GetAsync(ruta)).StatusCode);
        } finally { File.Delete(Path.Combine(carpeta,resena.Foto!));Directory.Delete(carpeta); }
    }

    [Fact]
    public async Task Codigo_inventado_consentimiento_ausente_y_textos_invalidos_no_crean_resenas()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena); using var cliente = fabrica.Cliente();
        foreach(var consentimiento in new[] {"true","false"}) {
            var r = await cliente.PostAsync("/opiniones",new FormUrlEncodedContent(new Dictionary<string,string> { ["Codigo"]=new string('A',32),["Alias"]="Visitante",["Comentario"]="Intento sin un ticket real.",["Estrellas"]="5",["Consentimiento"]=consentimiento,["__RequestVerificationToken"]=await Token(cliente,"/opiniones") }));
            Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);
        }
        await using var db = fixture.Bd.Contexto(); Assert.False(await db.Resenas.AnyAsync(x => x.Comentario=="Intento sin un ticket real."));
    }
}
