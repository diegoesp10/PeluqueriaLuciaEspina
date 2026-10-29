using System.Globalization;
using System.Net;
using System.Text;
using Comunicaciones.Correo;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Publico;

namespace PeluqueriaLucia.Application.Agenda;

/// <summary>
/// Correo de los avisos de cita con la imagen de la peluquería: cabecera morada con el logo, el texto del aviso (el que
/// el salón ha revisado en Comunicaciones), una ficha con día, hora, servicio y dirección, el botón que corresponda y el
/// teléfono para cambios. HTML de correo: tablas y estilos en línea, que es lo que respetan Gmail, Outlook y Apple Mail;
/// el logo va incrustado (cid:) para que se vea aunque el lector bloquee las imágenes externas.
/// </summary>
public static class PlantillaCorreoCita
{
    public const string IdLogo = "logo-salon";
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    private const string Fondo = "#f4f1fa", Tarjeta = "#ffffff", Profundo = "#3b1f63", Acento = "#5c2d91", Lavanda = "#c9b6ef",
        Tinta = "#27193f", Texto = "#3d3352", Suave = "#786d8e", Ficha = "#f6f2fd", Linea = "#e0dbeb";
    private const string Letra = "Georgia,'Times New Roman',serif";

    private enum Tipo { Propuesta, Confirmada, Cancelada }

    public static CorreoSaliente Componer(string destinatario, string texto, string? enlace, ReservaCita? reserva, DatosSalon? salon)
    {
        var tipo = enlace is not null ? Tipo.Propuesta
            : reserva?.Estado is EstadoCita.Cancelada or EstadoCita.Rechazada or EstadoCita.NoPresentada ? Tipo.Cancelada : Tipo.Confirmada;
        var nombre = salon?.Nombre is { Length: > 0 } n ? n : "la peluquería";
        var asunto = tipo switch
        {
            Tipo.Propuesta => $"Tu propuesta de cita en {nombre}",
            Tipo.Cancelada => $"Sobre tu solicitud de cita en {nombre}",
            _ => $"Tu cita en {nombre} está confirmada",
        };
        var (etiqueta, titulo) = tipo switch
        {
            Tipo.Propuesta => ("PROPUESTA DE CITA", "Tu cita, a un paso"),
            Tipo.Cancelada => ("TU SOLICITUD DE CITA", "Busquemos otro momento"),
            _ => ("CITA CONFIRMADA", "Nos vemos pronto"),
        };
        var telefono = salon is { Telefonos.Count: > 0 } ? salon : null;

        return new CorreoSaliente(destinatario, asunto, TextoPlano(texto, enlace, salon), Html(asunto, etiqueta, titulo, texto, enlace, reserva, tipo, salon, telefono),
            salon?.Logo is { Length: > 0 } logo ? [new ImagenIncrustada(IdLogo, "logo.jpg", logo, salon.TipoLogo)] : null);
    }

    private static string TextoPlano(string texto, string? enlace, DatosSalon? salon)
    {
        var t = new StringBuilder(texto.Trim());
        if (enlace is not null) t.Append("\n\nRevisa y acepta tu cita: ").Append(enlace);
        if (salon is not null)
        {
            t.Append("\n\n--\n").Append(salon.Nombre);
            if (salon.DireccionCompleta.Length > 0) t.Append('\n').Append(salon.DireccionCompleta);
            if (salon.Telefonos.Count > 0) t.Append("\nTel. ").Append(string.Join(" · ", salon.Telefonos));
        }
        return t.ToString();
    }

    private static string H(string? valor) => WebUtility.HtmlEncode(valor ?? "");
    private static string Mayuscula(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Es) + s[1..];

    /// <summary>Los teléfonos del salón que aparecen en el texto no se parten entre dos líneas («985» arriba, «757 692» abajo).</summary>
    private static string TelefonosEnteros(string html, DatosSalon? salon) =>
        (salon?.Telefonos ?? []).Select(H).Where(t => t.Length > 0).Aggregate(html, (actual, t) => actual.Replace(t, $"<span style=\"white-space:nowrap;\">{t}</span>"));

    /// <summary>Cada teléfono, un enlace para llamar: «<a>659 306 659</a> o al <a>985 757 692</a>».</summary>
    private static string EnlacesTelefono(IReadOnlyList<string> telefonos)
    {
        var enlaces = telefonos.Select(t => $"""<a href="{H(TelefonoContacto.Enlace(t))}" style="color:{Acento};font-weight:bold;text-decoration:none;white-space:nowrap;">{H(t)}</a>""").ToList();
        return enlaces.Count == 1 ? enlaces[0] : string.Join(", ", enlaces.Take(enlaces.Count - 1)) + " o al " + enlaces[^1];
    }

