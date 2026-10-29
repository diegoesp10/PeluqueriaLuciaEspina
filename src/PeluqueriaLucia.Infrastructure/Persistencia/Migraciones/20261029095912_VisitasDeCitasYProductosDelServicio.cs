using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class VisitasDeCitasYProductosDelServicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReservaId",
                table: "ServiciosClientes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Uso",
                table: "ProductosVisitas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ServiciosClientes_ReservaId",
                table: "ServiciosClientes",
                column: "ReservaId",
                unique: true,
                filter: "[ReservaId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductosVisitas_Uso",
                table: "ProductosVisitas",
                sql: "[Uso] = 0 OR ([Uso] IN (1, 2) AND [PrecioUnitario] = 0 AND [Importe] = 0)");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiciosClientes_ReservasCitas_ReservaId",
                table: "ServiciosClientes",
                column: "ReservaId",
                principalTable: "ReservasCitas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiciosClientes_ReservasCitas_ReservaId",
                table: "ServiciosClientes");

            migrationBuilder.DropIndex(
                name: "IX_ServiciosClientes_ReservaId",
                table: "ServiciosClientes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductosVisitas_Uso",
                table: "ProductosVisitas");

            migrationBuilder.DropColumn(
                name: "ReservaId",
                table: "ServiciosClientes");

            migrationBuilder.DropColumn(
                name: "Uso",
                table: "ProductosVisitas");
        }
    }
}
