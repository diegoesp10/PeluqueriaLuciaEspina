using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PelqueriaLuciaEspina.Migrations
{
    /// <inheritdoc />
    public partial class TotalServicioCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalEfectivo",
                table: "ServiciosClientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalTarjeta",
                table: "ServiciosClientes",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalEfectivo",
                table: "ServiciosClientes");

            migrationBuilder.DropColumn(
                name: "TotalTarjeta",
                table: "ServiciosClientes");
        }
    }
}
