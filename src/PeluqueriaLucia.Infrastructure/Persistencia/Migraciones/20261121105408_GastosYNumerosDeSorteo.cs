using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// Gastos del salón (tabla nueva) y números en los sorteos: cada participante lleva un número único en su sorteo y puede
    /// no tener ficha (números metidos a mano). Solo añade: los participantes que ya había reciben los números 1, 2, 3… en
    /// el orden en que se añadieron, y las extracciones, el número de su ganador.
    /// </summary>
    public partial class GastosYNumerosDeSorteo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParticipantesSorteos_SorteoId_ClienteId",
                table: "ParticipantesSorteos");

            migrationBuilder.AlterColumn<int>(
                name: "ClienteId",
                table: "ParticipantesSorteos",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Nombre",
                table: "ParticipantesSorteos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Numero",
                table: "ParticipantesSorteos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Numero",
                table: "ExtraccionesSorteos",
                type: "int",
                nullable: true);

            // Los participantes que ya había: 1, 2, 3… dentro de cada sorteo, en el orden en que se añadieron
            migrationBuilder.Sql("""
                UPDATE p SET Numero = x.Orden
                FROM ParticipantesSorteos AS p
                INNER JOIN (SELECT Id, ROW_NUMBER() OVER (PARTITION BY SorteoId ORDER BY Id) AS Orden FROM ParticipantesSorteos) AS x ON x.Id = p.Id;
                """);

            // Las extracciones ya hechas: el número de su ganador (si sigue en el sorteo)
            migrationBuilder.Sql("""
                UPDATE e SET Numero = p.Numero
                FROM ExtraccionesSorteos AS e
                INNER JOIN ParticipantesSorteos AS p ON p.SorteoId = e.SorteoId AND p.ClienteId = e.ClienteId;
                """);

            migrationBuilder.CreateTable(
                name: "Gastos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Concepto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MedioPago = table.Column<int>(type: "int", nullable: false),
                    Nota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gastos", x => x.Id);
                    table.CheckConstraint("CK_Gastos_Importe", "[Importe] > 0");
                    table.CheckConstraint("CK_Gastos_MedioPago", "[MedioPago] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Gastos_Tipo", "[Tipo] BETWEEN 1 AND 14 OR [Tipo] = 99");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesSorteos_SorteoId_ClienteId",
                table: "ParticipantesSorteos",
                columns: new[] { "SorteoId", "ClienteId" },
                unique: true,
                filter: "[ClienteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesSorteos_SorteoId_Numero",
                table: "ParticipantesSorteos",
                columns: new[] { "SorteoId", "Numero" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ParticipantesSorteos_Numero",
                table: "ParticipantesSorteos",
                sql: "[Numero] BETWEEN 1 AND 99999");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_Fecha",
                table: "Gastos",
                column: "Fecha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Gastos");

            // Sin números, los participantes sin ficha no caben en la tabla de antes
            migrationBuilder.Sql("DELETE FROM ParticipantesSorteos WHERE ClienteId IS NULL;");

            migrationBuilder.DropIndex(
                name: "IX_ParticipantesSorteos_SorteoId_ClienteId",
                table: "ParticipantesSorteos");

            migrationBuilder.DropIndex(
                name: "IX_ParticipantesSorteos_SorteoId_Numero",
                table: "ParticipantesSorteos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ParticipantesSorteos_Numero",
                table: "ParticipantesSorteos");

            migrationBuilder.DropColumn(
                name: "Nombre",
                table: "ParticipantesSorteos");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "ParticipantesSorteos");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "ExtraccionesSorteos");

            migrationBuilder.AlterColumn<int>(
                name: "ClienteId",
                table: "ParticipantesSorteos",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesSorteos_SorteoId_ClienteId",
                table: "ParticipantesSorteos",
                columns: new[] { "SorteoId", "ClienteId" },
                unique: true);
        }
    }
}