    /// <summary>
    /// Código de seis cifras para completar los datos de una cita en la web, con la misma imagen que los avisos. En el
    /// texto plano va «Tu código es 123456»; en el HTML, destacado en grande («123 456»).
    /// </summary>
    public static CorreoSaliente ComponerCodigo(string destinatario, string codigo, DatosSalon? salon)
    {
        var nombre = salon?.Nombre is { Length: > 0 } n ? n : "la peluquería";
        var asunto = $"Tu código para pedir cita en {nombre}";
        const string Instrucciones = "Escríbelo en el formulario de citas para completar tus datos. Caduca en 10 minutos y solo sirve una vez.";
        var plano = new StringBuilder($"Tu código es {codigo}. {Instrucciones}\n\nSi no lo has pedido tú, ignora este correo: sin el código nadie puede ver tus datos.");
        if (salon is not null) plano.Append("\n\n--\n").Append(salon.Nombre);
        var html = Html(asunto, "TU CÓDIGO", "Completa tus datos", Instrucciones, null, null, Tipo.Confirmada, salon, null,
            destacado: codigo.Length == 6 ? codigo[..3] + " " + codigo[3..] : codigo,
            contacto: "¿No lo has pedido tú? Ignora este correo: sin el código nadie puede ver tus datos.",
            notaPie: "Te escribimos porque se ha pedido un código para completar una cita con este correo.");
        return new CorreoSaliente(destinatario, asunto, plano.ToString(), html,
            salon?.Logo is { Length: > 0 } logo ? [new ImagenIncrustada(IdLogo, "logo.jpg", logo, salon.TipoLogo)] : null);
    }

    /// <summary>
    /// Aviso al ganador de un sorteo, con la misma imagen que los avisos de cita: el texto revisado por el salón y el premio
    /// destacado. Para recogerlo, el teléfono del salón.
    /// </summary>
    public static CorreoSaliente ComponerPremio(string destinatario, string texto, string premio, DatosSalon? salon)
    {
        var nombre = salon?.Nombre is { Length: > 0 } n ? n : "la peluquería";
        var asunto = $"¡Has ganado el sorteo de {nombre}!";
        var plano = new StringBuilder(texto.Trim()).Append("\n\nTu premio: ").Append(premio);
        if (salon is not null)
        {
            plano.Append("\n\n--\n").Append(salon.Nombre);
            if (salon.Telefonos.Count > 0) plano.Append("\nTel. ").Append(string.Join(" · ", salon.Telefonos));
        }
        var telefono = salon is { Telefonos.Count: > 0 } ? salon : null;
        var html = Html(asunto, "SORTEO", "¡Enhorabuena!", texto, null, null, Tipo.Confirmada, salon, telefono, destacado: premio,
            contacto: telefono is null ? "Responde a este correo para acordar cuándo recogerlo." : null,
            notaPie: "Te escribimos porque has participado en un sorteo de la peluquería.");
        return new CorreoSaliente(destinatario, asunto, plano.ToString(), html,
            salon?.Logo is { Length: > 0 } logo ? [new ImagenIncrustada(IdLogo, "logo.jpg", logo, salon.TipoLogo)] : null);
    }

    private static string Html(string asunto, string etiqueta, string titulo, string texto, string? enlace, ReservaCita? reserva, Tipo tipo, DatosSalon? salon, DatosSalon? telefono,
        string? destacado = null, string? contacto = null, string? notaPie = null)
    {
        var nombre = salon?.Nombre is { Length: > 0 } n ? n : "Tu peluquería";
        var parrafos = string.Concat(texto.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => $"<p style=\"margin:0 0 14px;font-family:{Letra};font-size:16px;line-height:1.6;color:{Texto};\">{TelefonosEnteros(H(p), salon)}</p>"));
        var resumen = texto.Replace("\r", " ").Replace("\n", " ").Trim();
        if (resumen.Length > 120) resumen = resumen[..117] + "…";

        var html = new StringBuilder();
        html.Append($"""
            <!DOCTYPE html>
            <html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="color-scheme" content="light"><meta name="supported-color-schemes" content="light"><title>{H(asunto)}</title></head>
            <body style="margin:0;padding:0;background:{Fondo};">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">{H(resumen)}</div>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{Fondo};"><tr><td align="center" style="padding:32px 12px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:600px;background:{Tarjeta};border:1px solid {Linea};border-radius:20px;overflow:hidden;">
            <tr><td align="center" style="background:{Profundo};padding:32px 24px 28px;">
            """);
        if (salon?.Logo is { Length: > 0 })
            html.Append($"""<img src="cid:{IdLogo}" width="88" height="88" alt="{H(nombre)}" style="display:block;width:88px;height:88px;border:0;border-radius:18px;">""");
        html.Append($"""<p style="margin:14px 0 0;font-family:{Letra};font-size:21px;color:#f7f5fb;">{H(nombre)}</p>""");
        if (salon?.Ciudad is { Length: > 0 } ciudad)
            html.Append($"""<p style="margin:6px 0 0;font-family:{Letra};font-size:11px;letter-spacing:2px;text-transform:uppercase;color:{Lavanda};">Peluquería · {H(ciudad)}</p>""");
        html.Append($"""
            </td></tr>
            <tr><td style="padding:36px 40px 4px;">
            <p style="margin:0 0 8px;font-family:{Letra};font-size:12px;font-weight:bold;letter-spacing:2px;color:{Acento};">{H(etiqueta)}</p>
            <h1 style="margin:0 0 20px;font-family:{Letra};font-size:30px;line-height:1.2;font-weight:normal;color:{Tinta};">{H(titulo)}</h1>
            {parrafos}
            </td></tr>
            """);
        if (destacado is not null)
            html.Append($"""<tr><td align="center" style="padding:10px 40px 6px;"><div style="display:inline-block;padding:16px 30px;border:1px solid {Linea};border-radius:14px;background:{Ficha};font-family:{Letra};font-size:34px;font-weight:bold;letter-spacing:6px;color:{Tinta};">{H(destacado)}</div></td></tr>""");

