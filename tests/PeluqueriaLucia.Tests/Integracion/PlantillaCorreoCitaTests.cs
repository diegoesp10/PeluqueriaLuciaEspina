using Comunicaciones.Correo;
using MimeKit;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Tests.Integracion;

public sealed class PlantillaCorreoCitaTests
{
    private static readonly byte[] Logo = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
    private static DatosSalon Salon(params string[] telefonos) =>
        new("Lucía Peluquería Ecológica", telefonos, "C. de Pío XII, 10", "Oviedo", "https://maps.example/lucia", Logo);
    private static ReservaCita Reserva(EstadoCita estado) => new()
    {
        Inicio = new DateTime(2026, 10, 16, 15, 0, 0), Fin = new DateTime(2026, 10, 16, 15, 45, 0), ServicioNombre = "Corte & peinado", Estado = estado,
    };

    [Fact]
    public void Cita_confirmada_lleva_logo_ficha_y_los_telefonos_para_llamar()
    {
        var correo = PlantillaCorreoCita.Componer("ana@example.test", "Tu cita queda confirmada.\nTe esperamos <b>pronto</b>.", null, Reserva(EstadoCita.Confirmada), Salon("659 306 659", "985 757 692"));
        Assert.Equal("Tu cita en Lucía Peluquería Ecológica está confirmada", correo.Asunto);
        var html = correo.Html!;
        Assert.Contains("cid:" + PlantillaCorreoCita.IdLogo, html);
        Assert.Equal(PlantillaCorreoCita.IdLogo, Assert.Single(correo.Imagenes!).Id);
        Assert.Contains("CITA CONFIRMADA", html);
        Assert.Contains("Viernes 16 de octubre de 2026", html);
        Assert.Contains("15:00 – 15:45", html);
        Assert.Contains("Corte &amp; peinado", html);
        Assert.Contains("href=\"tel:+34659306659\"", html); Assert.Contains("href=\"tel:+34985757692\"", html);
        Assert.Contains("https://maps.example/lucia", html);
        // El texto del aviso (editable en Comunicaciones) nunca se interpreta como HTML
        Assert.Contains("&lt;b&gt;pronto&lt;/b&gt;", html); Assert.DoesNotContain("<b>pronto</b>", html);
        Assert.DoesNotContain("Revisar y aceptar", html);
        Assert.StartsWith("Tu cita queda confirmada.", correo.Texto);
        Assert.Contains("Tel. 659 306 659 · 985 757 692", correo.Texto);
    }

    [Fact]
    public void Propuesta_lleva_el_boton_de_aceptar_y_cancelacion_el_de_llamar()
    {
        var propuesta = PlantillaCorreoCita.Componer("ana@example.test", "Te proponemos una cita.", "https://lucia.example/citas/confirmar/ABC", Reserva(EstadoCita.PendienteAceptacion), Salon("659 306 659"));
        Assert.Equal("Tu propuesta de cita en Lucía Peluquería Ecológica", propuesta.Asunto);
        Assert.Contains("href=\"https://lucia.example/citas/confirmar/ABC\"", propuesta.Html!);
        Assert.Contains("Revisar y aceptar mi cita", propuesta.Html!);
        Assert.Contains("https://lucia.example/citas/confirmar/ABC", propuesta.Texto);

        var cancelada = PlantillaCorreoCita.Componer("ana@example.test", "No se ha podido confirmar.", null, Reserva(EstadoCita.Cancelada), Salon("659 306 659"));
        Assert.Equal("Sobre tu solicitud de cita en Lucía Peluquería Ecológica", cancelada.Asunto);
        Assert.Contains("Llamar al 659 306 659", cancelada.Html!);
        Assert.DoesNotContain("15:00 – 15:45", cancelada.Html!);
    }

    [Fact]
    public void Sin_datos_del_salon_el_correo_sale_igual_sin_logo()
    {
        var correo = PlantillaCorreoCita.Componer("ana@example.test", "Hola.", null, null, null);
        Assert.Null(correo.Imagenes);
        Assert.DoesNotContain("cid:", correo.Html!);
        Assert.Equal("Hola.", correo.Texto);
    }

    [Fact]
    public void El_logo_viaja_incrustado_con_su_content_id()
    {
        var correo = PlantillaCorreoCita.Componer("ana@example.test", "Hola.", null, Reserva(EstadoCita.Confirmada), Salon("659 306 659"));
        var mensaje = EnviadorCorreoSmtp.Construir(new ConfiguracionSmtp("smtp.example", 587, SeguridadSmtp.StartTls, "u", "c", "salon@example.test", "Lucía Peluquería Ecológica"),
            correo, MailboxAddress.Parse("ana@example.test"));
        var logo = Assert.Single(mensaje.BodyParts.OfType<MimePart>(), p => p.ContentType.MimeType == "image/jpeg");
        Assert.Equal(PlantillaCorreoCita.IdLogo, logo.ContentId);
        Assert.Equal("Lucía Peluquería Ecológica", ((MailboxAddress)mensaje.From.Single()).Name);
        Assert.NotNull(mensaje.HtmlBody); Assert.NotNull(mensaje.TextBody);
    }
}
