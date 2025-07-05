using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PelqueriaLuciaEspina.Migrations
{
    /// <inheritdoc />
    public partial class SexoEnClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Sexo",
                table: "Clientes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sexo",
                table: "Clientes");
        }
    }
}
