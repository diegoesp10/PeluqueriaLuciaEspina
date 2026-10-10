using System.Text;
using PeluqueriaLucia.Application.Seguridad;

namespace PeluqueriaLucia.Web.Configuracion;

/// <summary>
/// Comando de consola para crear el administrador del panel o ponerle una contraseña nueva:
/// <code>dotnet PeluqueriaLucia.Web.dll administrador</code>
/// Solo funciona en el propio servidor: la web no tiene ninguna página para crear usuarios.
/// </summary>
public static class HerramientaAdministrador
{
    public const string Comando = "administrador";

    public static bool EsComando(string[] args) => args.Length > 0 && (args[0] == Comando || args[0] == "recuperar-mfa");

    public static async Task<int> EjecutarAsync(IServiceProvider servicios, bool recuperarMfa = false)
    {
        Console.WriteLine("Administrador del panel de Peluquería Lucía");
        Console.WriteLine("Si el usuario ya existe, se le pondrá la contraseña nueva y se cerrarán todas sus sesiones.");
        Console.WriteLine();

        Console.Write("Usuario: ");
        var usuario = Console.ReadLine() ?? string.Empty;
        var contrasena = LeerOculto($"Contraseña (mínimo {AccesoService.LongitudMinimaContrasena} caracteres): ");
        if (recuperarMfa)
        {
            await using var scopeMfa = servicios.CreateAsyncScope();
            var recuperacion = await scopeMfa.ServiceProvider.GetRequiredService<MfaService>().RecuperarDesdeConsolaAsync(usuario, contrasena);
            if (!recuperacion.Exito) { Console.Error.WriteLine(recuperacion.Error!.Mensaje); return 1; }
            Console.WriteLine("Sesiones y códigos anteriores anulados. El próximo acceso exigirá vincular y confirmar otro Authenticator.");
            return 0;
        }
        if (LeerOculto("Repite la contraseña: ") != contrasena)
        {
            Console.Error.WriteLine("Las contraseñas no coinciden. No se ha cambiado nada.");
            return 1;
        }

        await using var scope = servicios.CreateAsyncScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<AccesoService>().EstablecerAdministradorAsync(usuario, contrasena);
        if (!resultado.Exito)
        {
            Console.Error.WriteLine(resultado.Error!.Mensaje);
            return 1;
        }

        Console.WriteLine(resultado.Valor
            ? "Administrador creado. Ya puede entrar en /admin."
            : "Contraseña cambiada. Las sesiones abiertas se han cerrado.");
        return 0;
    }

    /// <summary>Lee sin mostrar lo que se escribe. Si la entrada viene redirigida (scripts), lee la línea tal cual.</summary>
    public static string LeerOculto(string pregunta)
    {
        Console.Write(pregunta);
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? string.Empty;

        var texto = new StringBuilder();
        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter)
                break;
            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (texto.Length > 0)
                    texto.Length--;
            }
            else if (!char.IsControl(tecla.KeyChar))
            {
                texto.Append(tecla.KeyChar);
            }
        }

        Console.WriteLine();
        return texto.ToString();
    }
}
