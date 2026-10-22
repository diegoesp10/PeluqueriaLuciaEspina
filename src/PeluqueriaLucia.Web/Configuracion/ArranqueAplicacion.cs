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

        // Fuera de desarrollo, las fotos de productos tienen que ir a una carpeta que sobreviva a las publicaciones
        if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(app.Configuration["FotosProductos:Carpeta"]))
            app.Logger.LogWarning("FotosProductos:Carpeta no está configurada en {Entorno}: las fotos de productos se guardan en {Carpeta}, que una publicación puede borrar. En Azure, añade el ajuste FotosProductos__Carpeta=/home/data/productos.",
                app.Environment.EnvironmentName, Path.Combine(app.Environment.ContentRootPath, "AlmacenProductos"));

        startup.Configure(app);
        await app.RunAsync();
        return 0;
    }
}
