using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ProductosDeSorteoYVariosPremios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TipoPremio",
                table: "Sorteos",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Premio",
                table: "Sorteos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AddColumn<DateTime>(
                name: "EntregadoEn",
                table: "ExtraccionesSorteos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductosSorteo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NombreEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DescripcionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EliminadoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosSorteo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PremiosSorteos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorteoId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    ServicioId = table.Column<int>(type: "int", nullable: true),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    ProductoSorteoId = table.Column<int>(type: "int", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NombreEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DescripcionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Cantidad = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiosSorteos", x => x.Id);
                    table.CheckConstraint("CK_PremiosSorteos_Articulo", "([Tipo] = 0 AND [ProductoId] IS NULL AND [ProductoSorteoId] IS NULL) OR ([Tipo] = 1 AND [ServicioId] IS NULL AND [ProductoSorteoId] IS NULL) OR ([Tipo] = 2 AND [ServicioId] IS NULL AND [ProductoId] IS NULL)");
                    table.CheckConstraint("CK_PremiosSorteos_Cantidad", "[Cantidad] BETWEEN 1 AND 99");
                    table.CheckConstraint("CK_PremiosSorteos_Tipo", "[Tipo] IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_PremiosSorteos_ProductosSorteo_ProductoSorteoId",
                        column: x => x.ProductoSorteoId,
                        principalTable: "ProductosSorteo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PremiosSorteos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PremiosSorteos_ServiciosGenericos_ServicioId",
                        column: x => x.ServicioId,
                        principalTable: "ServiciosGenericos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PremiosSorteos_Sorteos_SorteoId",
                        column: x => x.SorteoId,
                        principalTable: "Sorteos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PremiosSorteos_ProductoId",
                table: "PremiosSorteos",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiosSorteos_ProductoSorteoId",
                table: "PremiosSorteos",
                column: "ProductoSorteoId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiosSorteos_ServicioId",
                table: "PremiosSorteos",
                column: "ServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiosSorteos_SorteoId",
                table: "PremiosSorteos",
                column: "SorteoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosSorteo_Activo_Nombre",
                table: "ProductosSorteo",
                columns: new[] { "Activo", "Nombre" });

            // Los sorteos que ya había tenían un solo premio, en las columnas de Sorteos: pasa a ser su primer premio. Las
            // columnas antiguas se conservan con sus datos (no se borra nada); los sorteos nuevos las dejan a nulo. «Sin
            // premio» es la marca que deja Down en los sorteos que no tenían ninguno: no es un premio.
            migrationBuilder.Sql("""
                INSERT INTO PremiosSorteos (SorteoId, Tipo, ServicioId, ProductoId, ProductoSorteoId, Nombre, NombreEn, Descripcion, DescripcionEn, Cantidad)
                SELECT s.Id, s.TipoPremio,
                       CASE WHEN s.TipoPremio = 0 THEN s.ServicioId END,
                       CASE WHEN s.TipoPremio = 1 THEN s.ProductoId END,
                       NULL, s.Premio, s.PremioEn, NULL, NULL, 1
                FROM Sorteos s
                WHERE s.TipoPremio IN (0, 1) AND s.Premio IS NOT NULL
                  AND NOT (s.TipoPremio = 0 AND s.ServicioId IS NULL AND s.Premio = N'Sin premio')
                  AND NOT EXISTS (SELECT 1 FROM PremiosSorteos p WHERE p.SorteoId = s.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Vuelta atrás: los sorteos creados con esta versión recuperan su primer premio en las columnas antiguas (un
            // producto de sorteo queda como producto sin enlace, con su nombre); los que no tenían premio, «Sin premio».
            migrationBuilder.Sql("""
                UPDATE s SET TipoPremio = CASE WHEN p.Tipo = 0 THEN 0 ELSE 1 END,
                             ServicioId = CASE WHEN p.Tipo = 0 THEN p.ServicioId END,
                             ProductoId = CASE WHEN p.Tipo = 1 THEN p.ProductoId END,
                             Premio = p.Nombre, PremioEn = p.NombreEn
                FROM Sorteos s
                CROSS APPLY (SELECT TOP (1) x.Tipo, x.ServicioId, x.ProductoId, x.Nombre, x.NombreEn FROM PremiosSorteos x WHERE x.SorteoId = s.Id ORDER BY x.Id) p
                WHERE s.Premio IS NULL OR s.TipoPremio IS NULL;
                UPDATE Sorteos SET TipoPremio = 0, ServicioId = NULL, ProductoId = NULL, Premio = N'Sin premio'
                WHERE Premio IS NULL OR TipoPremio IS NULL;
                """);

            migrationBuilder.DropTable(
                name: "PremiosSorteos");

            migrationBuilder.DropTable(
                name: "ProductosSorteo");

            migrationBuilder.DropColumn(
                name: "EntregadoEn",
                table: "ExtraccionesSorteos");

            migrationBuilder.AlterColumn<int>(
                name: "TipoPremio",
                table: "Sorteos",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Premio",
                table: "Sorteos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldNullable: true);
        }
    }
}
