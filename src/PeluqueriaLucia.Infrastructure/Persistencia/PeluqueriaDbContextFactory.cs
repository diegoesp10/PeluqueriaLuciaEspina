using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

/// <summary>
/// Las herramientas de EF utilizan el mismo servicio conectado que la web y no leen cadenas de appsettings.
/// </summary>
internal sealed class PeluqueriaDbContextFactory : IDesignTimeDbContextFactory<PeluqueriaDbContext>
{
    public PeluqueriaDbContext CreateDbContext(string[] args)
    {
        var cadena = ServicioConexionSql.Obtener();
        return new PeluqueriaDbContext(new DbContextOptionsBuilder<PeluqueriaDbContext>().UseSqlServer(cadena).Options);
    }
}
