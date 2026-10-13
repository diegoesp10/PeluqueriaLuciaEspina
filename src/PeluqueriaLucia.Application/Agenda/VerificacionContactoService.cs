using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Comunicaciones.Correo;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Application.Agenda;

public sealed record FichaContactoPublica(string Nombre, string Apellido, string Correo, Sexo Sexo);

public sealed class VerificacionContactoService(IRepositorioAgenda datos, IProtectorSecretos secretos, IEnviadorCorreo correo, TimeProvider reloj)
{
    private DateTime Utc => reloj.GetUtcNow().UtcDateTime;
    public bool Disponible => correo.Configurado;
    public Task<Guid> SolicitarAsync(string? telefono, string ip, CancellationToken ct) => datos.ExclusivoAsync(async () =>
    {
        var id = Guid.NewGuid(); var movil = AgendaService.NormalizarTelefono(telefono);
        if (movil is null) return id;
        var ipHash = ComunicacionesCitas.Hash(ip);
        if (await datos.ContarAsync<VerificacionContacto>(x => x.Telefono == movil && x.CreadaUtc > Utc.AddHours(-1), ct) >= 3 ||
            await datos.ContarAsync<VerificacionContacto>(x => x.IpHash == ipHash && x.CreadaUtc > Utc.AddHours(-1), ct) >= 5) return id;
        foreach(var anterior in await datos.ListarAsync<VerificacionContacto>(x=>x.Telefono==movil && x.CaducaUtc>Utc && x.ConsumidaUtc==null,ct))
        { anterior.CaducaUtc=Utc; anterior.CodigoProtegido=null; anterior.SecretoProtegido=null; anterior.EstadoEnvio=EstadoNotificacion.Obsoleto; }
        // La respuesta es idéntica para teléfonos nuevos, ambiguos, sin correo o limitados.
        var candidatos = (await datos.ClientesPorContactoAsync(movil, "", ct))
            .Where(x => AgendaService.NormalizarTelefono(x.Telefono) == movil).ToList();
        var cliente = candidatos.Count == 1 ? candidatos[0] : null;
        var email = AgendaService.NormalizarCorreo(cliente?.Email);
        var codigo = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
        var clave = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        datos.Agregar(new VerificacionContacto { Id = id, Telefono = movil, IpHash = ipHash, CreadaUtc = Utc, CaducaUtc = Utc.AddMinutes(10),
            ClienteId = email is null ? null : cliente!.Id, DestinoCorreo = email,
            CodigoHash = ComunicacionesCitas.Hash(clave + codigo),
            CodigoProtegido = email is null ? null : secretos.Proteger(codigo, "contacto-codigo"),
            SecretoProtegido = email is null ? null : secretos.Proteger(clave, "contacto-clave"),
            EstadoEnvio = email is null ? EstadoNotificacion.Obsoleto : EstadoNotificacion.Pendiente });
        return id;
    }, ct);

