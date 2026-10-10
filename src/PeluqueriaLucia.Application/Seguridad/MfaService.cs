using System.Security.Cryptography;
using System.Text;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Seguridad;

namespace PeluqueriaLucia.Application.Seguridad;

public sealed record DesafioIniciado(Guid Id, string Token);
public sealed record DesafioPresentado(string Usuario, string? Secreto);
public sealed record AccesoMfaCompletado(SesionIniciada Sesion, IReadOnlyList<string> Recuperacion);

public sealed class MfaService(AccesoService acceso, IAdministradorRepositorio administradores, IMfaRepositorio mfa,
    ISesionRepositorio sesiones, IProtectorSecretos secretos, IUnidadDeTrabajo unidad, TimeProvider reloj)
{
    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;
    public static string Hash(string valor) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
    public static string NormalizarRecuperacion(string valor) => valor.Replace("-", "").Replace(" ", "").ToUpperInvariant();

    public async Task<Resultado<DesafioIniciado>> IniciarAsync(string usuario, string contrasena, Origen origen, CancellationToken ct = default)
    {
        var comprobacion = await acceso.VerificarContrasenaAsync(usuario, contrasena, origen, ct);
        if (!comprobacion.Exito) return comprobacion.Error!;
        return await CrearDesafioAsync(comprobacion.Valor!, origen, reemplazar: false, ct);
    }

    private async Task<Resultado<DesafioIniciado>> CrearDesafioAsync(Administrador administrador, Origen origen, bool reemplazar, CancellationToken ct)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var nuevo = !administrador.MfaActivo || reemplazar ? secretos.Proteger(secretos.GenerarSecretoMfa(), "totp") : null;
        var desafio = DesafioMfa.Crear(administrador.Id, Hash(token), origen.Ip, nuevo, administrador.ContrasenaCambiadaEn, administrador.VersionSeguridad, Ahora);
        mfa.Agregar(desafio);
        await unidad.GuardarCambiosAsync(ct);
        return Resultado.Ok(new DesafioIniciado(desafio.Id, token));
    }

    private async Task<(DesafioMfa Desafio, Administrador Administrador)?> ObtenerAsync(Guid id, string token, string ip, CancellationToken ct)
    {
        var desafio = await mfa.ObtenerAsync(id, ct);
        if (desafio is null || !desafio.Activo(Ahora) || desafio.Ip != ip ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(desafio.TokenHash), Encoding.ASCII.GetBytes(Hash(token)))) return null;
        var administrador = await administradores.ObtenerAsync(desafio.AdministradorId, ct);
        if (administrador is null || administrador.EstaBloqueado(Ahora) || administrador.VersionSeguridad != desafio.VersionSeguridad) return null;
        return (desafio, administrador);
    }

    public async Task<DesafioPresentado?> PresentarAsync(Guid id, string token, string ip, CancellationToken ct = default)
    {
        var datos = await ObtenerAsync(id, token, ip, ct);
        if (datos is null) return null;
        var nuevo = datos.Value.Desafio.NuevoSecretoProtegido;
        return new(datos.Value.Administrador.Usuario, nuevo is null ? null : secretos.Leer(nuevo, "totp"));
    }

    public async Task<Resultado<AccesoMfaCompletado>> CompletarAsync(Guid id, string token, string codigo, Origen origen, CancellationToken ct = default)
    {
        var datos = await ObtenerAsync(id, token, origen.Ip, ct);
        if (datos is null) return new Error("La verificación ha caducado. Vuelve a introducir tu contraseña.");
        var (desafio, administrador) = datos.Value;
        var protegido = desafio.NuevoSecretoProtegido ?? administrador.SecretoMfaProtegido;
        var secreto = protegido is null ? null : secretos.Leer(protegido, "totp");
        var paso = secreto is null ? null : secretos.VerificarTotp(secreto, codigo ?? "", Ahora);
        var valido = paso is not null && (desafio.NuevoSecretoProtegido is not null || administrador.ConsumirPasoMfa(paso.Value));
        if (!valido && desafio.NuevoSecretoProtegido is null)
        {
            var hash = Hash(NormalizarRecuperacion(codigo ?? ""));
            var recuperacion = (await mfa.CodigosAsync(administrador.Id, ct)).FirstOrDefault(x =>
                CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(x.Hash), Encoding.ASCII.GetBytes(hash)));
            if (recuperacion is not null) { recuperacion.Consumir(Ahora); valido = true; }
        }
        if (!valido)
        {
            desafio.Fallar(); administrador.RegistrarFallo(Ahora); await unidad.GuardarCambiosAsync(ct);
            return new Error("Código incorrecto o ya utilizado. Espera al siguiente código de la app.", "Codigo");
        }
        var codigos = new List<string>();
        if (desafio.NuevoSecretoProtegido is not null)
        {
            // Una activación confirmada deja sin efecto otros QR pendientes y todas las sesiones anteriores.
            administrador.ActivarMfa(desafio.NuevoSecretoProtegido, paso!.Value);
            administrador.CambiarContrasena(administrador.HashContrasena, Ahora);
            foreach (var anterior in await sesiones.ListarAbiertasAsync(administrador.Id, Ahora, ct)) anterior.Cerrar(Ahora);
            foreach (var anterior in await mfa.CodigosAsync(administrador.Id, ct)) anterior.Consumir(Ahora);
            for (var i = 0; i < 10; i++)
            {
                var codigoNuevo = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                mfa.Agregar(RecuperacionMfa.Crear(administrador.Id, Hash(codigoNuevo)));
                codigos.Add(string.Join('-', Enumerable.Range(0, 8).Select(n => codigoNuevo.Substring(n * 4, 4))));
            }
        }
        desafio.Consumir(Ahora);
        var sesion = await acceso.AbrirSesionMfaAsync(administrador, origen, ct);
        return Resultado.Ok(new AccesoMfaCompletado(sesion, codigos));
    }

    public async Task<Resultado<DesafioIniciado>> ReconfigurarAsync(int id, string contrasena, string codigo, Origen origen, CancellationToken ct = default)
    {
        var administrador = await administradores.ObtenerAsync(id, ct);
        if (administrador is null) return Error.NoEncontrado("La cuenta");
        var comprobacion = await acceso.VerificarContrasenaAsync(administrador.Usuario, contrasena, origen, ct);
        if (!comprobacion.Exito) return comprobacion.Error!;
        var secreto = administrador.SecretoMfaProtegido is { } s ? secretos.Leer(s, "totp") : null;
        var paso = secreto is not null ? secretos.VerificarTotp(secreto, codigo, Ahora) : null;
        var valido = paso is not null && administrador.ConsumirPasoMfa(paso.Value);
        if (!valido)
        {
            var hash = Hash(NormalizarRecuperacion(codigo));
            var reserva = (await mfa.CodigosAsync(id, ct)).FirstOrDefault(x => x.Hash == hash);
            if (reserva is not null) { reserva.Consumir(Ahora); valido = true; }
        }
        if (!valido) { administrador.RegistrarFallo(Ahora); await unidad.GuardarCambiosAsync(ct); return new Error("Código incorrecto o ya utilizado.", "Codigo"); }
        return await CrearDesafioAsync(administrador, origen, reemplazar: true, ct);
    }

    // Solo lo invoca la herramienta de consola del servidor; no existe un endpoint web para esta recuperación.
    public async Task<Resultado> RecuperarDesdeConsolaAsync(string usuario, string contrasena, CancellationToken ct = default)
    {
        var comprobacion = await acceso.VerificarContrasenaAsync(usuario, contrasena, new Origen("consola-local", "Recuperación en el servidor"), ct);
        if (!comprobacion.Exito) return comprobacion.Error!;
        var administrador = comprobacion.Valor!;
        administrador.RestablecerMfa();
        foreach (var sesion in await sesiones.ListarAbiertasAsync(administrador.Id, Ahora, ct)) sesion.Cerrar(Ahora);
        foreach (var codigo in await mfa.CodigosAsync(administrador.Id, ct)) codigo.Consumir(Ahora);
        await unidad.GuardarCambiosAsync(ct);
        return Resultado.Ok();
    }
}
