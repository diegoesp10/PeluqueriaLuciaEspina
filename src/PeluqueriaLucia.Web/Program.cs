using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application;
using PeluqueriaLucia.Infrastructure;
using PeluqueriaLucia.Infrastructure.Almacenamiento;
using PeluqueriaLucia.Infrastructure.Persistencia;
using PeluqueriaLucia.Web.Infraestructura;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAplicacion();
builder.Services.AddInfraestructura(builder.Configuration);
builder.Services.PostConfigure<OpcionesImagenes>(o =>
{
    o.Carpeta = string.IsNullOrWhiteSpace(o.Carpeta)
        ? Path.Combine(builder.Environment.WebRootPath, "imagenes")
        : Path.GetFullPath(o.Carpeta, builder.Environment.ContentRootPath);
});
builder.Services.Configure<DatosNegocio>(builder.Configuration.GetSection(DatosNegocio.Seccion));

builder.Services
    .AddControllersWithViews(o =>
    {
        // Acepta «12,50» y «12.50» por igual, sin depender del idioma de Windows
        o.ModelBinderProviders.Insert(0, new DecimalFlexibleModelBinderProvider());
        o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    });

var app = builder.Build();

var espanol = new CultureInfo("es-ES");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(espanol),
    SupportedCultures = [espanol],
    SupportedUICultures = [espanol],
});

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error");
app.UseStatusCodePagesWithReExecute("/error/{0}");

app.UseStaticFiles();

// Si las fotos se guardan fuera de wwwroot (opción Imagenes:Carpeta), se sirven igualmente en /imagenes
var imagenes = app.Services.GetRequiredService<IOptions<OpcionesImagenes>>().Value;
var carpetaPorDefecto = Path.Combine(app.Environment.WebRootPath, "imagenes");
if (!string.Equals(Path.GetFullPath(imagenes.Carpeta), Path.GetFullPath(carpetaPorDefecto), StringComparison.OrdinalIgnoreCase))
{
    Directory.CreateDirectory(imagenes.Carpeta);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(imagenes.Carpeta),
        RequestPath = imagenes.RutaPublica,
    });
}

app.UseRouting();
app.MapControllerRoute(name: "default", pattern: "{controller=Visitas}/{action=Index}/{id?}");

if (app.Configuration.GetValue<bool>("BaseDeDatos:AplicarMigracionesAlArrancar"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PeluqueriaDbContext>().Database.MigrateAsync();
}

await app.RunAsync();

/// <summary>Punto de entrada; público para las pruebas de integración.</summary>
public partial class Program;
