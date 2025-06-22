using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PelqueriaLuciaEspina.Migrations
{
    /// <inheritdoc />
    public partial class AñadidometodoPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OpcionPago",
                table: "Servicios",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpcionPago",
                table: "Servicios");
        }
    }
}
