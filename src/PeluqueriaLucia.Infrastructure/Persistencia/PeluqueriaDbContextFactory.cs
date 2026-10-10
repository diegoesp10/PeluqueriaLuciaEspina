using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

/// <summary>
/// Lo usan solo las herramientas de EF (<c>dotnet ef migrations …</c>). La base de datos de destino se elige con
/// <c>--connection</c> o con la variable de entorno PELUQUERIA_CONEXION; por defecto, la base de datos de desarrollo.
/// </summary>
internal sealed class PeluqueriaDbContextFactory : IDesignTimeDbContextFactory<PeluqueriaDbContext>
{
    public PeluqueriaDbContext CreateDbContext(string[] args)
    {
        var cadena = ServicioConexionSql.Obtener();
        return new PeluqueriaDbContext(new DbContextOptionsBuilder<PeluqueriaDbContext>().UseSqlServer(cadena).Options);
    }
}
