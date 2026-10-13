using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Application.Visitas;

public sealed class VisitasService(
    IVisitaRepositorio visitas,
    IClienteRepositorio clientes,
    IServicioRepositorio servicios,
    IExportadorVisitas exportador,
    IUnidadDeTrabajo unidadDeTrabajo)
{
    public const int TamanoPagina = 30;

    public Task<Pagina<Visita>> ListarAsync(FiltroVisitas filtro, int pagina, CancellationToken ct = default) =>
        visitas.ListarAsync(filtro, Pagina<Visita>.Normalizar(pagina), TamanoPagina, ct);

    public Task<ResumenVisitas> ResumirAsync(FiltroVisitas filtro, CancellationToken ct = default) =>
        visitas.ResumirAsync(filtro, ct);

    public Task<Visita?> ObtenerAsync(int id, CancellationToken ct = default) => visitas.ObtenerAsync(id, ct);

    public Task<Resultado<int>> RegistrarAsync(NuevaVisita datos, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            var cliente = await clientes.ObtenerAsync(datos.ClienteId, ct);
            if (cliente is null)
                return new Error("Elige un cliente.", "ClienteId");

            var ids = datos.ServicioIds.Distinct().ToList();
            var elegidos = await servicios.ObtenerVariosAsync(ids, ct);
            if (elegidos.Count != ids.Count)
                return new Error("Alguno de los servicios elegidos ya no existe. Vuelve a cargar la página.", "Servicios");

            // Se respeta el orden en que se marcaron para que el ticket salga igual que en pantalla
            var ordenados = ids.Select(id => elegidos.First(s => s.Id == id)).ToList();
            var visita = Visita.Registrar(cliente, datos.Fecha, datos.FormaPago, ordenados, datos.Precios);
            visitas.Agregar(visita);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(visita.Id);
        });

    public async Task<Resultado> CobrarAsync(int id, FormaPago formaPago, decimal? efectivo, decimal? tarjeta, CancellationToken ct = default)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return Error.NoEncontrado("La visita");

        return await Resultado.ProtegerAsync(async () =>
        {
            visita.Cobrar(formaPago, efectivo, tarjeta);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    public async Task<Resultado> EliminarAsync(int id, CancellationToken ct = default)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return Error.NoEncontrado("La visita");

        return await Resultado.ProtegerAsync(async () =>
        {
            visita.ComprobarQueSePuedeEliminar();
            visitas.Eliminar(visita);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    public async Task<Resultado<ArchivoGenerado>> ExportarAsync(FiltroVisitas filtro, CancellationToken ct = default)
    {
        var seleccion = await visitas.ListarTodasAsync(filtro, ct);
        if (seleccion.Count == 0)
            return new Error($"No hay visitas que exportar ({filtro.Descripcion}).");

        return Resultado.Ok(exportador.Exportar(seleccion, filtro.Descripcion));
    }
}
