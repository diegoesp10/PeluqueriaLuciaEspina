using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ControlDeProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Referencia = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CodigoBarras = table.Column<string>(type: "varchar(14)", unicode: false, maxLength: 14, nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PrecioVenta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioCoste = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    StockMinimo = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                    table.CheckConstraint("CK_Productos_PrecioVenta", "[PrecioVenta] > 0");
                    table.CheckConstraint("CK_Productos_StockMinimo", "[StockMinimo] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "FotosProductos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    SubidaEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosProductos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FotosProductos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosStock",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    StockResultante = table.Column<int>(type: "int", nullable: false),
                    Nota = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VisitaId = table.Column<int>(type: "int", nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdministradorId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosStock", x => x.Id);
                    table.CheckConstraint("CK_MovimientosStock_Cantidad", "[Cantidad] <> 0");
                    table.ForeignKey(
                        name: "FK_MovimientosStock_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovimientosStock_ServiciosClientes_VisitaId",
                        column: x => x.VisitaId,
                        principalTable: "ServiciosClientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductosVisitas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitaId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosVisitas", x => x.Id);
                    table.CheckConstraint("CK_ProductosVisitas_Cantidad", "[Cantidad] BETWEEN 1 AND 99");
                    table.ForeignKey(
                        name: "FK_ProductosVisitas_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductosVisitas_ServiciosClientes_VisitaId",
                        column: x => x.VisitaId,
                        principalTable: "ServiciosClientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FotosProductos_ProductoId_Orden",
                table: "FotosProductos",
                columns: new[] { "ProductoId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_ProductoId_Fecha",
                table: "MovimientosStock",
                columns: new[] { "ProductoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_VisitaId",
                table: "MovimientosStock",
                column: "VisitaId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Activo_Nombre",
                table: "Productos",
                columns: new[] { "Activo", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CodigoBarras",
                table: "Productos",
                column: "CodigoBarras",
                unique: true,
                filter: "[CodigoBarras] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_Referencia",
                table: "Productos",
                column: "Referencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductosVisitas_ProductoId",
                table: "ProductosVisitas",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosVisitas_VisitaId",
                table: "ProductosVisitas",
                column: "VisitaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotosProductos");

            migrationBuilder.DropTable(
                name: "MovimientosStock");

            migrationBuilder.DropTable(
                name: "ProductosVisitas");

            migrationBuilder.DropTable(
                name: "Productos");
        }
    }
}
