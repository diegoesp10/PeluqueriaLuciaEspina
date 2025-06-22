using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PelqueriaLuciaEspina.Migrations
{
    /// <inheritdoc />
    public partial class ImagenAdjunta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagenRuta",
                table: "CortesPelo",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagenRuta",
                table: "CortesPelo");
        }
    }
}
