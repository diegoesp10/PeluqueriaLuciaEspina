using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ClientesDeAgendaYFaltas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClienteId",
                table: "ReservasCitas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PendienteVinculacion",
                table: "ReservasCitas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Apodo",
                table: "Clientes",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PendienteCompletar",
                table: "Clientes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Las citas previamente aceptadas quedan para vinculación manual, sin crear fichas retroactivamente.
            migrationBuilder.Sql("UPDATE ReservasCitas SET PendienteVinculacion=1 WHERE AceptadaUtc IS NOT NULL AND ClienteId IS NULL");

            migrationBuilder.CreateTable(
                name: "FaltasAsistencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    ReservaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InicioCita = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegistradaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdministradorId = table.Column<int>(type: "int", nullable: false),
                    Nota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AnuladaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnuladaPorId = table.Column<int>(type: "int", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaltasAsistencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaltasAsistencia_Administradores_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FaltasAsistencia_Administradores_AnuladaPorId",
                        column: x => x.AnuladaPorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FaltasAsistencia_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FaltasAsistencia_ReservasCitas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "ReservasCitas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasCitas_ClienteId",
                table: "ReservasCitas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_PendienteCompletar",
                table: "Clientes",
                column: "PendienteCompletar");

            migrationBuilder.CreateIndex(
                name: "IX_FaltasAsistencia_AdministradorId",
                table: "FaltasAsistencia",
                column: "AdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_FaltasAsistencia_AnuladaPorId",
                table: "FaltasAsistencia",
                column: "AnuladaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_FaltasAsistencia_ClienteId",
                table: "FaltasAsistencia",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_FaltasAsistencia_ReservaId",
                table: "FaltasAsistencia",
                column: "ReservaId",
                unique: true,
                filter: "[AnuladaUtc] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ReservasCitas_Clientes_ClienteId",
                table: "ReservasCitas",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReservasCitas_Clientes_ClienteId",
                table: "ReservasCitas");

            migrationBuilder.DropTable(
                name: "FaltasAsistencia");

            migrationBuilder.DropIndex(
                name: "IX_ReservasCitas_ClienteId",
                table: "ReservasCitas");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_PendienteCompletar",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "ReservasCitas");

            migrationBuilder.DropColumn(
                name: "PendienteVinculacion",
                table: "ReservasCitas");

            migrationBuilder.DropColumn(
                name: "Apodo",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "PendienteCompletar",
                table: "Clientes");
        }
    }
}
