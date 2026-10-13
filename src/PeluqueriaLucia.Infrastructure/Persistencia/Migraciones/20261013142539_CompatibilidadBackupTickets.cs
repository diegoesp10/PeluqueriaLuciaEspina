using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CompatibilidadBackupTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ClienteId",
                table: "ServiciosClientes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<decimal>(
                name: "PorcentajeIva",
                table: "ServiciosClientes",
                type: "decimal(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM ServiciosClientes WHERE ClienteId IS NULL OR PorcentajeIva IS NOT NULL)
                    THROW 51000, 'No se puede revertir: hay tickets sin cliente o con IVA histórico. Conserva estos datos antes de volver al esquema anterior.', 1;
                """);

            migrationBuilder.DropColumn(
                name: "PorcentajeIva",
                table: "ServiciosClientes");

            migrationBuilder.AlterColumn<int>(
                name: "ClienteId",
                table: "ServiciosClientes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
