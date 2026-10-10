namespace PeluqueriaLucia.Web.Infraestructura;

public static class ImagenesPrivadas
{
    // Las rutas históricas se conservan en SQL; el navegador las abre dentro del área protegida.
    public static string? Ruta(string? original) => original?.StartsWith("/imagenes/", StringComparison.OrdinalIgnoreCase) == true
        ? "/admin" + original : original;
}
