using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// La categoría del producto deja de ser un texto libre y pasa a ser una entidad obligatoria con borrado lógico.
    /// Sin pérdida de datos: cada categoría escrita se convierte en una fila de CategoriasProductos y los productos sin
    /// categoría van a «Sin categoría»; solo después se quita la columna de texto. El SQL va en EXEC porque usa columnas
    /// que se crean en esta misma migración (así también funciona en un script idempotente).
    /// </summary>
    public partial class CategoriasDeProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoriasProductos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EliminadaEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasProductos", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "CategoriaId",
                table: "Productos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ArchivadoConCategoria",
                table: "Productos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Una categoría por cada texto distinto (la intercalación no distingue mayúsculas: «champús» y «Champús» son la misma),
            // «Sin categoría» para los que no tenían, y cada producto apuntando a la suya
            migrationBuilder.Sql("""
                EXEC(N'
                INSERT INTO CategoriasProductos (Nombre, Activa, CreadaEn)
                SELECT MIN(LTRIM(RTRIM(Categoria))), 1, MIN(CreadoEn)
                FROM Productos
                WHERE Categoria IS NOT NULL AND LTRIM(RTRIM(Categoria)) <> N''''
                GROUP BY LTRIM(RTRIM(Categoria));

                IF NOT EXISTS (SELECT 1 FROM CategoriasProductos WHERE Nombre = N''Sin categoría'')
                    INSERT INTO CategoriasProductos (Nombre, Activa, CreadaEn)
                    SELECT N''Sin categoría'', 1, MIN(CreadoEn)
                    FROM Productos
                    WHERE Categoria IS NULL OR LTRIM(RTRIM(Categoria)) = N''''
                    HAVING COUNT(*) > 0;

                UPDATE p SET CategoriaId = c.Id
                FROM Productos AS p
                INNER JOIN CategoriasProductos AS c ON c.Nombre = LTRIM(RTRIM(p.Categoria))
                WHERE p.Categoria IS NOT NULL AND LTRIM(RTRIM(p.Categoria)) <> N'''';

                UPDATE Productos SET CategoriaId = (SELECT Id FROM CategoriasProductos WHERE Nombre = N''Sin categoría'')
                WHERE CategoriaId IS NULL;
                ')
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CategoriaId",
                table: "Productos",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Productos");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CategoriaId",
                table: "Productos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasProductos_Nombre",
                table: "CategoriasProductos",
                column: "Nombre",
                unique: true,
                filter: "[Activa] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_CategoriasProductos_CategoriaId",
                table: "Productos",
                column: "CategoriaId",
                principalTable: "CategoriasProductos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Productos",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            // Vuelve el texto; los archivados con su categoría se quedan archivados (el texto libre no tenía borrado lógico)
            migrationBuilder.Sql("""
                EXEC(N'
                UPDATE p SET Categoria = NULLIF(c.Nombre, N''Sin categoría'')
                FROM Productos AS p
                INNER JOIN CategoriasProductos AS c ON c.Id = p.CategoriaId;
                ')
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_CategoriasProductos_CategoriaId",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_CategoriaId",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "ArchivadoConCategoria",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "Productos");

            migrationBuilder.DropTable(
                name: "CategoriasProductos");
        }
    }
}
