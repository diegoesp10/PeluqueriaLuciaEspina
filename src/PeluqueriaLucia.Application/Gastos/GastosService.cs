using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Gastos;

namespace PeluqueriaLucia.Application.Gastos;

/// <summary>Gastos guardados. Con un filtro de cliente no hay ninguno: los gastos no son de un cliente.</summary>
public interface IGastoRepositorio
{
    Task<Gasto?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>Los del periodo, del primero al último, con su tipo.</summary>
    Task<IReadOnlyList<Gasto>> ListarAsync(FiltroVisitas filtro, CancellationToken ct = default);

    Task<ResumenGastos> ResumirAsync(FiltroVisitas filtro, CancellationToken ct = default);

    /// <summary>Lo pagado en el periodo por tipo, de más a menos.</summary>
    Task<IReadOnlyList<TotalPorTipo>> TotalesPorTipoAsync(FiltroVisitas filtro, CancellationToken ct = default);

    void Agregar(Gasto gasto);

    void Eliminar(Gasto gasto);

    /// <summary>Todos los tipos, activos y archivados, en su orden.</summary>
    Task<IReadOnlyList<TipoGasto>> TiposAsync(CancellationToken ct = default);

    Task<TipoGasto?> TipoAsync(int id, CancellationToken ct = default);

    /// <summary>Cuántos gastos lleva cada tipo (los que no tienen ninguno no salen).</summary>
    Task<IReadOnlyDictionary<int, int>> GastosPorTipoAsync(CancellationToken ct = default);

    void AgregarTipo(TipoGasto tipo);

    void EliminarTipo(TipoGasto tipo);
}

/// <summary>Lo pagado en un periodo: cuántos gastos, cuánto en total y cuánto de eso en efectivo (sale de la caja).</summary>
public sealed record ResumenGastos(int Gastos, decimal Total, decimal Efectivo)
{
    public static ResumenGastos Vacio { get; } = new(0, 0, 0);
}

/// <summary>Lo pagado de un tipo en un periodo.</summary>
public sealed record TotalPorTipo(string Tipo, int Gastos, decimal Total);

/// <summary>Un tipo de gasto con cuántos gastos lo llevan (los que tienen alguno no se pueden borrar).</summary>
public sealed record TipoConUso(TipoGasto Tipo, int Gastos);

