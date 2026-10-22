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

    public Task<(IReadOnlyList<string> Categorias, IReadOnlyList<string> Marcas)> SugerenciasAsync(CancellationToken ct = default) =>
        productos.SugerenciasAsync(ct);

    public string? RutaFoto(Guid id, bool miniatura) => fotos.RutaFisica(id, miniatura);

    public Task<Resultado<int>> CrearAsync(DatosProducto datos, IReadOnlyList<ArchivoImagen> nuevasFotos, int? administradorId, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            if (ValidarFotos(nuevasFotos, 0) is { } errorFotos)
                return errorFotos;

            var producto = Producto.Crear(datos.Ficha, Ahora);
            if (await DuplicadoAsync(producto, null, ct) is { } duplicado)
                return duplicado;
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

    public Task<Resultado<int>> EditarAsync(int id, FichaProducto ficha, CambiosFotos cambios, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            var producto = await productos.ObtenerAsync(id, ct);
            if (producto is null)
                return Error.NoEncontrado("El producto");

            var quitar = cambios.Quitar.Where(q => producto.Fotos.Any(f => f.Id == q)).Distinct().ToList();
            if (ValidarFotos(cambios.Nuevas, producto.Fotos.Count - quitar.Count) is { } errorFotos)
                return errorFotos;

            // Todo se comprueba (ficha, duplicados y fotos) antes de tocar el producto: un error deja la pantalla como estaba
            var propuesta = Producto.Crear(ficha, Ahora);
            if (await DuplicadoAsync(propuesta, id, ct) is { } duplicado)
                return duplicado;

            var subidas = new List<Guid>();
            try
            {
                if (await SubirAsync(cambios.Nuevas, subidas, ct) is { } errorSubida)
                    return errorSubida;

                producto.Editar(ficha, Ahora);
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

        if (archivar)
            producto.Archivar(Ahora);
        else
            producto.Reactivar(Ahora);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return Resultado.Ok();
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

    private async Task<Error?> DuplicadoAsync(Producto producto, int? excluirId, CancellationToken ct)
    {
        if (await productos.ExisteReferenciaAsync(producto.Referencia, excluirId, ct))
            return new Error($"Ya hay otro producto con la referencia {producto.Referencia}.", "Referencia");
        if (producto.CodigoBarras is { } codigo && await productos.ExisteCodigoBarrasAsync(codigo, excluirId, ct))
            return new Error($"Ya hay otro producto con el código de barras {codigo}.", "CodigoBarras");
        return null;
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
