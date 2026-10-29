using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class SorteosYCostesDeProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CostesProductos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Coste = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Desde = table.Column<DateOnly>(type: "date", nullable: false),
                    Nota = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RegistradoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdministradorId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostesProductos", x => x.Id);
                    table.CheckConstraint("CK_CostesProductos_Coste", "[Coste] >= 0");
                    table.ForeignKey(
                        name: "FK_CostesProductos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sorteos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TituloEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DescripcionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TipoPremio = table.Column<int>(type: "int", nullable: false),
                    ServicioId = table.Column<int>(type: "int", nullable: true),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    Premio = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PremioEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FechaSorteo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    MotivoCancelacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sorteos", x => x.Id);
                    table.CheckConstraint("CK_Sorteos_Estado", "[Estado] IN (0, 1, 2)");
                    table.CheckConstraint("CK_Sorteos_Premio", "([TipoPremio] = 0 AND [ProductoId] IS NULL) OR ([TipoPremio] = 1 AND [ServicioId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Sorteos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Sorteos_ServiciosGenericos_ServicioId",
                        column: x => x.ServicioId,
                        principalTable: "ServiciosGenericos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AnunciosSorteos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorteoId = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TextoEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Desde = table.Column<DateOnly>(type: "date", nullable: false),
                    Hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnunciosSorteos", x => x.Id);
                    table.CheckConstraint("CK_AnunciosSorteos_Fechas", "[Hasta] >= [Desde]");
                    table.ForeignKey(
                        name: "FK_AnunciosSorteos_Sorteos_SorteoId",
                        column: x => x.SorteoId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExtraccionesSorteos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorteoId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    Ganador = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false),
                    Forma = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Participantes = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AdministradorId = table.Column<int>(type: "int", nullable: true),
                    AvisoCanal = table.Column<int>(type: "int", nullable: true),
                    AvisadoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtraccionesSorteos", x => x.Id);
                    table.CheckConstraint("CK_ExtraccionesSorteos_Participantes", "[Participantes] >= 1");
                    table.ForeignKey(
                        name: "FK_ExtraccionesSorteos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExtraccionesSorteos_Sorteos_SorteoId",
                        column: x => x.SorteoId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParticipantesSorteos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorteoId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    AnadidoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantesSorteos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantesSorteos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantesSorteos_Sorteos_SorteoId",
                        column: x => x.SorteoId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnunciosSorteos_Desde_Hasta",
                table: "AnunciosSorteos",
                columns: new[] { "Desde", "Hasta" });

            migrationBuilder.CreateIndex(
                name: "IX_AnunciosSorteos_SorteoId",
                table: "AnunciosSorteos",
                column: "SorteoId");

            migrationBuilder.CreateIndex(
                name: "IX_CostesProductos_ProductoId_Desde",
                table: "CostesProductos",
                columns: new[] { "ProductoId", "Desde" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtraccionesSorteos_ClienteId",
                table: "ExtraccionesSorteos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtraccionesSorteos_SorteoId",
                table: "ExtraccionesSorteos",
                column: "SorteoId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesSorteos_ClienteId",
                table: "ParticipantesSorteos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesSorteos_SorteoId_ClienteId",
                table: "ParticipantesSorteos",
                columns: new[] { "SorteoId", "ClienteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sorteos_Estado_FechaSorteo",
                table: "Sorteos",
                columns: new[] { "Estado", "FechaSorteo" });

            migrationBuilder.CreateIndex(
                name: "IX_Sorteos_ProductoId",
                table: "Sorteos",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Sorteos_ServicioId",
                table: "Sorteos",
                column: "ServicioId");

            // Los productos que ya tenían precio de coste empiezan su historial con él, desde el día en que se dieron de alta.
            // Solo añade filas: el coste de cada producto no cambia.
            migrationBuilder.Sql("""
                INSERT INTO CostesProductos (ProductoId, Coste, Desde, Nota, RegistradoEn, AdministradorId)
                SELECT Id, PrecioCoste, CAST(CreadoEn AS date), N'Coste anterior al historial', ActualizadoEn, NULL
                FROM Productos
                WHERE PrecioCoste IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnunciosSorteos");

            migrationBuilder.DropTable(
                name: "CostesProductos");

            migrationBuilder.DropTable(
                name: "ExtraccionesSorteos");

            migrationBuilder.DropTable(
                name: "ParticipantesSorteos");

            migrationBuilder.DropTable(
                name: "Sorteos");
        }
    }
}
