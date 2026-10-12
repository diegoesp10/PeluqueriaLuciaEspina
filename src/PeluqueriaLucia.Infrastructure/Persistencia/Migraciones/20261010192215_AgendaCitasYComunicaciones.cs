using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgendaCitasYComunicaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CierresAgenda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Dia = table.Column<DateOnly>(type: "date", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresAgenda", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReservasCitas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Inicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Telefono = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    ServicioId = table.Column<int>(type: "int", nullable: true),
                    ServicioNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CaducaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    TelefonoValidado = table.Column<bool>(type: "bit", nullable: false),
                    AdministradorId = table.Column<int>(type: "int", nullable: true),
                    NotaInterna = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasCitas", x => x.Id);
                    table.CheckConstraint("CK_ReservasCitas_Intervalo", "[Fin] > [Inicio]");
                    table.ForeignKey(
                        name: "FK_ReservasCitas_Administradores_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasCitas_ServiciosGenericos_ServicioId",
                        column: x => x.ServicioId,
                        principalTable: "ServiciosGenericos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmsEntrantes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProveedorId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Telefono = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RecibidoUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsEntrantes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TramosAgenda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Dia = table.Column<int>(type: "int", nullable: false),
                    Desde = table.Column<TimeOnly>(type: "time", nullable: false),
                    Hasta = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TramosAgenda", x => x.Id);
                    table.CheckConstraint("CK_TramosAgenda_Intervalo", "[Hasta] > [Desde]");
                });

            migrationBuilder.CreateTable(
                name: "MensajesCitas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Telefono = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcesadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProveedorId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajesCitas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MensajesCitas_ReservasCitas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "ReservasCitas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CierresAgenda_Dia",
                table: "CierresAgenda",
                column: "Dia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MensajesCitas_Estado_CreadoUtc",
                table: "MensajesCitas",
                columns: new[] { "Estado", "CreadoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MensajesCitas_ReservaId",
                table: "MensajesCitas",
                column: "ReservaId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasCitas_AdministradorId",
                table: "ReservasCitas",
                column: "AdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasCitas_Inicio_Estado",
                table: "ReservasCitas",
                columns: new[] { "Inicio", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasCitas_IpHash_CreadaUtc",
                table: "ReservasCitas",
                columns: new[] { "IpHash", "CreadaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasCitas_ServicioId",
                table: "ReservasCitas",
                column: "ServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasCitas_SolicitudId",
                table: "ReservasCitas",
                column: "SolicitudId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservasCitas_Telefono_CreadaUtc",
                table: "ReservasCitas",
                columns: new[] { "Telefono", "CreadaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsEntrantes_ProveedorId",
                table: "SmsEntrantes",
                column: "ProveedorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TramosAgenda_Dia_Desde",
                table: "TramosAgenda",
                columns: new[] { "Dia", "Desde" },
                unique: true);
            // Horario inicial editable desde /admin/agenda/horarios; confirmar antes de abrir reservas al público.
            migrationBuilder.Sql("""
                INSERT INTO TramosAgenda (Dia, Desde, Hasta) VALUES
                (1,'09:45','13:00'),(1,'15:00','19:00'),
                (2,'09:45','13:00'),(2,'15:00','19:00'),
                (3,'09:45','13:00'),(3,'15:00','19:00'),
                (4,'09:45','13:00'),(4,'15:00','19:00'),
                (5,'09:45','13:00'),(5,'15:00','19:00'),(6,'09:45','13:00');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CierresAgenda");

            migrationBuilder.DropTable(
                name: "MensajesCitas");

            migrationBuilder.DropTable(
                name: "SmsEntrantes");

            migrationBuilder.DropTable(
                name: "TramosAgenda");

            migrationBuilder.DropTable(
                name: "ReservasCitas");
        }
    }
}
