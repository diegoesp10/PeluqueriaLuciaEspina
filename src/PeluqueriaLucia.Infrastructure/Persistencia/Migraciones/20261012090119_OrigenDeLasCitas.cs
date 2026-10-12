using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class OrigenDeLasCitas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Origen",
                table: "ReservasCitas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservasCitas_Origen",
                table: "ReservasCitas",
                sql: "[Origen] IN (0,1,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservasCitas_Origen",
                table: "ReservasCitas");

            migrationBuilder.DropColumn(
                name: "Origen",
                table: "ReservasCitas");
        }
    }
}
