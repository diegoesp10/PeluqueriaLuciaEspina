using System.Security.Cryptography;
using System.Text;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Seguridad;

namespace PeluqueriaLucia.Application.Seguridad;

/// <summary>Una sesión recién abierta. El token solo existe aquí y en la cookie cifrada del navegador.</summary>
public sealed record SesionIniciada(Guid SesionId, string Token, int AdministradorId, string Usuario, DateTime ExpiraEn);

/// <summary>Sesión comprobada en una petición al panel.</summary>
public sealed record SesionValida(Guid SesionId, int AdministradorId, string Usuario);

/// <summary>Quién hace la petición: se guarda con cada sesión e intento de acceso.</summary>
public sealed record Origen(string Ip, string? Agente);

/// <summary>Entrada al panel, validación del token de sesión en cada llamada, cierre de sesiones y contraseñas.</summary>
public sealed class AccesoService(
    IAdministradorRepositorio administradores,
    ISesionRepositorio sesiones,
    IIntentoAccesoRepositorio intentos,
    IHasherContrasenas hasher,
    IProtectorSecretos secretos,
    IMfaRepositorio mfa,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider reloj)
{
    public const int LongitudMinimaContrasena = 15;

    private const string MensajeCredenciales = "El usuario o la contraseña no son correctos.";

    /// <summary>
    /// Hash de una contraseña que no existe. Si el usuario no existe se verifica contra él igualmente, para que la
    /// respuesta tarde lo mismo y no delate qué usuarios existen.
    /// </summary>

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    public Task<bool> HayAdministradorAsync(CancellationToken ct = default) => administradores.HayAlgunoAsync(ct);

    public async Task<Resultado<SesionIniciada>> IniciarSesionAsync(string usuario, string contrasena, Origen origen, CancellationToken ct = default, string? codigo = null)
    {
        var comprobacion = await VerificarContrasenaAsync(usuario, contrasena, origen, ct);
        if (!comprobacion.Exito)
            return comprobacion.Error!;

        var administrador = comprobacion.Valor!;
        if (administrador.MfaActivo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return new Error("Introduce el código de tu aplicación de autenticación.", "Codigo");
            if (!await EsCodigoMfaValidoAsync(administrador, codigo, ct))
            {
                administrador.RegistrarFallo(Ahora);
                await unidadDeTrabajo.GuardarCambiosAsync(ct);
                return new Error("El código de verificación no es correcto o ya se ha usado.", "Codigo");
            }
        }

        var sesion = await AbrirSesionAsync(administrador, origen, ct);
        await AnotarAsync(administrador.Usuario, origen, exito: true, "Acceso correcto", ct);

        return Resultado.Ok(sesion);
    }

    /// <summary>
    /// Comprueba usuario y contraseña sin abrir sesión. Lo usa el segundo factor (MFA) como primer paso:
    /// solo si la contraseña es correcta se genera el desafío. Anota los intentos fallidos igual que el acceso normal.
    /// </summary>
    public async Task<Resultado<Administrador>> VerificarContrasenaAsync(string usuario, string contrasena, Origen origen, CancellationToken ct = default)
    {
        var nombre = Administrador.NormalizarUsuario(usuario);
        var administrador = nombre.Length == 0 ? null : await administradores.ObtenerPorUsuarioAsync(nombre, ct);

        if (administrador is null)
        {
            hasher.Verificar(hasher.HashSenuelo, contrasena ?? string.Empty);
            await AnotarAsync(nombre, origen, exito: false, "Usuario inexistente", ct);
            return new Error(MensajeCredenciales);
        }

        if (administrador.EstaBloqueado(Ahora))
        {
            hasher.Verificar(hasher.HashSenuelo, contrasena ?? string.Empty);
            await AnotarAsync(nombre, origen, exito: false, "Cuenta bloqueada", ct);
            return new Error(MensajeCredenciales);
        }

        var verificacion = hasher.Verificar(administrador.HashContrasena, contrasena ?? string.Empty);
        if (verificacion == ResultadoVerificacion.Incorrecta)
        {
            administrador.RegistrarFallo(Ahora);
            await AnotarAsync(nombre, origen, exito: false, "Contraseña incorrecta", ct);
            return new Error(MensajeCredenciales);
        }

        if (verificacion == ResultadoVerificacion.CorrectaRecalcular)
            administrador.ActualizarHash(hasher.Calcular(contrasena!));

        return Resultado.Ok(administrador);
    }

    /// <summary>
    /// Abre una sesión cuando el segundo factor (MFA) ya se ha superado. Marca el acceso y limita las sesiones abiertas.
    /// </summary>
    public Task<SesionIniciada> AbrirSesionMfaAsync(Administrador administrador, Origen origen, CancellationToken ct = default) =>
        AbrirSesionAsync(administrador, origen, ct);

    /// <summary>Registra el acceso, cierra las sesiones más antiguas por encima del límite y crea una sesión nueva.</summary>
    private async Task<SesionIniciada> AbrirSesionAsync(Administrador administrador, Origen origen, CancellationToken ct)
    {
        administrador.RegistrarAcceso(Ahora);
        var abiertas = await sesiones.ListarAbiertasAsync(administrador.Id, Ahora, ct);
        foreach (var anterior in abiertas.Skip(9)) anterior.Cerrar(Ahora);
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var sesion = SesionAdministrador.Abrir(administrador.Id, HashToken(token), origen.Ip, origen.Agente, Ahora);
        sesiones.Agregar(sesion);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);

        return new SesionIniciada(sesion.Id, token, administrador.Id, administrador.Usuario, sesion.ExpiraEn);
    }

    /// <summary>
    /// Comprueba el token de sesión de una petición: que exista, que coincida, que no esté cerrada ni caducada.
    /// </summary>
    public async Task<SesionValida?> ValidarSesionAsync(Guid sesionId, string? token, string ip, CancellationToken ct = default, bool registrarUso = true)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        var sesion = await sesiones.ObtenerAsync(sesionId, ct);
        if (sesion is null || !sesion.EstaActiva(Ahora) || !TokenCoincide(token, sesion.TokenHash))
            return null;

        var administrador = await administradores.ObtenerAsync(sesion.AdministradorId, ct);
        if (administrador is null)
            return null;

        if (registrarUso && sesion.RegistrarUso(Ahora, ip))
            await unidadDeTrabajo.GuardarCambiosAsync(ct);

        return new SesionValida(sesion.Id, administrador.Id, administrador.Usuario);
    }

    public async Task CerrarSesionAsync(Guid sesionId, CancellationToken ct = default)
    {
        if (await sesiones.ObtenerAsync(sesionId, ct) is not { } sesion)
            return;
        sesion.Cerrar(Ahora);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
    }

    /// <summary>Cierra una sesión concreta, solo si es del mismo administrador.</summary>
    public async Task<Resultado> CerrarSesionDeAsync(int administradorId, Guid sesionId, CancellationToken ct = default)
    {
        var sesion = await sesiones.ObtenerAsync(sesionId, ct);
        if (sesion is null || sesion.AdministradorId != administradorId)
            return Error.NoEncontrado("La sesión");
        sesion.Cerrar(Ahora);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return Resultado.Ok();
    }

    public async Task<int> CerrarOtrasSesionesAsync(int administradorId, Guid sesionActual, CancellationToken ct = default)
    {
        var otras = (await sesiones.ListarAbiertasAsync(administradorId, Ahora, ct)).Where(s => s.Id != sesionActual).ToList();
        otras.ForEach(s => s.Cerrar(Ahora));
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return otras.Count;
    }

    /// <summary>Cierra todas las sesiones abiertas de un administrador, incluida la actual.</summary>
    public async Task CerrarTodasSesionesAsync(int administradorId, CancellationToken ct = default)
    {
        var abiertas = await sesiones.ListarAbiertasAsync(administradorId, Ahora, ct);
        foreach (var sesion in abiertas) sesion.Cerrar(Ahora);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
    }

    public Task<IReadOnlyList<SesionAdministrador>> ListarSesionesAsync(int administradorId, CancellationToken ct = default) =>
        sesiones.ListarAbiertasAsync(administradorId, Ahora, ct);

    public Task<IReadOnlyList<IntentoAcceso>> UltimosIntentosAsync(int cantidad, CancellationToken ct = default) =>
        intentos.UltimosAsync(cantidad, ct);

    /// <summary>Cambia la contraseña desde el panel y cierra el resto de sesiones abiertas.</summary>
    public async Task<Resultado> CambiarContrasenaAsync(
        int administradorId, Guid sesionActual, string actual, string nueva, CancellationToken ct = default, string? codigo = null)
    {
        var administrador = await administradores.ObtenerAsync(administradorId, ct);
        if (administrador is null)
            return Error.NoEncontrado("El administrador");
        if (administrador.EstaBloqueado(Ahora))
            return new Error("Demasiados intentos. Espera 15 minutos.", "Actual");
        if (hasher.Verificar(administrador.HashContrasena, actual ?? string.Empty) == ResultadoVerificacion.Incorrecta)
        {
            administrador.RegistrarFallo(Ahora);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return new Error("La contraseña actual no es correcta.", "Actual");
        }
        if (ValidarContrasena(administrador.Usuario, nueva) is { } error)
            return error;
        if (administrador.MfaActivo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return new Error("Introduce el código de tu aplicación de autenticación.", "Codigo");
            if (!await EsCodigoMfaValidoAsync(administrador, codigo, ct))
            {
                administrador.RegistrarFallo(Ahora);
                await unidadDeTrabajo.GuardarCambiosAsync(ct);
                return new Error("El código de verificación no es correcto o ya se ha usado.", "Codigo");
            }
        }

        administrador.CambiarContrasena(hasher.Calcular(nueva), Ahora);
        foreach (var sesion in await sesiones.ListarAbiertasAsync(administradorId, Ahora, ct))
        {
            if (sesion.Id != sesionActual)
                sesion.Cerrar(Ahora);
        }

        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return Resultado.Ok();
    }

    private async Task<bool> EsCodigoMfaValidoAsync(Administrador administrador, string codigo, CancellationToken ct)
    {
        var secreto = administrador.SecretoMfaProtegido is { } protegido ? secretos.Leer(protegido, "totp") : null;
        var paso = secreto is not null ? secretos.VerificarTotp(secreto, codigo, Ahora) : null;
        if (paso is not null && administrador.ConsumirPasoMfa(paso.Value))
            return true;

        var hash = MfaService.Hash(MfaService.NormalizarRecuperacion(codigo));
        var recuperacion = (await mfa.CodigosAsync(administrador.Id, ct))
            .FirstOrDefault(c => CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(c.Hash), Encoding.ASCII.GetBytes(hash)));
        if (recuperacion is null)
            return false;

        recuperacion.Consumir(Ahora);
        return true;
    }

    /// <summary>
    /// Crea el administrador, o le pone una contraseña nueva si ya existe (cerrando todas sus sesiones).
    /// Solo se usa desde la línea de comandos del servidor, nunca desde la web.
    /// </summary>
    public async Task<Resultado<bool>> EstablecerAdministradorAsync(string usuario, string contrasena, CancellationToken ct = default)
    {
        var nombre = Administrador.NormalizarUsuario(usuario);
        if (ValidarContrasena(nombre, contrasena) is { } error)
            return error;

        return await Resultado.ProtegerAsync<bool>(async () =>
        {
            var existente = await administradores.ObtenerPorUsuarioAsync(nombre, ct);
            if (existente is null)
            {
                administradores.Agregar(Administrador.Crear(nombre, hasher.Calcular(contrasena), Ahora));
            }
            else
            {
                existente.CambiarContrasena(hasher.Calcular(contrasena), Ahora);
                foreach (var sesion in await sesiones.ListarAbiertasAsync(existente.Id, Ahora, ct))
                    sesion.Cerrar(Ahora);
            }

            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(existente is null);
        });
    }

    public static Error? ValidarContrasena(string usuario, string? contrasena)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < LongitudMinimaContrasena)
            return new Error($"La contraseña debe tener al menos {LongitudMinimaContrasena} caracteres.", "Nueva");
        if (contrasena.Length > 256)
            return new Error("La contraseña no puede superar 256 caracteres.", "Nueva");
        if (contrasena.Contains(usuario, StringComparison.OrdinalIgnoreCase) && usuario.Length > 0)
            return new Error("La contraseña no puede contener el nombre de usuario.", "Nueva");
        if (contrasena.Distinct().Count() < 5)
            return new Error("La contraseña es demasiado sencilla: usa más caracteres distintos.", "Nueva");
        return null;
    }

    internal static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool TokenCoincide(string token, string hashGuardado) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(HashToken(token)), Encoding.ASCII.GetBytes(hashGuardado));

    private string MensajeBloqueo(Administrador administrador)
    {
        var minutos = Math.Max(1, (int)Math.Ceiling((administrador.BloqueadoHasta!.Value - Ahora).TotalMinutes));
        return $"Demasiados intentos fallidos. Por seguridad, la cuenta está bloqueada {minutos} min.";
    }

    private async Task AnotarAsync(string usuario, Origen origen, bool exito, string motivo, CancellationToken ct)
    {
        intentos.Agregar(IntentoAcceso.Registrar(usuario, origen.Ip, exito, motivo, Ahora));
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
    }
}