        // Ficha de la cita: lo esencial de un vistazo (no en una cancelación, que ya no tiene cita)
        if (reserva is not null && tipo != Tipo.Cancelada)
        {
            html.Append($"""<tr><td style="padding:8px 40px 4px;"><table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{Ficha};border:1px solid {Linea};border-radius:14px;">""");
            void Fila(string clave, string valor, bool ultima = false) => html.Append($"""
                <tr><td width="34%" valign="top" style="padding:14px 0 {(ultima ? 14 : 0)}px 20px;font-family:{Letra};font-size:12px;letter-spacing:1px;text-transform:uppercase;color:{Suave};">{H(clave)}</td>
                <td valign="top" style="padding:12px 20px {(ultima ? 14 : 0)}px 8px;font-family:{Letra};font-size:16px;line-height:1.4;color:{Tinta};">{valor}</td></tr>
                """);
            var direccion = salon?.DireccionCompleta is { Length: > 0 } d ? d : null;
            Fila("Día", H(Mayuscula(reserva.Inicio.ToString("dddd d 'de' MMMM 'de' yyyy", Es))));
            Fila("Hora", H($"{reserva.Inicio:HH:mm} – {reserva.Fin:HH:mm}"));
            Fila("Servicio", H(reserva.ServicioNombre), ultima: direccion is null);
            if (direccion is not null)
                Fila("Dónde", salon!.Mapa is { Length: > 0 } mapa ? $"""<a href="{H(mapa)}" style="color:{Acento};text-decoration:none;">{H(direccion)}</a>""" : H(direccion), ultima: true);
            html.Append("</table></td></tr>");
        }

        // Acción principal: aceptar la propuesta o, si se ha cancelado, llamar para buscar otro momento
        var (textoBoton, destino) = tipo switch
        {
            Tipo.Propuesta => ("Revisar y aceptar mi cita", enlace),
            Tipo.Cancelada when telefono is not null => ($"Llamar al {telefono.Telefonos[0]}", TelefonoContacto.Enlace(telefono.Telefonos[0])),
            _ => ((string?)null, (string?)null),
        };
        if (textoBoton is not null && destino is not null)
            html.Append($"""<tr><td align="center" style="padding:26px 40px 6px;"><a href="{H(destino)}" style="display:inline-block;padding:15px 32px;border-radius:999px;background:{Acento};color:#ffffff;font-family:{Letra};font-size:16px;font-weight:bold;text-decoration:none;">{H(textoBoton)}</a></td></tr>""");

        // Contacto para cambios
        html.Append($"""<tr><td style="padding:26px 40px 34px;"><table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr><td style="border-top:1px solid {Linea};padding-top:20px;font-family:{Letra};font-size:15px;line-height:1.6;color:{Texto};">""");
        html.Append(contacto is not null ? H(contacto)
            : telefono is not null ? $"""¿Necesitas cambiar algo? Llámanos al {EnlacesTelefono(telefono.Telefonos)}, o responde a este correo."""
            : "¿Necesitas cambiar algo? Responde a este correo y te ayudamos.");
        html.Append("</td></tr></table></td></tr></table>");

        // Pie
        var pie = new[] { salon?.Nombre, salon?.DireccionCompleta, salon?.Telefonos is { Count: > 0 } ts ? "Tel. " + string.Join(" · ", ts) : null }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(H);
        html.Append($"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:600px;"><tr><td align="center" style="padding:20px 24px 8px;font-family:{Letra};font-size:12px;line-height:1.7;color:{Suave};">
            {string.Join(" · ", pie)}<br>{H(notaPie ?? "Te escribimos por tu cita en la peluquería. Tus datos solo se usan para gestionarla.")}
            </td></tr></table>
            </td></tr></table>
            </body></html>
            """);
        return html.ToString();
    }
}
