using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CatalogoInicialCompleto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            using var recurso = typeof(CatalogoInicialCompleto).Assembly.GetManifestResourceStream("CatalogoInicial20261010")
                ?? throw new InvalidOperationException("Falta el catálogo inicial incrustado.");
            using var lector = new StreamReader(recurso);
            var json = lector.ReadToEnd().Replace("'", "''");
            migrationBuilder.Sql($"""
                DECLARE @catalogo nvarchar(max) = N'{json}';
                INSERT INTO CategoriasPublicas (Nombre, Slug, Orden)
                SELECT DISTINCT j.Categoria, j.CategoriaSlug, j.CategoriaOrden
                FROM OPENJSON(@catalogo) WITH (Categoria nvarchar(80), CategoriaSlug varchar(80), CategoriaOrden int) j
                WHERE j.CategoriaSlug IS NOT NULL AND NOT EXISTS (SELECT 1 FROM CategoriasPublicas c WHERE c.Slug=j.CategoriaSlug);
                INSERT INTO ServiciosGenericos (Nombre, Precio, Sexo, ImagenRuta, Activo, FechaCreacion, CategoriaPublicaId,
                    DescripcionPublica, TipoPrecioPublico, DuracionMinutos, OrdenPublico, Publicado, Destacado)
                SELECT j.Nombre, j.Precio, j.Sexo, j.ImagenRuta, j.Activo, j.FechaCreacion, c.Id,
                    j.DescripcionPublica, j.TipoPrecioPublico, j.DuracionMinutos, j.OrdenPublico, j.Publicado, j.Destacado
                FROM OPENJSON(@catalogo) WITH (Nombre nvarchar(100), Precio decimal(18,2), Sexo int, ImagenRuta nvarchar(400),
                    Activo bit, FechaCreacion datetime2, CategoriaSlug varchar(80), DescripcionPublica nvarchar(1000),
                    TipoPrecioPublico int, DuracionMinutos int, OrdenPublico int, Publicado bit, Destacado bit) j
                LEFT JOIN CategoriasPublicas c ON c.Slug=j.CategoriaSlug
                WHERE NOT EXISTS (SELECT 1 FROM ServiciosGenericos s WHERE s.Nombre COLLATE Latin1_General_CI_AI = j.Nombre COLLATE Latin1_General_CI_AI);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No borrar servicios: pueden estar referenciados por visitas reales.
        }
    }
}
