using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class DatosClienteYVerificacionPublica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "ReservasCitas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AddColumn<string>(
                name: "Apellido",
                table: "ReservasCitas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Sexo",
                table: "ReservasCitas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VerificacionesContacto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    Telefono = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    DestinoCorreo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    CodigoProtegido = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    SecretoProtegido = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CodigoHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    IpHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CaducaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumidaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    EstadoEnvio = table.Column<int>(type: "int", nullable: false),
                    ProcesadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificacionesContacto", x => x.Id);
                    table.CheckConstraint("CK_VerificacionesContacto_Intentos", "[Intentos] BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_VerificacionesContacto_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReservasCitas_Sexo",
                table: "ReservasCitas",
                sql: "[Sexo] IS NULL OR [Sexo] IN (0,1,2)");

            migrationBuilder.CreateIndex(
                name: "IX_VerificacionesContacto_ClienteId",
                table: "VerificacionesContacto",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificacionesContacto_EstadoEnvio_CaducaUtc",
                table: "VerificacionesContacto",
                columns: new[] { "EstadoEnvio", "CaducaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VerificacionesContacto_IpHash_CreadaUtc",
                table: "VerificacionesContacto",
                columns: new[] { "IpHash", "CreadaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VerificacionesContacto_Telefono_CreadaUtc",
                table: "VerificacionesContacto",
                columns: new[] { "Telefono", "CreadaUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerificacionesContacto");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReservasCitas_Sexo",
                table: "ReservasCitas");

            migrationBuilder.DropColumn(
                name: "Apellido",
                table: "ReservasCitas");

            migrationBuilder.DropColumn(
                name: "Sexo",
                table: "ReservasCitas");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "ReservasCitas",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