    public Task<Resultado<FichaContactoPublica>> VerificarAsync(Guid id, string? codigo, CancellationToken ct) => datos.ExclusivoAsync<Resultado<FichaContactoPublica>>(async () =>
    {
        var fallo = new Error("El código no es válido, ha caducado o ya se ha utilizado. Puedes rellenar los datos manualmente.");
        var v = await datos.BuscarAsync<VerificacionContacto>(x => x.Id == id, ct);
        if (v is null || v.ConsumidaUtc is not null || v.CaducaUtc <= Utc || v.Intentos >= 5) return fallo;
        v.Intentos++;
        var clave = v.SecretoProtegido is null ? null : secretos.Leer(v.SecretoProtegido, "contacto-clave");
        if (clave is null || codigo is null || !Regex.IsMatch(codigo, "^[0-9]{6}$") ||
            !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(v.CodigoHash),Convert.FromHexString(ComunicacionesCitas.Hash(clave + codigo)))) return fallo;
        var cliente = await datos.BuscarAsync<Cliente>(x => x.Id == v.ClienteId, ct);
        if (cliente is null || AgendaService.NormalizarTelefono(cliente.Telefono) != v.Telefono ||
            !string.Equals(cliente.Email?.Trim(), v.DestinoCorreo, StringComparison.OrdinalIgnoreCase)) return fallo;
        v.ConsumidaUtc = Utc; v.CodigoProtegido = null; v.SecretoProtegido = null; v.EstadoEnvio = EstadoNotificacion.Obsoleto;
        return Resultado.Ok(new FichaContactoPublica(cliente.Nombre,cliente.Apellido,cliente.Email!,cliente.Sexo));
    }, ct);

    public async Task ProcesarColaAsync(CancellationToken ct)
    {
        if (!correo.Configurado) return;
        await datos.ExclusivoAsync(async () =>
        {
            foreach(var v in await datos.ListarAsync<VerificacionContacto>(x => x.CaducaUtc <= Utc && (x.CodigoProtegido != null || x.SecretoProtegido != null),ct))
            { v.CodigoProtegido=null; v.SecretoProtegido=null; if(v.EstadoEnvio == EstadoNotificacion.Pendiente) v.EstadoEnvio=EstadoNotificacion.Obsoleto; }
            foreach(var v in await datos.ListarAsync<VerificacionContacto>(x => x.EstadoEnvio == EstadoNotificacion.Enviando && x.ProcesadoUtc < Utc.AddMinutes(-2),ct))
                v.EstadoEnvio=EstadoNotificacion.Incierto;
            return true;
        },ct);
        var pendientes=(await datos.ListarAsync<VerificacionContacto>(x=>x.EstadoEnvio==EstadoNotificacion.Pendiente && x.CaducaUtc>Utc,ct))
            .OrderBy(x=>x.CreadaUtc).Take(10).Select(x=>x.Id).ToArray();
        foreach(var id in pendientes)
        {
            var mensaje=await datos.ExclusivoAsync(async () =>
            {
                var v=await datos.BuscarAsync<VerificacionContacto>(x=>x.Id==id,ct);
                if(v is null || v.EstadoEnvio!=EstadoNotificacion.Pendiente || v.CaducaUtc<=Utc || v.CodigoProtegido is null) return null;
                if(v.ClienteId is null){v.EstadoEnvio=EstadoNotificacion.Obsoleto;return null;}
                var cliente=await datos.BuscarAsync<Cliente>(x=>x.Id==v.ClienteId,ct);
                if(cliente is null || AgendaService.NormalizarTelefono(cliente.Telefono)!=v.Telefono || !string.Equals(cliente.Email?.Trim(),v.DestinoCorreo,StringComparison.OrdinalIgnoreCase))
                {v.EstadoEnvio=EstadoNotificacion.Obsoleto;v.CodigoProtegido=null;v.SecretoProtegido=null;return null;}
                var codigo=secretos.Leer(v.CodigoProtegido,"contacto-codigo");
                if(codigo is null || v.DestinoCorreo is null) {v.EstadoEnvio=EstadoNotificacion.Obsoleto;return null;}
                v.EstadoEnvio=EstadoNotificacion.Enviando;v.ProcesadoUtc=Utc;
                return new CorreoSaliente(v.DestinoCorreo,"Código para completar tus datos de cita",
                    "Tu código es " + codigo + ". Caduca en 10 minutos y solo puede utilizarse una vez. Introdúcelo en el formulario de citas para completar tus datos. Si no lo has solicitado, ignora este correo. El equipo del salón.");
            },ct);
            if(mensaje is null)continue;
            ResultadoEnvio resultado;
            try{resultado=await correo.EnviarAsync(mensaje,ct);}catch(Exception){resultado=ResultadoEnvio.Incierto;}
            await datos.ExclusivoAsync(async () =>
            {
                var v=await datos.BuscarAsync<VerificacionContacto>(x=>x.Id==id,CancellationToken.None);
                if(v is {EstadoEnvio:EstadoNotificacion.Enviando})
                {v.EstadoEnvio=resultado switch{ResultadoEnvio.Aceptado=>EstadoNotificacion.AceptadoServidor,ResultadoEnvio.Rechazado=>EstadoNotificacion.Error,_=>EstadoNotificacion.Incierto};v.ProcesadoUtc=Utc;v.CodigoProtegido=null;}
                return true;
            },CancellationToken.None);
            ct.ThrowIfCancellationRequested();
        }
    }
}
