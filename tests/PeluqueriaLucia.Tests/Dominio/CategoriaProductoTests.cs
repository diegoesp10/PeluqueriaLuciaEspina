using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;

namespace PeluqueriaLucia.Tests.Dominio;

public sealed class CategoriaProductoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 22, 10, 0, 0);

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("Una categoría con un nombre larguísimo que pasa de los sesenta caracteres permitidos")]
    public void El_nombre_es_obligatorio_y_tiene_un_maximo(string? nombre)
    {
        var ex = Assert.Throws<ReglaDeNegocioException>(() => CategoriaProducto.Crear(nombre, Ahora));
        Assert.Equal("Nombre", ex.Campo);
    }

    [Fact]
    public void Eliminarla_archiva_con_su_stock_los_productos_a_la_venta_y_recuperarla_solo_devuelve_esos()
    {
        var categoria = ProductoTests.Categoria(1);
        var aLaVenta = Guardado(1, categoria, stock: 6);
        var otroALaVenta = Guardado(2, categoria, stock: 0);
        var archivadoAntes = Guardado(3, categoria, stock: 2);
        archivadoAntes.Archivar(Ahora);
        var deOtra = Guardado(4, ProductoTests.Categoria(2, "Mascarillas"), stock: 1);
        var todos = new[] { aLaVenta, otroALaVenta, archivadoAntes, deOtra };

        var archivados = categoria.Eliminar(todos, Ahora.AddHours(1));

        Assert.Equal(2, archivados);
        Assert.Equal((false, Ahora.AddHours(1)), (categoria.Activa, categoria.EliminadaEn));
        Assert.Equal([(false, true, 6), (false, true, 0), (false, false, 2)],
            new[] { aLaVenta, otroALaVenta, archivadoAntes }.Select(p => (p.Activo, p.ArchivadoConCategoria, p.Stock)));
        Assert.True(deOtra.Activo);
        Assert.Throws<ReglaDeNegocioException>(() => categoria.Eliminar(todos, Ahora));

        var recuperados = categoria.Recuperar(todos, Ahora.AddDays(1));

        Assert.Equal(2, recuperados);
        Assert.Equal((true, null), (categoria.Activa, categoria.EliminadaEn));
        Assert.Equal([(true, false), (true, false), (false, false)],
            new[] { aLaVenta, otroALaVenta, archivadoAntes }.Select(p => (p.Activo, p.ArchivadoConCategoria)));
        Assert.Throws<ReglaDeNegocioException>(() => categoria.Recuperar(todos, Ahora));
    }

    [Fact]
    public void Con_la_categoria_eliminada_el_producto_no_vuelve_a_la_venta_hasta_cambiarla_o_recuperarla()
    {
        var champus = ProductoTests.Categoria(1);
        var producto = Guardado(1, champus);
        champus.Eliminar([producto], Ahora);

        var ex = Assert.Throws<ReglaDeNegocioException>(() => producto.Reactivar(Ahora));
        Assert.Equal("CategoriaId", ex.Campo);

        // Se puede editar sin tocar su categoría, pero no pasar a otra eliminada ni crear un producto en ella
        producto.Editar(ProductoTests.Ficha("REF-1", "Champú renombrado"), champus, Ahora);
        Assert.Equal(("Champú renombrado", true), (producto.Nombre, producto.ArchivadoConCategoria));
        Assert.Equal("CategoriaId", Assert.Throws<ReglaDeNegocioException>(() => Producto.Crear(ProductoTests.Ficha("NUEVO"), champus, Ahora)).Campo);

        // Con otra categoría deja de depender de la eliminada: queda archivado sin más y se puede volver a vender
        producto.Editar(ProductoTests.Ficha("REF-1", "Champú renombrado"), ProductoTests.Categoria(2, "Champús sólidos"), Ahora);
        Assert.Equal((2, false, false), (producto.CategoriaId, producto.ArchivadoConCategoria, producto.Activo));
        producto.Reactivar(Ahora);
        Assert.True(producto.Activo);
    }

    private static Producto Guardado(int id, CategoriaProducto categoria, int stock = 3)
    {
        var producto = Producto.Crear(ProductoTests.Ficha($"REF-{id}"), categoria, Ahora);
        typeof(Producto).GetProperty(nameof(Producto.Id))!.SetValue(producto, id);
        producto.Alta(stock, Ahora, null);
        return producto;
    }
}
