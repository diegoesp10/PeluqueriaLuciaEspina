using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeluqueriaLucia.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CatalogoUnificadoConWeb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TratamientosPublicos_CategoriasPublicas_CategoriaId",
                table: "TratamientosPublicos");

            migrationBuilder.AddColumn<int>(
                name: "CategoriaPublicaId",
                table: "ServiciosGenericos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescripcionPublica",
                table: "ServiciosGenericos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Destacado",
                table: "ServiciosGenericos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DuracionMinutos",
                table: "ServiciosGenericos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrdenPublico",
                table: "ServiciosGenericos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Publicado",
                table: "ServiciosGenericos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TipoPrecioPublico",
                table: "ServiciosGenericos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ServiciosGenericos_CategoriaPublicaId",
                table: "ServiciosGenericos",
                column: "CategoriaPublicaId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiciosGenericos_CategoriasPublicas_CategoriaPublicaId",
                table: "ServiciosGenericos",
                column: "CategoriaPublicaId",
                principalTable: "CategoriasPublicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                EXEC(N'
                UPDATE ServiciosGenericos SET DuracionMinutos = 30;
                IF EXISTS(SELECT 1 FROM TratamientosPublicos WHERE Precio > 9999.99)
                    THROW 51000, ''Revisa los precios públicos superiores al máximo del catálogo antes de migrar.'', 1;
                DECLARE @categoria int, @nombre nvarchar(100), @detalle nvarchar(1000), @precio decimal(18,2),
                    @tipo int, @minutos int, @orden int, @publicado bit, @destacado bit, @sexo int, @servicio int;
                DECLARE cuidados CURSOR LOCAL FAST_FORWARD FOR
                    SELECT t.CategoriaId,t.Nombre,t.Descripcion,t.Precio,t.TipoPrecio,t.DuracionMinutos,t.Orden,t.Publicado,t.Destacado,
                        CASE WHEN c.Slug = ''caballero'' THEN 0 WHEN c.Slug = ''estetica'' THEN 2 ELSE 1 END
                    FROM TratamientosPublicos t JOIN CategoriasPublicas c ON c.Id=t.CategoriaId ORDER BY t.Id;
                OPEN cuidados;
                FETCH NEXT FROM cuidados INTO @categoria,@nombre,@detalle,@precio,@tipo,@minutos,@orden,@publicado,@destacado,@sexo;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @servicio = NULL;
                    SELECT TOP(1) @servicio=Id FROM ServiciosGenericos
                        WHERE Activo=1 AND LTRIM(RTRIM(Nombre)) COLLATE Latin1_General_100_CI_AI = LTRIM(RTRIM(@nombre)) COLLATE Latin1_General_100_CI_AI
                        ORDER BY Id;
                    IF @servicio IS NULL
                    BEGIN
                        INSERT INTO ServiciosGenericos(Nombre,Precio,Sexo,ImagenRuta,Activo,FechaCreacion,Publicado,Destacado,DuracionMinutos,OrdenPublico,TipoPrecioPublico)
                            VALUES(@nombre,COALESCE(@precio,0),@sexo,NULL,1,SYSUTCDATETIME(),0,0,30,0,0);
                        SET @servicio=CONVERT(int,SCOPE_IDENTITY());
                    END;
                    UPDATE ServiciosGenericos SET CategoriaPublicaId=@categoria,DescripcionPublica=@detalle,TipoPrecioPublico=@tipo,
                        DuracionMinutos=@minutos,OrdenPublico=@orden,Publicado=@publicado,Destacado=@destacado WHERE Id=@servicio;
                    FETCH NEXT FROM cuidados INTO @categoria,@nombre,@detalle,@precio,@tipo,@minutos,@orden,@publicado,@destacado,@sexo;
                END;
                CLOSE cuidados; DEALLOCATE cuidados;
                ');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiciosGenericos_CategoriasPublicas_CategoriaPublicaId",
                table: "ServiciosGenericos");

            migrationBuilder.DropIndex(
                name: "IX_ServiciosGenericos_CategoriaPublicaId",
                table: "ServiciosGenericos");

            migrationBuilder.DropColumn(
                name: "CategoriaPublicaId",
                table: "ServiciosGenericos");

            migrationBuilder.DropColumn(
                name: "DescripcionPublica",
                table: "ServiciosGenericos");

            migrationBuilder.DropColumn(
                name: "Destacado",
                table: "ServiciosGenericos");

            migrationBuilder.DropColumn(
                name: "DuracionMinutos",
                table: "ServiciosGenericos");

            migrationBuilder.DropColumn(
                name: "OrdenPublico",
                table: "ServiciosGenericos");

            migrationBuilder.DropColumn(
                name: "Publicado",
                table: "ServiciosGenericos");

            migrationBuilder.DropColumn(
                name: "TipoPrecioPublico",
                table: "ServiciosGenericos");

            migrationBuilder.AddForeignKey(
                name: "FK_TratamientosPublicos_CategoriasPublicas_CategoriaId",
                table: "TratamientosPublicos",
                column: "CategoriaId",
                principalTable: "CategoriasPublicas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
