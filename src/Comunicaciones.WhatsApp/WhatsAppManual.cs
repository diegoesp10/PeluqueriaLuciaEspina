using System.Text.RegularExpressions;

namespace Comunicaciones.WhatsApp;

public interface IWhatsAppManual { Uri Preparar(string telefonoInternacional, string mensaje); }

/// <summary>Prepara un mensaje para que una persona lo envíe en WhatsApp. Abrir el enlace no lo envía.</summary>
public sealed class WhatsAppManual : IWhatsAppManual
{
    public Uri Preparar(string telefonoInternacional, string mensaje)
    {
        if (!Regex.IsMatch(telefonoInternacional, @"^\+[1-9][0-9]{6,14}$"))
            throw new ArgumentException("Es necesario un teléfono internacional válido.", nameof(telefonoInternacional));
        if (string.IsNullOrWhiteSpace(mensaje) || mensaje.Length > 4000)
            throw new ArgumentException("El mensaje debe tener entre 1 y 4000 caracteres.", nameof(mensaje));
        return new Uri($"https://wa.me/{telefonoInternacional[1..]}?text={Uri.EscapeDataString(mensaje)}");
    }
}
