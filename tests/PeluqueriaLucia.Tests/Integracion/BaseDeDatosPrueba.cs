using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Tests.Integracion;

/// <summary>
/// Base de datos SQL Server de usar y tirar para las pruebas de integración: se crea vacía con un nombre único y se
/// borra al terminar. El servidor se elige con la variable PELUQUERIA_PRUEBAS_SQL (por defecto, el SQL Server local).
/// </summary>
public sealed class BaseDeDatosPrueba : IAsyncDisposable
{
    private const string ServidorPorDefecto = "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    private BaseDeDatosPrueba(string cadena) => Cadena = cadena;

    public string Cadena { get; }

    /// <summary>Crea la base de datos aplicando migraciones hasta <paramref name="migracion"/> (todas si es null).</summary>
    public static async Task<BaseDeDatosPrueba> CrearAsync(string? migracion = null)
    {
        var servidor = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PELUQUERIA_PRUEBAS_SQL") ?? ServidorPorDefecto)
        {
            InitialCatalog = $"PeluqueriaLucia_Pruebas_{Guid.NewGuid():N}",
        };
        var bd = new BaseDeDatosPrueba(servidor.ConnectionString);
        await using var db = bd.Contexto();
        await db.GetService<IMigrator>().MigrateAsync(migracion);
        return bd;
    }

    public PeluqueriaDbContext Contexto() =>
        new(new DbContextOptionsBuilder<PeluqueriaDbContext>().UseSqlServer(Cadena).Options);

    public async Task<int> EjecutarAsync(string sql)
    {
        await using var db = Contexto();
        return await db.Database.ExecuteSqlRawAsync(sql);
    }

    public async Task<T> EscalarAsync<T>(string sql)
    {
        await using var conexion = new SqlConnection(Cadena);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        return (T)Convert.ChangeType((await comando.ExecuteScalarAsync())!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var db = Contexto();
        await db.Database.EnsureDeletedAsync();
    }
}
