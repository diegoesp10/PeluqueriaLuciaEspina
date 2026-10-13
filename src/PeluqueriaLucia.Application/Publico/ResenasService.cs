using System.Security.Cryptography;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Seguridad;
using PeluqueriaLucia.Domain.Publico;

namespace PeluqueriaLucia.Application.Publico;

public sealed record OpinionPublica(Guid Id, string Alias, string Comentario, int Estrellas, string? Foto, DateTime Fecha, string? Respuesta);
public sealed record OpinionesPublicas(Pagina<OpinionPublica> Pagina, int Total, double Media);
public sealed class ResenasService(IContenidoRepositorio repositorio, IVisitaRepositorio visitas, IProtectorSecretos secretos,
    IFotosResenas fotos, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;
    public async Task<string?> CodigoTicketAsync(int visitaId, CancellationToken ct = default)
    {
        var visita = await visitas.ObtenerAsync(visitaId, ct);
        if (visita?.EstaCobrada != true) return null;
        var permiso = await repositorio.PermisoVisitaAsync(visitaId, ct);
        if (permiso is not null) return secretos.Leer(permiso.CodigoProtegido, "ticket-resena");
        var codigo = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        permiso = await repositorio.GuardarPermisoTicketAsync(PermisoResena.Crear(visitaId, MfaService.Hash(codigo), secretos.Proteger(codigo, "ticket-resena"), Ahora), ct);
        return secretos.Leer(permiso.CodigoProtegido, "ticket-resena");
    }
    private async Task<PermisoResena?> VerificarCodigoAsync(string codigo, CancellationToken ct)
    {
        codigo = MfaService.NormalizarRecuperacion(codigo);
        if (codigo.Length != 32 || codigo.Any(c => !char.IsAsciiHexDigit(c))) return null;
        var permiso = await repositorio.PermisoCodigoAsync(MfaService.Hash(codigo), ct);
        if (permiso is null || !permiso.Activo(Ahora)) return null;
        return (await visitas.ObtenerAsync(permiso.VisitaId, ct))?.EstaCobrada == true ? permiso : null;
    }
    public async Task<Resultado> EnviarAsync(string codigo, string alias, string comentario, int estrellas, Stream? foto, CancellationToken ct = default)
    {
        var permiso = await VerificarCodigoAsync(codigo, ct);
        if (permiso is null) return new Error("El código del ticket no es válido, ha caducado o ya se utilizó.", "Codigo");
        try { Resena.Crear(permiso.Id, alias, comentario, estrellas, null, Ahora); }
        catch (Domain.Comun.ReglaDeNegocioException ex) { return new Error(ex.Message); }
        string? nombre = null;
        var guardada = false;
        try
        {
            if (foto is not null)
            {
                var imagen = await fotos.GuardarAsync(foto, ct);
                if (!imagen.Exito) return imagen.Error!;
                nombre = imagen.Valor;
            }
            repositorio.Agregar(Resena.Crear(permiso.Id, alias, comentario, estrellas, nombre, Ahora));
            permiso.Consumir(Ahora);
            await unidad.GuardarCambiosAsync(ct);
            guardada = true;
            return Resultado.Ok();
        }
        finally { if (!guardada && nombre is not null) fotos.Eliminar(nombre); }
    }
    public async Task<OpinionesPublicas> PublicasAsync(int pagina, CancellationToken ct = default)
    {
        var seleccion = await repositorio.ResenasAsync(EstadoResena.Publicada, pagina, null, ct);
        var r = await repositorio.ValoracionAsync(ct);
        return new(new Pagina<OpinionPublica>(seleccion.Elementos.Select(x => new OpinionPublica(x.Id, x.Alias, x.Comentario, x.Estrellas,
            x.Foto is null ? null : $"/opiniones/foto/{x.Id}", x.CreadaEn, x.Respuesta)).ToList(), seleccion.Numero, seleccion.Tamano, seleccion.TotalElementos), r.Total, r.Media);
    }
    public Task<Pagina<Resena>> AdministrarAsync(EstadoResena? estado, int pagina, string? texto = null, CancellationToken ct = default) => repositorio.ResenasAsync(estado, pagina, texto, ct);
    public Task<Resena?> ObtenerAsync(Guid id, CancellationToken ct = default) => repositorio.ResenaAsync(id, ct);
    public async Task<Resultado> ModerarAsync(Guid id, EstadoResena estado, string? respuesta, CancellationToken ct = default)
    {
        var resena = await repositorio.ResenaAsync(id, ct);
        if (resena is null) return Error.NoEncontrado("La reseña");
        return await Resultado.ProtegerAsync(async () => { resena.Moderar(estado, respuesta); await unidad.GuardarCambiosAsync(ct); });
    }
}
