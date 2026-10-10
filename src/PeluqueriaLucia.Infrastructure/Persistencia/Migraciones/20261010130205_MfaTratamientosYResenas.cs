using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MfaTratamientosYResenas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecretoMfaProtegido",
                table: "Administradores",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UltimoPasoMfa",
                table: "Administradores",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "VersionSeguridad",
                table: "Administradores",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CategoriasPublicas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Slug = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasPublicas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DesafiosMfa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdministradorId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Ip = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: false),
                    NuevoSecretoProtegido = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ContrasenaEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VersionSeguridad = table.Column<int>(type: "int", nullable: false),
                    ExpiraEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumidoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesafiosMfa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesafiosMfa_Administradores_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermisosResenas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitaId = table.Column<int>(type: "int", nullable: false),
                    CodigoHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CodigoProtegido = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ExpiraEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsadoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisosResenas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermisosResenas_ServiciosClientes_VisitaId",
                        column: x => x.VisitaId,
                        principalTable: "ServiciosClientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecuperacionesMfa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdministradorId = table.Column<int>(type: "int", nullable: false),
                    Hash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    UsadoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecuperacionesMfa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecuperacionesMfa_Administradores_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TratamientosPublicos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TipoPrecio = table.Column<int>(type: "int", nullable: false),
                    DuracionMinutos = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Publicado = table.Column<bool>(type: "bit", nullable: false),
                    Destacado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TratamientosPublicos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TratamientosPublicos_CategoriasPublicas_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "CategoriasPublicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Resenas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermisoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    Estrellas = table.Column<int>(type: "int", nullable: false),
                    Foto = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    CreadaEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Respuesta = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resenas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resenas_PermisosResenas_PermisoId",
                        column: x => x.PermisoId,
                        principalTable: "PermisosResenas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasPublicas_Slug",
                table: "CategoriasPublicas",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DesafiosMfa_AdministradorId",
                table: "DesafiosMfa",
                column: "AdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_DesafiosMfa_ExpiraEn",
                table: "DesafiosMfa",
                column: "ExpiraEn");

            migrationBuilder.CreateIndex(
                name: "IX_PermisosResenas_CodigoHash",
                table: "PermisosResenas",
                column: "CodigoHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermisosResenas_VisitaId",
                table: "PermisosResenas",
                column: "VisitaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecuperacionesMfa_AdministradorId",
                table: "RecuperacionesMfa",
                column: "AdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_RecuperacionesMfa_Hash",
                table: "RecuperacionesMfa",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resenas_Estado_CreadaEn",
                table: "Resenas",
                columns: new[] { "Estado", "CreadaEn" });

            migrationBuilder.CreateIndex(
                name: "IX_Resenas_PermisoId",
                table: "Resenas",
                column: "PermisoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TratamientosPublicos_CategoriaId",
                table: "TratamientosPublicos",
                column: "CategoriaId");
            migrationBuilder.Sql("UPDATE SesionesAdministrador SET CerradaEn = SYSUTCDATETIME() WHERE CerradaEn IS NULL");
            using var documento = System.Text.Json.JsonDocument.Parse("""
[
  {
    "Nombre": "Combinados",
    "Servicios": [
      {
        "Nombre": "Peinar y cortar",
        "Precio": "33,90 €",
        "Duracion": "1 h",
        "Destacado": true
      },
      {
        "Nombre": "Tinte y peinar",
        "Precio": "61,46 €",
        "Duracion": "2 h 10 min",
        "Detalle": "Tinte, lavar y marcar, mascarilla"
      },
      {
        "Nombre": "Tinte ecológico y peinar",
        "Precio": "75,59 €",
        "Duracion": "2 h 15 min",
        "Detalle": "Tinte ecológico, lavar y marcar, multimasking",
        "Destacado": true
      },
      {
        "Nombre": "Todo mechas",
        "Precio": "Consultar",
        "Duracion": "3 h 55 min",
        "Detalle": "Mechas, lavar y marcar, cortar, matizar, agua micelar y multimasking"
      },
      {
        "Nombre": "Permanente, cortar, hidratar y peinar",
        "Precio": "66,90 €",
        "Duracion": "2 h"
      }
    ]
  },
  {
    "Nombre": "Peluquería",
    "Servicios": [
      {
        "Nombre": "Cortar",
        "Precio": "20,00 €",
        "Duracion": "30 min",
        "Destacado": true
      },
      {
        "Nombre": "Corte de rizos en seco",
        "Precio": "30,00 €",
        "Duracion": "30 min"
      },
      {
        "Nombre": "Lavar y marcar",
        "Precio": "13,90 €",
        "Duracion": "30 min"
      },
      {
        "Nombre": "Lavar y marcar media melena",
        "Precio": "17,50 €",
        "Duracion": "30 min"
      },
      {
        "Nombre": "Lavar y marcar melena",
        "Precio": "19,90 €",
        "Duracion": "40 min"
      },
      {
        "Nombre": "Tinte",
        "Precio": "42,26 €",
        "Duracion": "1 h 30 min"
      },
      {
        "Nombre": "Tinte ecológico",
        "Precio": "51,59 €",
        "Duracion": "1 h 30 min",
        "Destacado": true
      },
      {
        "Nombre": "Color 100 % plantas",
        "Precio": "67,90 €",
        "Duracion": "2 h 15 min",
        "Destacado": true
      },
      {
        "Nombre": "Mechas",
        "Precio": "Consultar",
        "Duracion": "1 h 30 min"
      },
      {
        "Nombre": "Pocas mechas",
        "Precio": "Consultar",
        "Duracion": "1 h 30 min"
      },
      {
        "Nombre": "Matizar mechas",
        "Precio": "27,00 €",
        "Duracion": "30 min"
      },
      {
        "Nombre": "Gloss, baño de color",
        "Precio": "19,00 €",
        "Duracion": "45 min"
      },
      {
        "Nombre": "Color fantasía",
        "Precio": "25,00 €",
        "Duracion": "40 min"
      },
      {
        "Nombre": "Mascarilla",
        "Precio": "5,30 €",
        "Duracion": "10 min"
      },
      {
        "Nombre": "Multimasking",
        "Precio": "6,50 €",
        "Duracion": "15 min"
      },
      {
        "Nombre": "Agua micelar",
        "Precio": "desde 9,00 €",
        "Duracion": "30 min"
      },
      {
        "Nombre": "Gotas de alergia / Olaplex",
        "Precio": "5,50 €",
        "Duracion": "45 min"
      },
      {
        "Nombre": "Ampolla caída / hidratación",
        "Precio": "15,47 €",
        "Duracion": "30 min"
      }
    ]
  },
  {
    "Nombre": "Caballero",
    "Servicios": [
      {
        "Nombre": "Corte caballero",
        "Precio": "12,00 €",
        "Duracion": "30 min",
        "Destacado": true
      },
      {
        "Nombre": "Corte solo máquina",
        "Precio": "10,00 €",
        "Duracion": "15 min"
      },
      {
        "Nombre": "Arreglo de barba",
        "Precio": "5,00 €",
        "Duracion": "15 min"
      }
    ]
  },
  {
    "Nombre": "Recogidos",
    "Servicios": [
      {
        "Nombre": "Semirrecogido",
        "Precio": "30,00 €",
        "Duracion": "1 h"
      },
      {
        "Nombre": "Recogido",
        "Precio": "36,00 €",
        "Duracion": "1 h 30 min"
      }
    ]
  },
  {
    "Nombre": "Tratamientos",
    "Servicios": [
      {
        "Nombre": "Oxigenación",
        "Precio": "35,00 €",
        "Duracion": "30 min"
      },
      {
        "Nombre": "Oxigenación exprés",
        "Precio": "12,00 €",
        "Duracion": "15 min"
      },
      {
        "Nombre": "Oxidación terapéutica",
        "Precio": "45,00 €",
        "Duracion": "45 min"
      },
      {
        "Nombre": "Spa trufa blanca, regeneración",
        "Precio": "33,38 €",
        "Duracion": "40 min"
      },
      {
        "Nombre": "Tratamiento profundo intensivo",
        "Precio": "20,00 €",
        "Duracion": "15 min"
      }
    ]
  },
  {
    "Nombre": "Estética",
    "Servicios": [
      {
        "Nombre": "Manicura",
        "Precio": "10,00 €",
        "Duracion": "30 min"
      },
      {
        "Nombre": "Pedicura",
        "Precio": "18,00 €",
        "Duracion": "40 min"
      },
      {
        "Nombre": "Limar y cortar",
        "Precio": "4,00 €",
        "Duracion": "20 min"
      },
      {
        "Nombre": "Quitar semipermanente o gel",
        "Precio": "6,00 €",
        "Duracion": "30 min"
      },
      {
        "Nombre": "Tinte de cejas",
        "Precio": "9,47 €",
        "Duracion": "30 min"
      },
      {
        "Nombre": "Depilación con hilo",
        "Precio": "9,00 €",
        "Duracion": "30 min"
      },
      {
        "Nombre": "Maquillaje de evento",
        "Precio": "26,00 €",
        "Duracion": "45 min"
      },
      {
        "Nombre": "Maquillaje de novia",
        "Precio": "35,00 €",
        "Duracion": "1 h 15 min"
      }
    ]
  }
]
""");
            var categoriaId = 0; var tratamientoId = 0;
            foreach (var categoria in documento.RootElement.EnumerateArray())
            {
                categoriaId++;
                var nombre = categoria.GetProperty("Nombre").GetString()!;
                var slug = new string(nombre.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD).Where(char.IsAsciiLetterOrDigit).ToArray());
                migrationBuilder.InsertData("CategoriasPublicas", new[] { "Id", "Nombre", "Slug", "Orden" }, new object[] { categoriaId, nombre, slug, categoriaId * 10 });
                var orden = 0;
                foreach (var tratamiento in categoria.GetProperty("Servicios").EnumerateArray())
                {
                    tratamientoId++; orden++;
                    var precioTexto = tratamiento.GetProperty("Precio").GetString()!;
                    var tipo = precioTexto == "Consultar" ? 2 : precioTexto.StartsWith("desde") ? 1 : 0;
                    object precio = tipo == 2 ? null! : decimal.Parse(precioTexto.Replace("desde", "").Replace("€", "").Trim(), System.Globalization.CultureInfo.GetCultureInfo("es-ES"));
                    var duracion = tratamiento.GetProperty("Duracion").GetString()!;
                    var numeros = System.Text.RegularExpressions.Regex.Matches(duracion, @"\d+").Select(m => int.Parse(m.Value)).ToArray();
                    var minutos = duracion.Contains('h') ? numeros[0] * 60 + (numeros.Length > 1 ? numeros[1] : 0) : numeros[0];
                    migrationBuilder.InsertData("TratamientosPublicos", new[] { "Id", "CategoriaId", "Nombre", "Descripcion", "Precio", "TipoPrecio", "DuracionMinutos", "Orden", "Publicado", "Destacado" },
                        new object[] { tratamientoId, categoriaId, tratamiento.GetProperty("Nombre").GetString()!, tratamiento.TryGetProperty("Detalle", out var detalle) ? detalle.GetString()! : null!, precio, tipo, minutos, orden * 10, true, tratamiento.TryGetProperty("Destacado", out var destacado) && destacado.GetBoolean() });
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DesafiosMfa");

            migrationBuilder.DropTable(
                name: "RecuperacionesMfa");

            migrationBuilder.DropTable(
                name: "Resenas");

            migrationBuilder.DropTable(
                name: "TratamientosPublicos");

            migrationBuilder.DropTable(
                name: "PermisosResenas");

            migrationBuilder.DropTable(
                name: "CategoriasPublicas");

            migrationBuilder.DropColumn(
                name: "SecretoMfaProtegido",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "UltimoPasoMfa",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "VersionSeguridad",
                table: "Administradores");
        }
    }
}

