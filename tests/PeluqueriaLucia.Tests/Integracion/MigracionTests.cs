using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PeluqueriaLucia.Tests.Integracion;

/// <summary>
/// La actualización de la base de datos de la peluquería no puede perder nada: se reproduce el esquema de la primera
/// versión con datos, se migra y se comprueba que todo sigue ahí y que los datos nuevos se han rellenado bien.
/// </summary>
public sealed class MigracionTests
{
    private const string UltimaMigracionDeLaVersionAnterior = "20250705153725_TotalServicioCliente";

    [Fact]
    public async Task Migrar_una_base_de_datos_de_la_version_anterior_conserva_todos_los_datos()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(UltimaMigracionDeLaVersionAnterior);
        await bd.EjecutarAsync(
            """
            INSERT INTO Clientes (Nombre, Apellido, Email, Telefono, FechaCreacion, Sexo) VALUES
             (N'Ana', N'García', NULL, N'600111222', '2025-07-01', 1),
             (N'Luis', N'Pérez', N'luis@ejemplo.es', NULL, '2025-07-02', 0);
            INSERT INTO ServiciosGenericos (Nombre, Precio, ImagenRuta, Sexo, FechaCreacion) VALUES
             (N'Corte mujer', 18.50, '/imagenes/a.jpg', 1, '2025-07-01'),
             (N'Corte hombre', 12.00, NULL, 0, '2025-07-01'),
             (N'Tinte', 35.00, NULL, 1, '2025-07-01');
            INSERT INTO ServiciosClientes (Fecha, ClienteId, OpcionPago, Estado, TotalEfectivo, TotalTarjeta) VALUES
             ('2025-07-10 10:00', 1, 3, 1, 20.00, 33.50),
             ('2025-07-11 09:15', 2, 2, 2, NULL, NULL);
            INSERT INTO ServiciosGenericosRealizados (ServicioClienteId, ServicioGenericoId) VALUES (1, 1), (1, 3), (2, 2);
            """);

        await using (var db = bd.Contexto())
        {
            Assert.Contains("20261010102138_HistoricoPreciosYCatalogoArchivable", await db.Database.GetPendingMigrationsAsync());
            await db.GetService<IMigrator>().MigrateAsync();
        }

        Assert.Equal(2, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM Clientes"));
        Assert.Equal(3, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ServiciosGenericos"));
        Assert.Equal(2, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ServiciosClientes"));
        Assert.Equal(3, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ServiciosGenericosRealizados"));

        // Cada línea antigua recibe el nombre y el precio del catálogo
        Assert.Equal(0, await bd.EscalarAsync<int>(
            """
            SELECT COUNT(*) FROM ServiciosGenericosRealizados r
            JOIN ServiciosGenericos g ON g.Id = r.ServicioGenericoId
            WHERE r.PrecioAplicado <> g.Precio OR r.NombreAplicado <> g.Nombre
            """));
        Assert.Equal(53.50m, await bd.EscalarAsync<decimal>("SELECT SUM(PrecioAplicado) FROM ServiciosGenericosRealizados WHERE ServicioClienteId = 1"));

        // Todo el catálogo existente queda activo y los cobros antiguos intactos
        Assert.Equal(3, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ServiciosGenericos WHERE Activo = 1"));
        Assert.Equal(33.50m, await bd.EscalarAsync<decimal>("SELECT TotalTarjeta FROM ServiciosClientes WHERE Id = 1"));

        await using (var db = bd.Contexto())
        {
            var visita = await db.Visitas.Include(v => v.Lineas).Include(v => v.Cliente).SingleAsync(v => v.Id == 1);
            Assert.Equal(53.50m, visita.Total);
            Assert.Equal("Ana García", visita.Cliente!.NombreCompleto);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
    }

    /// <summary>
    /// En el equipo de la peluquería la base de datos se actualiza con el script idempotente (docs/DESPLIEGUE.md).
    /// El script es un único lote de SQL: se comprueba que se puede ejecutar sobre la versión anterior, y dos veces.
    /// </summary>
    [Fact]
    public async Task El_script_idempotente_actualiza_la_version_anterior_y_se_puede_repetir()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(UltimaMigracionDeLaVersionAnterior);
        await bd.EjecutarAsync(
            """
            INSERT INTO Clientes (Nombre, Apellido, FechaCreacion, Sexo) VALUES (N'Ana', N'García', '2025-07-01', 1);
            INSERT INTO ServiciosGenericos (Nombre, Precio, Sexo, FechaCreacion) VALUES (N'Corte', 18.50, 1, '2025-07-01');
            INSERT INTO ServiciosClientes (Fecha, ClienteId, OpcionPago, Estado) VALUES ('2025-07-10', 1, 2, 2);
            INSERT INTO ServiciosGenericosRealizados (ServicioClienteId, ServicioGenericoId) VALUES (1, 1);
            """);

        string script;
        await using (var db = bd.Contexto())
            script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);

        for (var vez = 0; vez < 2; vez++)
        {
            foreach (var lote in System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline))
            {
                if (!string.IsNullOrWhiteSpace(lote))
                    await bd.EjecutarAsync(lote);
            }
        }

        Assert.Equal(18.50m, await bd.EscalarAsync<decimal>("SELECT PrecioAplicado FROM ServiciosGenericosRealizados"));
        Assert.Equal("Corte", await bd.EscalarAsync<string>("SELECT NombreAplicado FROM ServiciosGenericosRealizados"));
        Assert.Equal(1, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ServiciosClientes"));
        await using var final = bd.Contexto();
        Assert.Empty(await final.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task El_modelo_coincide_con_las_migraciones()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await using var db = bd.Contexto();

        // Si alguien cambia una entidad y olvida crear la migración, esta prueba lo detecta
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
