using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class IntervalosConfigurablesAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntervaloMinutos",
                table: "TramosAgenda",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TramosAgenda_IntervaloMinutos",
                table: "TramosAgenda",
                sql: "[IntervaloMinutos] BETWEEN 1 AND 600");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TramosAgenda_IntervaloMinutos",
                table: "TramosAgenda");

            migrationBuilder.DropColumn(
                name: "IntervaloMinutos",
                table: "TramosAgenda");
        }
    }
}
