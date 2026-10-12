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
            var clave = HerramientaAdministrador.LeerOculto("Contraseña SMTP (oculta): ");
            Console.Write("Correo remitente: "); var remitente = Console.ReadLine() ?? "";
            var c = new ConfiguracionSmtp(servidor, puerto, seguridad, usuario, clave, remitente, "El equipo del salón");
            if (!EnviadorCorreoSmtp.EsValida(c)) { Console.Error.WriteLine("Revisa servidor, puerto, seguridad, cuenta y remitente."); return 1; }
            ServicioConexionSql.GuardarCredencial(ConfiguracionCorreo.Destino, JsonSerializer.Serialize(c));
            Console.WriteLine("Credencial guardada en Windows. Configura PELUQUERIA_URL_PUBLICA=https://tu-dominio/ y activa PELUQUERIA_CORREO_ACTIVO=true cuando quieras habilitar los envíos. Ver docs/AGENDA.md.");
            return 0;
        }
        catch (Exception) { Console.Error.WriteLine("No se ha podido guardar la configuración SMTP. Revisa los permisos del almacén de Windows."); return 1; }
    }
}
