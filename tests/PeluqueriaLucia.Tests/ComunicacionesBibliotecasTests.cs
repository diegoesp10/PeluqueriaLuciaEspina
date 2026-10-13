using Comunicaciones.Correo;
using Comunicaciones.WhatsApp;

namespace PeluqueriaLucia.Tests;

public sealed class ComunicacionesBibliotecasTests
{
    private sealed class SinConfiguracion : IConfiguracionSmtp { public ConfiguracionSmtp? Obtener() => null; }
    [Fact]
    public async Task Smtp_sin_configuracion_no_inicia_un_envio()
    {
        var correo = new EnviadorCorreoSmtp(new SinConfiguracion());
        Assert.False(correo.Configurado);
        Assert.Equal(ResultadoEnvio.Rechazado, await correo.EnviarAsync(new("prueba@example.test", "Prueba", "No enviar")));
    }
    [Fact]
    public void Configuracion_exige_puerto_tls_y_remitente_validos()
    {
        var c = new ConfiguracionSmtp("smtp.example.test", 587, SeguridadSmtp.StartTls, "cuenta", "clave-ficticia", "prueba@example.test", "Equipo");
        Assert.True(EnviadorCorreoSmtp.EsValida(c));
        Assert.False(EnviadorCorreoSmtp.EsValida(c with { Puerto = 0 }));
        Assert.False(EnviadorCorreoSmtp.EsValida(c with { Seguridad = (SeguridadSmtp)99 }));
        Assert.False(EnviadorCorreoSmtp.EsValida(c with { Remitente = "Nombre <prueba@example.test>" }));
    }
    [Fact]
    public void WhatsApp_codifica_texto_y_rechaza_numeros_inyectados()
    {
        var wa = new WhatsAppManual(); var mensaje = "Hola & confirma aquí: https://example.test/?a=1&b=2";
        var enlace = wa.Preparar("+34612345678", mensaje);
        Assert.Equal("https", enlace.Scheme); Assert.Equal("wa.me", enlace.Host);
        Assert.Equal("/34612345678", enlace.AbsolutePath);
        Assert.Equal("?text=" + mensaje, Uri.UnescapeDataString(enlace.Query));
        Assert.Throws<ArgumentException>(() => wa.Preparar("+34612345678/otra?x=1", mensaje));
    }
}
