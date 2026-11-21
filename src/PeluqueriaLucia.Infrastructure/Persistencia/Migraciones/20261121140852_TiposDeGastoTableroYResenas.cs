using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Tipos de gasto editables, fechas de las reseñas y talonario del sorteo. Solo añade: la tabla «TiposGasto» nace con los
    /// tipos que había (los mismos números, del 1 al 14 y el 99, «Otros»), así que cada gasto conserva el suyo; la columna
    /// «Gastos.Tipo» pasa a llamarse «TipoId» con sus valores intactos. Las reseñas y los sorteos ganan columnas que admiten
    /// vacío. No borra ni cambia ningún dato.
    /// </summary>
    public partial class TiposDeGastoTableroYResenas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Gastos_Tipo",
                table: "Gastos");

            migrationBuilder.RenameColumn(
                name: "Tipo",
                table: "Gastos",
                newName: "TipoId");

            migrationBuilder.AddColumn<int>(
                name: "NumerosHasta",
                table: "Sorteos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublicadaEn",
                table: "Resenas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevisadaEn",
                table: "Resenas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TiposGasto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposGasto", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_TipoId",
                table: "Gastos",
                column: "TipoId");

            migrationBuilder.CreateIndex(
                name: "IX_TiposGasto_Nombre",
                table: "TiposGasto",
                column: "Nombre",
                unique: true);

            // Los tipos que había, con sus mismos números: antes de la clave ajena, para que todos los gastos encuentren el suyo
            migrationBuilder.Sql("""
                DECLARE @ahora datetime2 = CONVERT(datetime2, SYSDATETIMEOFFSET() AT TIME ZONE 'Romance Standard Time');
                SET IDENTITY_INSERT [TiposGasto] ON;
                INSERT INTO [TiposGasto] ([Id], [Nombre], [Activo], [Orden], [CreadoEn], [ActualizadoEn]) VALUES
                    (1, N'Personal (nóminas y Seguridad Social)', 1, 1, @ahora, @ahora),
                    (2, N'Cuota de autónomos', 1, 2, @ahora, @ahora),
                    (3, N'Dietas', 1, 3, @ahora, @ahora),
                    (4, N'Viajes y desplazamientos', 1, 4, @ahora, @ahora),
                    (5, N'Material y productos', 1, 5, @ahora, @ahora),
                    (6, N'Suministros (luz, agua, teléfono…)', 1, 6, @ahora, @ahora),
                    (7, N'Alquiler', 1, 7, @ahora, @ahora),
                    (8, N'Asesoría y profesionales', 1, 8, @ahora, @ahora),
                    (9, N'Formación', 1, 9, @ahora, @ahora),
                    (10, N'Publicidad', 1, 10, @ahora, @ahora),
                    (11, N'Reparaciones y mantenimiento', 1, 11, @ahora, @ahora),
                    (12, N'Seguros', 1, 12, @ahora, @ahora),
                    (13, N'Impuestos y tasas', 1, 13, @ahora, @ahora),
                    (14, N'Comisiones del banco', 1, 14, @ahora, @ahora),
                    (99, N'Otros', 1, 999, @ahora, @ahora);
                SET IDENTITY_INSERT [TiposGasto] OFF;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Gastos_TiposGasto_TipoId",
                table: "Gastos",
                column: "TipoId",
                principalTable: "TiposGasto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lo que la versión anterior no conoce: los tipos añadidos pasan a «Otros» y las reseñas ocultas, a rechazadas
            migrationBuilder.Sql("UPDATE [Gastos] SET [TipoId] = 99 WHERE [TipoId] NOT BETWEEN 1 AND 14 AND [TipoId] <> 99;");
            migrationBuilder.Sql("UPDATE [Resenas] SET [Estado] = 2 WHERE [Estado] = 3;");

            migrationBuilder.DropForeignKey(
                name: "FK_Gastos_TiposGasto_TipoId",
                table: "Gastos");

            migrationBuilder.DropTable(
                name: "TiposGasto");

            migrationBuilder.DropIndex(
                name: "IX_Gastos_TipoId",
                table: "Gastos");

            migrationBuilder.DropColumn(
                name: "NumerosHasta",
                table: "Sorteos");

            migrationBuilder.DropColumn(
                name: "PublicadaEn",
                table: "Resenas");

            migrationBuilder.DropColumn(
                name: "RevisadaEn",
                table: "Resenas");

            migrationBuilder.RenameColumn(
                name: "TipoId",
                table: "Gastos",
                newName: "Tipo");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Gastos_Tipo",
                table: "Gastos",
                sql: "[Tipo] BETWEEN 1 AND 14 OR [Tipo] = 99");
        }
    }
}
