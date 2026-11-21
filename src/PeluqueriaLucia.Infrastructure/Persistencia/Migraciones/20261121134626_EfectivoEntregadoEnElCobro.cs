using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Lo que da el cliente en efectivo al cobrar (la calculadora del cambio), para imprimirlo en el ticket con el cambio. Solo
    /// añade una columna que admite nulos: las visitas que ya había quedan sin ese dato y su ticket sale como siempre.
    /// </summary>
    public partial class EfectivoEntregadoEnElCobro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EfectivoEntregado",
                table: "ServiciosClientes",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EfectivoEntregado",
                table: "ServiciosClientes");
        }
    }
}
