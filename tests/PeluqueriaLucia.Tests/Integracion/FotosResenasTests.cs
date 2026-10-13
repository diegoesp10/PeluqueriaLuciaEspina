using Microsoft.Extensions.Options;
using PeluqueriaLucia.Infrastructure.Almacenamiento;
using SkiaSharp;

namespace PeluqueriaLucia.Tests.Integracion;

public sealed class FotosResenasTests
{
    [Fact]
    public async Task Las_fotos_se_decodifican_redimensionan_y_reescriben_sin_conservar_el_fichero_original()
    {
        var carpeta = Path.GetFullPath(Path.Combine(".artifacts", "fotos-prueba-" + Guid.NewGuid().ToString("N")));
        var servicio = new FotosResenas(Options.Create(new OpcionesFotosResenas { Carpeta = carpeta }));
        using var bitmap = new SKBitmap(2000,1000); using(var canvas=new SKCanvas(bitmap)) canvas.Clear(SKColors.Green);
        using var imagen=SKImage.FromBitmap(bitmap); using var png=imagen.Encode(SKEncodedImageFormat.Png,100);
        using var stream=new MemoryStream(png.ToArray());
        var resultado = await servicio.GuardarAsync(stream,default); Assert.True(resultado.Exito,resultado.Error?.Mensaje);
        var ruta = servicio.RutaFisica(resultado.Valor!)!;
        try {
            using var normal = SKBitmap.Decode(ruta); Assert.Equal(1600,normal.Width); Assert.Equal(800,normal.Height);
            Assert.EndsWith(".jpg",ruta); Assert.Null(servicio.RutaFisica("../ajeno.jpg"));
            var bytes = await File.ReadAllBytesAsync(ruta); Assert.Equal(0xff,bytes[0]); Assert.Equal(0xd8,bytes[1]);
        } finally { servicio.Eliminar(resultado.Valor!); Directory.Delete(carpeta); }
    }
    [Fact]
    public async Task No_se_aceptan_SVG_HTML_archivos_falsos_ni_subidas_mayores_de_cinco_MB()
    {
        var servicio = new FotosResenas(Options.Create(new OpcionesFotosResenas { Carpeta = Path.GetFullPath(".artifacts/fotos-no-creadas") }));
        foreach(var contenido in new[] {System.Text.Encoding.UTF8.GetBytes("<svg onload='alert(1)'/>"),System.Text.Encoding.UTF8.GetBytes("<html>falso jpg</html>"),new byte[5*1024*1024+1]}) {
            using var stream=new MemoryStream(contenido); Assert.False((await servicio.GuardarAsync(stream,default)).Exito);
        }
    }
}
