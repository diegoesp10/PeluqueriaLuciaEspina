using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;

namespace PeluqueriaLucia.Application.Productos;

/// <summary>
/// Inventario de la tienda: fichas con referencia y fotos, y el stock, que solo cambia con movimientos (alta, entradas,
/// uso en el salón, recuentos y las ventas, que registra <see cref="Visitas.VisitasService"/> al cobrar).
/// </summary>
public sealed class ProductosService(
    IProductoRepositorio productos,
    IFotosProductos fotos,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider reloj)
{
    public const int TamanoPagina = 30;
    public const int TamanoHistorial = 20;

    private DateTime Ahora => reloj.GetLocalNow().DateTime;

    public Task<Pagina<Producto>> BuscarAsync(FiltroProductos filtro, int pagina, CancellationToken ct = default) =>
        productos.BuscarAsync(filtro, Pagina<Producto>.Normalizar(pagina), TamanoPagina, ct);

    public Task<ResumenInventario> ResumirAsync(CancellationToken ct = default) => productos.ResumirAsync(ct);

    public Task<Producto?> ObtenerAsync(int id, CancellationToken ct = default) => productos.ObtenerAsync(id, ct);

    public Task<Pagina<MovimientoStock>> MovimientosAsync(int id, int pagina, CancellationToken ct = default) =>
        productos.MovimientosAsync(id, Pagina<MovimientoStock>.Normalizar(pagina), TamanoHistorial, ct);

    /// <summary>Productos que se pueden añadir a una visita.</summary>
    public Task<IReadOnlyList<Producto>> ParaVentaAsync(CancellationToken ct = default) => productos.ListarActivosAsync(ct);

    public Task<IReadOnlyList<Producto>> PorReponerAsync(int maximo, CancellationToken ct = default) => productos.PorReponerAsync(maximo, ct);

    public Task<IReadOnlyList<string>> MarcasAsync(CancellationToken ct = default) => productos.MarcasAsync(ct);

    /// <summary>Categorías que se pueden elegir (las activas).</summary>
    public Task<IReadOnlyList<CategoriaProducto>> CategoriasActivasAsync(CancellationToken ct = default) => productos.CategoriasAsync(false, ct);

    public Task<IReadOnlyList<ResumenCategoria>> CategoriasAsync(CancellationToken ct = default) => productos.ResumenCategoriasAsync(ct);

    public Task<Resultado<int>> CrearCategoriaAsync(string? nombre, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            var categoria = CategoriaProducto.Crear(nombre, Ahora);
            if (await productos.CategoriaActivaPorNombreAsync(categoria.Nombre, null, ct) is not null)
                return new Error($"Ya hay una categoría «{categoria.Nombre}».", "Nombre");
            productos.AgregarCategoria(categoria);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(categoria.Id);
        });

    public async Task<Resultado> RenombrarCategoriaAsync(int id, string? nombre, CancellationToken ct = default)
    {
        var categoria = await productos.CategoriaAsync(id, ct);
        if (categoria is null)
            return Error.NoEncontrado("La categoría");

        return await Resultado.ProtegerAsync(async () =>
        {
            categoria.Renombrar(nombre);
            if (categoria.Activa && await productos.CategoriaActivaPorNombreAsync(categoria.Nombre, id, ct) is not null)
                throw new ReglaDeNegocioException($"Ya hay otra categoría «{categoria.Nombre}».", "Nombre");
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    /// <summary>Borrado lógico de la categoría: sus productos a la venta quedan archivados con su stock. Devuelve cuántos.</summary>
    public async Task<Resultado<CambioCategoria>> EliminarCategoriaAsync(int id, CancellationToken ct = default)
    {
        var categoria = await productos.CategoriaAsync(id, ct);
        if (categoria is null)
            return Error.NoEncontrado("La categoría");

        return await Resultado.ProtegerAsync<CambioCategoria>(async () =>
        {
            var archivados = categoria.Eliminar(await productos.ProductosDeCategoriaAsync(id, ct), Ahora);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(new CambioCategoria(categoria.Nombre, archivados));
        });
    }

    /// <summary>Recupera la categoría y vuelve a poner a la venta los productos archivados con ella. Devuelve cuántos.</summary>
    public async Task<Resultado<CambioCategoria>> RecuperarCategoriaAsync(int id, CancellationToken ct = default)
    {
        var categoria = await productos.CategoriaAsync(id, ct);
        if (categoria is null)
            return Error.NoEncontrado("La categoría");

        return await Resultado.ProtegerAsync<CambioCategoria>(async () =>
        {
            if (await productos.CategoriaActivaPorNombreAsync(categoria.Nombre, id, ct) is not null)
                return new Error($"Ya hay otra categoría «{categoria.Nombre}» activa. Renombra una de las dos antes de recuperar esta.", "Nombre");
            var recuperados = categoria.Recuperar(await productos.ProductosDeCategoriaAsync(id, ct), Ahora);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(new CambioCategoria(categoria.Nombre, recuperados));
        });
    }

    public string? RutaFoto(Guid id, bool miniatura) => fotos.RutaFisica(id, miniatura);

    public Task<Resultado<int>> CrearAsync(DatosProducto datos, IReadOnlyList<ArchivoImagen> nuevasFotos, int? administradorId, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            if (ValidarFotos(nuevasFotos, 0) is { } errorFotos)
                return errorFotos;

            if (await DuplicadoAsync(Producto.Comprobar(datos.Ficha), null, ct) is { } duplicado)
                return duplicado;
            var categoria = await CategoriaAsync(datos.Categoria, null, ct);
            if (!categoria.Exito)
                return categoria.Error!;
            var producto = Producto.Crear(datos.Ficha, categoria.Valor!, Ahora);
            var alta = producto.Alta(datos.StockInicial, Ahora, administradorId);

            var subidas = new List<Guid>();
            try
            {
                if (await SubirAsync(nuevasFotos, subidas, ct) is { } errorSubida)
                    return errorSubida;

                subidas.ForEach(foto => producto.AgregarFoto(foto, Ahora));
                productos.Agregar(producto);
                if (alta is not null)
                    productos.AgregarMovimientos([alta]);
                await unidadDeTrabajo.GuardarCambiosAsync(ct);
            }
            catch
            {
                // Ni la ficha ni las fotos a medias: si no se guarda, los ficheros subidos se borran
                Descartar(subidas);
                throw;
            }

            return Resultado.Ok(producto.Id);
        });

    public Task<Resultado<int>> EditarAsync(int id, FichaProducto ficha, CategoriaElegida categoriaElegida, CambiosFotos cambios, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            var producto = await productos.ObtenerAsync(id, ct);
            if (producto is null)
                return Error.NoEncontrado("El producto");

            var quitar = cambios.Quitar.Where(q => producto.Fotos.Any(f => f.Id == q)).Distinct().ToList();
            if (ValidarFotos(cambios.Nuevas, producto.Fotos.Count - quitar.Count) is { } errorFotos)
                return errorFotos;

            // Todo se comprueba (ficha, duplicados, categoría y fotos) antes de tocar el producto: un error deja la pantalla como estaba
            if (await DuplicadoAsync(Producto.Comprobar(ficha), id, ct) is { } duplicado)
                return duplicado;
            var categoria = await CategoriaAsync(categoriaElegida, producto, ct);
            if (!categoria.Exito)
                return categoria.Error!;

            var subidas = new List<Guid>();
            try
            {
                if (await SubirAsync(cambios.Nuevas, subidas, ct) is { } errorSubida)
                    return errorSubida;

                producto.Editar(ficha, categoria.Valor!, Ahora);
                foreach (var foto in quitar)
                    producto.QuitarFoto(foto, Ahora);
                subidas.ForEach(foto => producto.AgregarFoto(foto, Ahora));
                if (cambios.Principal is { } principal && producto.Fotos.Any(f => f.Id == principal))
                    producto.HacerPrincipal(principal, Ahora);

                await unidadDeTrabajo.GuardarCambiosAsync(ct);
            }
            catch
            {
                Descartar(subidas);
                throw;
            }

            // Los ficheros quitados solo se borran cuando la base de datos ya no los nombra
            Descartar(quitar);
            return Resultado.Ok(producto.Id);
        });

    public async Task<Resultado> ArchivarAsync(int id, bool archivar, CancellationToken ct = default)
    {
        var producto = await productos.ObtenerAsync(id, ct);
        if (producto is null)
            return Error.NoEncontrado("El producto");

        // Reactivar falla si su categoría está eliminada: primero hay que recuperarla o elegir otra
        return await Resultado.ProtegerAsync(async () =>
        {
            if (archivar)
                producto.Archivar(Ahora);
            else
                producto.Reactivar(Ahora);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    /// <summary>Un producto que aparece en visitas se archiva para no perder tickets ni historial; si no, se borra del todo.</summary>
    public async Task<Resultado<Eliminacion>> EliminarAsync(int id, CancellationToken ct = default)
    {
        var producto = await productos.ObtenerAsync(id, ct);
        if (producto is null)
            return Error.NoEncontrado("El producto");

        if (await productos.TieneVentasAsync(id, ct))
        {
            producto.Archivar(Ahora);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(Eliminacion.Archivado);
        }

        var ficheros = producto.Fotos.Select(f => f.Id).ToList();
        productos.Eliminar(producto);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        Descartar(ficheros);
        return Resultado.Ok(Eliminacion.Eliminado);
    }

    /// <summary>Entrada de mercancía, uso en el salón o recuento. Devuelve el stock con el que queda.</summary>
    public async Task<Resultado<int>> RegistrarMovimientoAsync(
        int id, MovimientoManual tipo, int cantidad, string? nota, int? administradorId, CancellationToken ct = default)
    {
        var producto = await productos.ObtenerAsync(id, ct);
        if (producto is null)
            return Error.NoEncontrado("El producto");

        return await Resultado.ProtegerAsync<int>(async () =>
        {
            var movimiento = tipo switch
            {
                MovimientoManual.Entrada => producto.RegistrarEntrada(cantidad, nota, Ahora, administradorId),
                MovimientoManual.UsoInterno => producto.RegistrarUsoInterno(cantidad, nota, Ahora, administradorId),
                MovimientoManual.Recuento => producto.AjustarInventario(cantidad, nota, Ahora, administradorId),
                _ => throw new ReglaDeNegocioException("Elige qué quieres registrar: entrada, uso en el salón o recuento.", "Tipo"),
            };
            productos.AgregarMovimientos([movimiento]);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(producto.Stock);
        });
    }

    private async Task<Error?> DuplicadoAsync((string Referencia, string? CodigoBarras) ficha, int? excluirId, CancellationToken ct)
    {
        if (await productos.ExisteReferenciaAsync(ficha.Referencia, excluirId, ct))
            return new Error($"Ya hay otro producto con la referencia {ficha.Referencia}.", "Referencia");
        if (ficha.CodigoBarras is { } codigo && await productos.ExisteCodigoBarrasAsync(codigo, excluirId, ct))
            return new Error($"Ya hay otro producto con el código de barras {codigo}.", "CodigoBarras");
        return null;
    }

    /// <summary>
    /// La categoría elegida: una existente (puede seguir siendo la eliminada que ya tenía el producto) o, si se escribió un
    /// nombre nuevo, la activa con ese nombre o una que se crea al guardar.
    /// </summary>
    private async Task<Resultado<CategoriaProducto>> CategoriaAsync(CategoriaElegida elegida, Producto? actual, CancellationToken ct)
    {
        if (elegida.Nueva?.Trim() is { Length: > 0 } nombre)
        {
            if (nombre.Length > CategoriaProducto.LongitudNombre)
                return new Error($"El nombre de la categoría no puede superar los {CategoriaProducto.LongitudNombre} caracteres.", "CategoriaId");
            if (await productos.CategoriaActivaPorNombreAsync(nombre, null, ct) is { } existente)
                return Resultado.Ok(existente);
            var nueva = CategoriaProducto.Crear(nombre, Ahora);
            productos.AgregarCategoria(nueva);
            return Resultado.Ok(nueva);
        }

        if (elegida.Id is not { } id)
            return new Error("Elige la categoría del producto o escribe una nueva.", "CategoriaId");
        var categoria = await productos.CategoriaAsync(id, ct);
        if (categoria is null)
            return new Error("Esa categoría ya no existe. Elige otra.", "CategoriaId");
        if (!categoria.Activa && categoria.Id != actual?.CategoriaId)
            return new Error($"La categoría «{categoria.Nombre}» está eliminada. Elige otra o recupérala en Categorías.", "CategoriaId");
        return Resultado.Ok(categoria);
    }

    private static Error? ValidarFotos(IReadOnlyList<ArchivoImagen> nuevas, int existentes)
    {
        if (nuevas.Count == 0)
            return null;
        if (existentes + nuevas.Count > Producto.MaximoFotos)
            return new Error($"Un producto admite hasta {Producto.MaximoFotos} fotos; ahora tendría {existentes + nuevas.Count}.", "Fotos");
        if (nuevas.FirstOrDefault(f => f.Tamano > IFotosProductos.TamanoMaximo) is { } grande)
            return new Error($"«{grande.NombreOriginal}» pesa más de {IFotosProductos.TamanoMaximo / (1024 * 1024)} MB.", "Fotos");
        return null;
    }

    /// <summary>
    /// Procesa y guarda cada foto; sus identificadores quedan en <paramref name="subidas"/>. Si una no vale, borra las ya
    /// guardadas y devuelve el error: o entran todas o ninguna.
    /// </summary>
    private async Task<Error?> SubirAsync(IReadOnlyList<ArchivoImagen> nuevas, List<Guid> subidas, CancellationToken ct)
    {
        foreach (var archivo in nuevas)
        {
            var guardada = await fotos.GuardarAsync(archivo.Contenido, ct);
            if (!guardada.Exito)
            {
                Descartar(subidas);
                subidas.Clear();
                return guardada.Error! with { Mensaje = nuevas.Count == 1 ? guardada.Error.Mensaje : $"«{archivo.NombreOriginal}»: {guardada.Error.Mensaje}" };
            }

            subidas.Add(guardada.Valor);
        }

        return null;
    }

    private void Descartar(IEnumerable<Guid> ids)
    {
        foreach (var id in ids)
            fotos.Eliminar(id);
    }
}
