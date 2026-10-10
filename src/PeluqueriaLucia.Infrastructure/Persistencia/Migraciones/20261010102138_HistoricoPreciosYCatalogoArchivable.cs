using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Segunda versión de la aplicación. Migración solo aditiva, pensada para una base de datos con datos reales:
    /// <list type="bullet">
    /// <item>Cada servicio hecho en una visita guarda su nombre y su precio (NombreAplicado, PrecioAplicado). Las filas
    /// existentes se rellenan con el nombre y el precio actuales del catálogo, que es lo que la versión anterior mostraba.</item>
    /// <item>Los servicios del catálogo se pueden archivar (Activo). Todos los existentes quedan activos.</item>
    /// <item>Índices para filtrar visitas por fecha y buscar clientes por nombre.</item>
    /// <item>Borrar un cliente o un servicio ya no arrastra en cascada sus visitas: la base de datos lo impide (Restrict).
    /// Solo se cambia la regla de la clave foránea; los datos no se tocan.</item>
    /// </list>
    /// No borra ni renombra tablas ni columnas.
    /// </summary>
    public partial class HistoricoPreciosYCatalogoArchivable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiciosClientes_Clientes_ClienteId",
                table: "ServiciosClientes");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiciosGenericosRealizados_ServiciosGenericos_ServicioGenericoId",
                table: "ServiciosGenericosRealizados");

            migrationBuilder.AddColumn<string>(
                name: "NombreAplicado",
                table: "ServiciosGenericosRealizados",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioAplicado",
                table: "ServiciosGenericosRealizados",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "ServiciosGenericos",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Las visitas existentes se quedan con el nombre y el precio actuales del catálogo.
            // Dentro de EXEC para que SQL Server lo compile al ejecutarlo, cuando las columnas nuevas ya existen
            migrationBuilder.Sql(
                """
                EXEC(N'
                UPDATE r
                SET r.NombreAplicado = g.Nombre,
                    r.PrecioAplicado = g.Precio
                FROM ServiciosGenericosRealizados AS r
                INNER JOIN ServiciosGenericos AS g ON g.Id = r.ServicioGenericoId;
                ');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ServiciosClientes_Fecha",
                table: "ServiciosClientes",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Nombre_Apellido",
                table: "Clientes",
                columns: new[] { "Nombre", "Apellido" });

            migrationBuilder.AddForeignKey(
                name: "FK_ServiciosClientes_Clientes_ClienteId",
                table: "ServiciosClientes",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiciosGenericosRealizados_ServiciosGenericos_ServicioGenericoId",
                table: "ServiciosGenericosRealizados",
                column: "ServicioGenericoId",
                principalTable: "ServiciosGenericos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <summary>Vuelta atrás de emergencia: recupera el esquema anterior (se pierden nombre y precio guardados por visita).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiciosClientes_Clientes_ClienteId",
                table: "ServiciosClientes");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiciosGenericosRealizados_ServiciosGenericos_ServicioGenericoId",
                table: "ServiciosGenericosRealizados");

            migrationBuilder.DropIndex(
                name: "IX_ServiciosClientes_Fecha",
                table: "ServiciosClientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_Nombre_Apellido",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "NombreAplicado",
                table: "ServiciosGenericosRealizados");

            migrationBuilder.DropColumn(
                name: "PrecioAplicado",
                table: "ServiciosGenericosRealizados");

            migrationBuilder.DropColumn(
                name: "Activo",
                table: "ServiciosGenericos");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiciosClientes_Clientes_ClienteId",
                table: "ServiciosClientes",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiciosGenericosRealizados_ServiciosGenericos_ServicioGenericoId",
                table: "ServiciosGenericosRealizados",
                column: "ServicioGenericoId",
                principalTable: "ServiciosGenericos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
