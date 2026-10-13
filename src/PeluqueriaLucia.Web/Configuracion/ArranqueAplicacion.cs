namespace PeluqueriaLucia.Web.Configuracion;

public static class ArranqueAplicacion
{
    public static async Task<int> EjecutarAsync(string[] args)
    {
        if (args.FirstOrDefault() == "configurar-conexion") return await HerramientaConexion.ConfigurarAsync();
        if (args.FirstOrDefault() == "configurar-correo") return HerramientaCorreo.Configurar();
        var builder = WebApplication.CreateBuilder(args);

        // Textos y datos de la web pública (horario, servicios, contacto): se editan sin recompilar
        builder.Configuration.AddJsonFile("contenido-publico.json", optional: false, reloadOnChange: true);

        var startup = new Startup(builder.Configuration, builder.Environment);
        startup.ConfigureServices(builder.Services);

        var app = builder.Build();

        // «dotnet PeluqueriaLucia.Web.dll administrador»: crea el usuario del panel desde la consola y termina
        if (HerramientaAdministrador.EsComando(args))
            return await HerramientaAdministrador.EjecutarAsync(app.Services, recuperarMfa: args[0] == "recuperar-mfa", soloCrear: args[0] == "administrador-nuevo");

        startup.Configure(app);
        await app.RunAsync();
        return 0;
    }
}
