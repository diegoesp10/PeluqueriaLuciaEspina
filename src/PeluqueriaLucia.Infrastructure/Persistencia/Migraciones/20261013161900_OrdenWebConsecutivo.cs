using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Solo datos: deja el orden de la web como posiciones seguidas (1, 2, 3…) en lugar de los 10, 20, 30… o los
    /// repetidos que había. Respeta el orden en que se veían (mismo Orden y, a igualdad, el Id). No borra nada ni
    /// cambia el esquema.
    /// </summary>
    public partial class OrdenWebConsecutivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH posiciones AS (
                    SELECT Orden, ROW_NUMBER() OVER (ORDER BY Orden, Id) AS Posicion FROM CategoriasPublicas)
                UPDATE posiciones SET Orden = Posicion;
                """);

            migrationBuilder.Sql("""
                WITH posiciones AS (
                    SELECT OrdenPublico, ROW_NUMBER() OVER (PARTITION BY CategoriaPublicaId ORDER BY OrdenPublico, Id) AS Posicion
                    FROM ServiciosGenericos WHERE CategoriaPublicaId IS NOT NULL)
                UPDATE posiciones SET OrdenPublico = Posicion;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Las posiciones seguidas siguen siendo un orden válido: no hay nada que deshacer.
        }
    }
}
