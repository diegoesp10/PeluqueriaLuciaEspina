using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    /// <summary>
    /// Textos en inglés para la web pública: nombre y descripción de los servicios, nombre de las categorías y respuesta
    /// del salón a las reseñas. Solo añade columnas opcionales: lo que ya existe sigue en español y se traduce cuando se pueda.
    /// </summary>
    public partial class TextosEnIngles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescripcionPublicaEn",
                table: "ServiciosGenericos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreEn",
                table: "ServiciosGenericos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RespuestaEn",
                table: "Resenas",
                type: "nvarchar(1500)",
                maxLength: 1500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreEn",
                table: "CategoriasPublicas",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescripcionPublicaEn",
                table: "ServiciosGenericos");

            migrationBuilder.DropColumn(
                name: "NombreEn",
                table: "ServiciosGenericos");

            migrationBuilder.DropColumn(
                name: "RespuestaEn",
                table: "Resenas");

            migrationBuilder.DropColumn(
                name: "NombreEn",
                table: "CategoriasPublicas");
        }
    }
}
