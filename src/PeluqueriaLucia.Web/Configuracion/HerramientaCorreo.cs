using System.Text.Json;
using Comunicaciones.Correo;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Web.Configuracion;

internal static class HerramientaCorreo
{
    public static int Configurar()
    {
        try
        {
            Console.WriteLine("Configuración SMTP. No se conectará ni se enviará ningún correo.");
            Console.Write("Servidor SMTP: "); var servidor = Console.ReadLine() ?? "";
            Console.Write("Puerto (587 STARTTLS / 465 TLS): "); var puerto = int.TryParse(Console.ReadLine(), out var p) ? p : 0;
            Console.Write("Seguridad (StartTls o Tls): "); var seguridad = Enum.TryParse<SeguridadSmtp>(Console.ReadLine(), true, out var s) ? s : (SeguridadSmtp)(-1);
            Console.Write("Usuario SMTP: "); var usuario = Console.ReadLine() ?? "";
            // Google enseña la contraseña de aplicación en bloques de cuatro letras: los espacios no forman parte de ella
            var clave = string.Concat(HerramientaAdministrador.LeerOculto("Contraseña SMTP (oculta; con o sin espacios): ").Where(c => !char.IsWhiteSpace(c)));
            Console.Write("Correo remitente: "); var remitente = Console.ReadLine() ?? "";
            var c = new ConfiguracionSmtp(servidor, puerto, seguridad, usuario, clave, remitente, "Lucía Peluquería Ecológica");
            if (!EnviadorCorreoSmtp.EsValida(c)) { Console.Error.WriteLine("Revisa servidor, puerto, seguridad, cuenta y remitente."); return 1; }
            ServicioConexionSql.GuardarCredencial(ConfiguracionCorreo.Destino, JsonSerializer.Serialize(c));
            Console.WriteLine("Credencial guardada en Windows. Para enviar hacen falta también PELUQUERIA_CORREO_ACTIVO=true y PELUQUERIA_URL_PUBLICA (en desarrollo ya vienen en los perfiles de launchSettings.json). Ver docs/AGENDA.md.");
            return 0;
        }
        catch (Exception) { Console.Error.WriteLine("No se ha podido guardar la configuración SMTP. Revisa los permisos del almacén de Windows."); return 1; }
    }
}