/// <summary>
/// Gastos del salón: lo que se paga (personal, cuota de autónomos, dietas, viajes…). Salen en el listado de visitas y en el
/// Excel para el asesor, separados de los cobros. Sus tipos los edita el salón.
/// </summary>
public sealed class GastosService(IGastoRepositorio gastos, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <summary>El orden de «Otros», que va siempre al final; los tipos nuevos se ponen antes.</summary>
    public const int OrdenUltimo = 999;

    /// <summary>Hora del salón, aunque el servidor esté en otra zona.</summary>
    public DateTime Ahora => TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), AgendaService.Zona).DateTime;

    public Task<Gasto?> ObtenerAsync(int id, CancellationToken ct = default) => gastos.ObtenerAsync(id, ct);

    public Task<ResumenGastos> ResumirAsync(FiltroVisitas filtro, CancellationToken ct = default) => gastos.ResumirAsync(filtro, ct);

    public Task<IReadOnlyList<TotalPorTipo>> TotalesPorTipoAsync(FiltroVisitas filtro, CancellationToken ct = default) =>
        gastos.TotalesPorTipoAsync(filtro, ct);

    public Task<Resultado<int>> CrearAsync(DatosGasto datos, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            var gasto = Gasto.Crear(datos, await gastos.TipoAsync(datos.TipoId, ct), Ahora);
            gastos.Agregar(gasto);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(gasto.Id);
        });

    public async Task<Resultado> EditarAsync(int id, DatosGasto datos, CancellationToken ct = default)
    {
        var gasto = await gastos.ObtenerAsync(id, ct);
        if (gasto is null)
            return Error.NoEncontrado("El gasto");
        var tipo = await gastos.TipoAsync(datos.TipoId, ct);
        return await Resultado.ProtegerAsync(async () =>
        {
            gasto.Editar(datos, tipo, Ahora);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    public async Task<Resultado> EliminarAsync(int id, CancellationToken ct = default)
    {
        var gasto = await gastos.ObtenerAsync(id, ct);
        if (gasto is null)
            return Error.NoEncontrado("El gasto");
        return await Resultado.ProtegerAsync(async () =>
        {
            gastos.Eliminar(gasto);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    // ── Tipos de gasto ──────────────────────────────────────────────────────

    /// <summary>Todos los tipos, activos y archivados, en su orden.</summary>
    public Task<IReadOnlyList<TipoGasto>> TiposAsync(CancellationToken ct = default) => gastos.TiposAsync(ct);

    /// <summary>Los tipos con cuántos gastos lleva cada uno: primero los activos, después los archivados.</summary>
    public async Task<IReadOnlyList<TipoConUso>> TiposConUsoAsync(CancellationToken ct = default)
    {
        var tipos = await gastos.TiposAsync(ct);
        var uso = await gastos.GastosPorTipoAsync(ct);
        return [.. tipos.OrderBy(t => !t.Activo).Select(t => new TipoConUso(t, uso.GetValueOrDefault(t.Id)))];
    }

    public async Task<Resultado<int>> CrearTipoAsync(string? nombre, CancellationToken ct = default)
    {
        var tipos = await gastos.TiposAsync(ct);
        if (tipos.Count >= TipoGasto.Maximo)
            return new Error($"Como mucho puede haber {TipoGasto.Maximo} tipos: borra o renombra alguno que no uses.", "Nombre");
        if (Igual(tipos, nombre, null) is { } igual)
            return new Error($"Ya hay un tipo «{igual.Nombre}»{(igual.Activo ? "" : " (archivado: vuelve a activarlo)")}.", "Nombre");
        var orden = tipos.Where(t => t.Orden < OrdenUltimo).Select(t => t.Orden).DefaultIfEmpty(0).Max() + 1;
        return await Resultado.ProtegerAsync<int>(async () =>
        {
            var tipo = TipoGasto.Crear(nombre, orden, Ahora);
            gastos.AgregarTipo(tipo);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(tipo.Id);
        });
    }

    /// <summary>Le cambia el nombre: los gastos que ya lo llevan salen con el nombre nuevo (también en el Excel).</summary>
    public async Task<Resultado> RenombrarTipoAsync(int id, string? nombre, CancellationToken ct = default)
    {
        var tipos = await gastos.TiposAsync(ct);
        var tipo = await gastos.TipoAsync(id, ct);
        if (tipo is null)
            return Error.NoEncontrado("El tipo de gasto");
        if (Igual(tipos, nombre, id) is { } igual)
            return new Error($"Ya hay otro tipo «{igual.Nombre}».", "Nombre");
        return await Resultado.ProtegerAsync(async () =>
        {
            tipo.Renombrar(nombre, Ahora);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    /// <summary>Lo archiva (deja de salir al anotar un gasto) o lo vuelve a activar.</summary>
    public async Task<Resultado> ArchivarTipoAsync(int id, bool archivar, CancellationToken ct = default)
    {
        var tipo = await gastos.TipoAsync(id, ct);
        if (tipo is null)
            return Error.NoEncontrado("El tipo de gasto");
        if (archivar && tipo.Activo && (await gastos.TiposAsync(ct)).Count(t => t.Activo) == 1)
            return new Error("Tiene que quedar al menos un tipo activo para poder anotar gastos.");
        if (archivar)
            tipo.Archivar(Ahora);
        else
            tipo.Activar(Ahora);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return Resultado.Ok();
    }

    /// <summary>Borra un tipo que no lleva ningún gasto; uno con gastos solo se puede archivar.</summary>
    public async Task<Resultado> EliminarTipoAsync(int id, CancellationToken ct = default)
    {
        var tipo = await gastos.TipoAsync(id, ct);
        if (tipo is null)
            return Error.NoEncontrado("El tipo de gasto");
        var usados = (await gastos.GastosPorTipoAsync(ct)).GetValueOrDefault(id);
        if (usados > 0)
            return new Error($"«{tipo.Nombre}» lo llevan {(usados == 1 ? "1 gasto" : $"{usados} gastos")}: no se puede borrar, pero sí archivar para que no salga en los gastos nuevos.");
        if (tipo.Activo && (await gastos.TiposAsync(ct)).Count(t => t.Activo) == 1)
            return new Error("Tiene que quedar al menos un tipo activo para poder anotar gastos.");
        gastos.EliminarTipo(tipo);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return Resultado.Ok();
    }

    /// <summary>Otro tipo con el mismo nombre (sin distinguir mayúsculas ni espacios de más).</summary>
    private static TipoGasto? Igual(IReadOnlyList<TipoGasto> tipos, string? nombre, int? excepto) =>
        nombre?.Trim() is { Length: > 0 } buscado
            ? tipos.FirstOrDefault(t => t.Id != excepto && string.Equals(t.Nombre.Trim(), buscado, StringComparison.CurrentCultureIgnoreCase))
            : null;
}
