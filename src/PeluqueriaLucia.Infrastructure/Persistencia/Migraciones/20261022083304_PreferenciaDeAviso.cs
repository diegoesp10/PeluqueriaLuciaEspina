using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class PreferenciaDeAviso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CanalPreferido",
                table: "Clientes",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Clientes_CanalPreferido",
                table: "Clientes",
                sql: "[CanalPreferido] IS NULL OR [CanalPreferido] IN (0,1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Clientes_CanalPreferido",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "CanalPreferido",
                table: "Clientes");
        }
    }
}
