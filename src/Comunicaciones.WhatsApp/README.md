# Comunicaciones.WhatsApp

Biblioteca de clases .NET 10 sin referencias a otros proyectos ni paquetes.

`IWhatsAppManual` / `WhatsAppManual` prepara un `Uri` de WhatsApp con teléfono internacional y texto codificado:

```csharp
IWhatsAppManual whatsapp = new WhatsAppManual();
Uri enlace = whatsapp.Preparar(telefonoInternacional, mensaje);
```

La aplicación anfitriona presenta el enlace. Una persona abre WhatsApp y pulsa enviar. La biblioteca no envía mensajes, no comprueba cuentas ni entrega y no usa una API comercial. El registro del envío manual y las confirmaciones pertenecen a la aplicación anfitriona.
