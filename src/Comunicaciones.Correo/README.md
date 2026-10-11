# Comunicaciones.Correo

Biblioteca de clases .NET 10 independiente de las demás capas y proyectos de la solución.

Implementa `IConfiguracionSmtp` en la aplicación anfitriona para obtener la cuenta desde su almacén de secretos. Instancia `EnviadorCorreoSmtp` con esa configuración y llama a `EnviarAsync(new CorreoSaliente(destinatario, asunto, texto, html), cancellationToken)`.

`ConfiguracionSmtp` permite servidor, puerto, `StartTls` obligatorio o `Tls` directo, usuario, contraseña y remitente. La biblioteca valida certificados; no acepta conexiones sin cifrado. Cada envío usa su propio cliente SMTP.

`ResultadoEnvio.Aceptado` indica aceptación del servidor SMTP, no entrega ni lectura. `Rechazado` indica fallo conocido; `Incierto` requiere revisión antes de repetir. La cola, los reintentos y las reglas de cada negocio corresponden al anfitrión.

No registra credenciales ni mensajes. No conecta si no hay configuración válida. La cuenta y el proveedor deben probarse antes de habilitar envíos reales; si requieren OAuth se puede aportar otra implementación de `IEnviadorCorreo`.
