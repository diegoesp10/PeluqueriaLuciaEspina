using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PelqueriaLuciaEspina.Migrations
{
    /// <inheritdoc />
    public partial class TicketSinClientePrecioEditableIva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiciosClientes_Clientes_ClienteId",
                table: "ServiciosClientes");

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioAplicado",
                table: "ServiciosGenericosRealizados",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Rellena el precio aplicado de los registros ya existentes con el precio de
            // catálogo vigente, ya que hasta ahora el importe se leía siempre de ahí.
            migrationBuilder.Sql(@"
                UPDATE sgr
                SET sgr.PrecioAplicado = sg.Precio
                FROM ServiciosGenericosRealizados sgr
                INNER JOIN ServiciosGenericos sg ON sg.Id = sgr.ServicioGenericoId
                WHERE sgr.PrecioAplicado = 0;
            ");

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
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiciosClientes_Clientes_ClienteId",
                table: "ServiciosClientes",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiciosClientes_Clientes_ClienteId",
                table: "ServiciosClientes");

            migrationBuilder.DropColumn(
                name: "PrecioAplicado",
                table: "ServiciosGenericosRealizados");

            migrationBuilder.DropColumn(
                name: "PorcentajeIva",
                table: "ServiciosClientes");

            migrationBuilder.AlterColumn<int>(
                name: "ClienteId",
                table: "ServiciosClientes",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiciosClientes_Clientes_ClienteId",
                table: "ServiciosClientes",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
