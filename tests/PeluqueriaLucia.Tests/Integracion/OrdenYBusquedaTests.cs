using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Publico;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Tests.Integracion;

/// <summary>Orden de la web con flechas y buscadores por palabras (LIKE) del panel.</summary>
public sealed class OrdenYBusquedaTests
{
    private static ContenidoService Contenido(PeluqueriaDbContext db) => new(new ContenidoRepositorio(db), new ServicioRepositorio(db), db);

    [Fact]
    public async Task La_migracion_deja_posiciones_seguidas_sin_tocar_el_orden_anterior()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync("20261013142539_CompatibilidadBackupTickets");
        await bd.EjecutarAsync("UPDATE CategoriasPublicas SET Orden = 50 WHERE Slug = 'combinados'; UPDATE ServiciosGenericos SET OrdenPublico = 7 WHERE CategoriaPublicaId IS NOT NULL;");
        string[] antes;
        await using (var db = bd.Contexto()) antes = (await new ContenidoRepositorio(db).CategoriasAsync(default)).Select(c => c.Slug).ToArray();

        await using (var db = bd.Contexto()) await db.Database.MigrateAsync();

        await using var final = bd.Contexto();
        var categorias = await new ContenidoRepositorio(final).CategoriasAsync(default);
        Assert.Equal(antes, categorias.Select(c => c.Slug));
        Assert.Equal(Enumerable.Range(1, categorias.Count), categorias.Select(c => c.Orden));
        foreach (var grupo in (await final.Servicios.AsNoTracking().Where(s => s.CategoriaPublicaId != null).ToListAsync()).GroupBy(s => s.CategoriaPublicaId))
            Assert.Equal(Enumerable.Range(1, grupo.Count()), grupo.Select(s => s.OrdenPublico).Order());
    }

    [Fact]
    public async Task Las_flechas_mueven_un_puesto_y_renumeran()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        int primera, segunda;
        await using (var db = bd.Contexto())
        {
            var lista = await new ContenidoRepositorio(db).CategoriasAsync(default);
            (primera, segunda) = (lista[0].Id, lista[1].Id);
            Assert.True((await Contenido(db).MoverCategoriaAsync(segunda, -1)).Exito);
            Assert.True((await Contenido(db).MoverCategoriaAsync(segunda, -1)).Exito); // ya es la primera: no cambia
        }
        await using (var db = bd.Contexto())
        {
            var lista = await new ContenidoRepositorio(db).CategoriasAsync(default);
            Assert.Equal(new[] { segunda, primera }, lista.Take(2).Select(c => c.Id));
            Assert.Equal(Enumerable.Range(1, lista.Count), lista.Select(c => c.Orden));
        }

        await using (var db = bd.Contexto())
        {
            var servicios = (await Contenido(db).TratamientosAsync(false)).GroupBy(t => t.CategoriaId).First(g => g.Count() >= 3).ToList();
            Assert.True((await Contenido(db).MoverTratamientoAsync(servicios[0].Id, 1)).Exito);
            var despues = (await Contenido(db).TratamientosAsync(false)).Where(t => t.CategoriaId == servicios[0].CategoriaId).ToList();
            Assert.Equal(new[] { servicios[1].Id, servicios[0].Id, servicios[2].Id }, despues.Take(3).Select(t => t.Id));
            Assert.Equal(Enumerable.Range(1, despues.Count), despues.Select(t => t.Orden));
        }
    }

    [Fact]
    public async Task Categoria_nueva_entra_la_ultima()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await using var db = bd.Contexto();
        Assert.True((await Contenido(db).GuardarCategoriaAsync(null, "Novias")).Exito);
        var lista = await new ContenidoRepositorio(db).CategoriasAsync(default);
        Assert.Equal("Novias", lista[^1].Nombre);
        Assert.Equal(lista[^2].Orden + 1, lista[^1].Orden);
    }

    [Fact]
    public async Task Buscadores_por_palabras_con_like_y_comodines_escapados()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await using var db = bd.Contexto();
        PeluqueriaLucia.Application.Abstracciones.IServicioRepositorio servicios = new ServicioRepositorio(db);

        var tinte = await servicios.BuscarAsync("tinte eco", null, true);
        Assert.NotEmpty(tinte);
        Assert.All(tinte, s => Assert.Contains("ecol", s.Nombre, StringComparison.OrdinalIgnoreCase));
        // Los comodines de LIKE se buscan literalmente: «_» no encuentra nada y «%» solo «Color 100 % plantas»
        Assert.Empty(await servicios.BuscarAsync("_", null, true));
        Assert.All(await servicios.BuscarAsync("%", null, true), s => Assert.Contains("%", s.Nombre));
        Assert.Equal((await servicios.ListarAsync(null, true)).Count, (await servicios.BuscarAsync("  ", null, true)).Count);
        Assert.NotEmpty(await Contenido(db).BuscarTratamientosAsync("tinte"));

        var cliente = PeluqueriaLucia.Domain.Clientes.Cliente.Crear("Marta", "Prueba", null, null, PeluqueriaLucia.Domain.Comun.Sexo.Mujer, DateTime.Now);
        var visita = PeluqueriaLucia.Domain.Visitas.Visita.Registrar(cliente, DateTime.Now, PeluqueriaLucia.Domain.Visitas.FormaPago.Efectivo, [tinte[0]]);
        db.Add(cliente); db.Add(visita); await db.SaveChangesAsync();
        var permiso = PermisoResena.Crear(visita.Id, "hash-prueba", "protegido", DateTime.UtcNow);
        db.Add(permiso); db.Resenas.Add(Resena.Crear(permiso.Id, "Marta", "Me encantó el color vegetal", 5, null, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var contenido = new ContenidoRepositorio(db);
        Assert.Single((await contenido.ResenasAsync(null, 1, "color marta", default)).Elementos);
        Assert.Empty((await contenido.ResenasAsync(null, 1, "rizos", default)).Elementos);
    }
}
