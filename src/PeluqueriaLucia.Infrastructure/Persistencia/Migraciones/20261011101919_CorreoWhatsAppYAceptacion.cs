using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CorreoWhatsAppYAceptacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MensajesCitas_ReservasCitas_ReservaId",
                table: "MensajesCitas");

            migrationBuilder.RenameTable(
                name: "SmsEntrantes", newName: "ComunicacionesHistoricas");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MensajesCitas",
                table: "MensajesCitas");

            migrationBuilder.DropColumn(
                name: "ProveedorId",
                table: "MensajesCitas");

            migrationBuilder.RenameColumn(
                name: "Telefono",
                table: "MensajesCitas",
                newName: "Destinatario");

            migrationBuilder.RenameTable(
                name: "MensajesCitas",
                newName: "NotificacionesCitas");

            migrationBuilder.RenameIndex(
                name: "IX_MensajesCitas_ReservaId",
                table: "NotificacionesCitas",
                newName: "IX_NotificacionesCitas_ReservaId");

            migrationBuilder.RenameIndex(
                name: "IX_MensajesCitas_Estado_CreadoUtc",
                table: "NotificacionesCitas",
                newName: "IX_NotificacionesCitas_Estado_CreadoUtc");

            migrationBuilder.AddColumn<DateTime>(
                name: "AceptadaUtc",
                table: "ReservasCitas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Canal",
                table: "ReservasCitas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmacionHash",
                table: "ReservasCitas",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Correo",
                table: "ReservasCitas",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Texto",
                table: "NotificacionesCitas",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<int>(
                name: "Canal",
                table: "NotificacionesCitas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Destinatario",
                table: "NotificacionesCitas",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string), oldType: "varchar(16)", oldUnicode: false, oldMaxLength: 16);

            // El historial del transporte retirado se conserva, pero nunca se reutiliza ni se envía.
            migrationBuilder.Sql("UPDATE [NotificacionesCitas] SET [Estado] = 6, [Canal] = 1;");

            migrationBuilder.AddColumn<string>(
                name: "TokenProtegido",
                table: "NotificacionesCitas",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_NotificacionesCitas",
                table: "NotificacionesCitas",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasCitas_ConfirmacionHash",
                table: "ReservasCitas",
                column: "ConfirmacionHash",
                unique: true,
                filter: "[ConfirmacionHash] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificacionesCitas_ReservasCitas_ReservaId",
                table: "NotificacionesCitas",
                column: "ReservaId",
                principalTable: "ReservasCitas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificacionesCitas_ReservasCitas_ReservaId",
                table: "NotificacionesCitas");

            migrationBuilder.DropIndex(
                name: "IX_ReservasCitas_ConfirmacionHash",
                table: "ReservasCitas");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NotificacionesCitas",
                table: "NotificacionesCitas");

            migrationBuilder.DropColumn(
                name: "AceptadaUtc",
                table: "ReservasCitas");

            migrationBuilder.DropColumn(
                name: "Canal",
                table: "ReservasCitas");

            migrationBuilder.DropColumn(
                name: "ConfirmacionHash",
                table: "ReservasCitas");

            migrationBuilder.DropColumn(
                name: "Correo",
                table: "ReservasCitas");

            migrationBuilder.DropColumn(
                name: "Canal",
                table: "NotificacionesCitas");

            migrationBuilder.DropColumn(
                name: "Destinatario",
                table: "NotificacionesCitas");

            migrationBuilder.DropColumn(
                name: "TokenProtegido",
                table: "NotificacionesCitas");

            migrationBuilder.RenameTable(
                name: "NotificacionesCitas",
                newName: "MensajesCitas");

            migrationBuilder.RenameIndex(
                name: "IX_NotificacionesCitas_ReservaId",
                table: "MensajesCitas",
                newName: "IX_MensajesCitas_ReservaId");

            migrationBuilder.RenameIndex(
                name: "IX_NotificacionesCitas_Estado_CreadoUtc",
                table: "MensajesCitas",
                newName: "IX_MensajesCitas_Estado_CreadoUtc");

            migrationBuilder.AlterColumn<string>(
                name: "Texto",
                table: "MensajesCitas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<string>(
                name: "ProveedorId",
                table: "MensajesCitas",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "MensajesCitas",
                type: "varchar(16)",
                unicode: false,
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MensajesCitas",
                table: "MensajesCitas",
                column: "Id");

            migrationBuilder.RenameTable(name: "ComunicacionesHistoricas", newName: "SmsEntrantes");

            migrationBuilder.AddForeignKey(
                name: "FK_MensajesCitas_ReservasCitas_ReservaId",
                table: "MensajesCitas",
                column: "ReservaId",
                principalTable: "ReservasCitas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
