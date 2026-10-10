# Agenda, rama agenda

La rama `agenda` nace de `develop`, después del commit de conexión segura, error genérico y catálogo reconstruible. No se ha fusionado ni publicado en un servidor externo.

## Flujo

La página `/citas` presenta calendario, servicios del catálogo publicado y horas libres. Se exige un móvil español (6/7, nueve cifras, normalizado a +34), día, hora y consentimiento para contactar. Esto comprueba el formato; la titularidad y legitimidad se comprueban contactando con ese teléfono desde el salón. No se envía un OTP automático.

La solicitud bloquea provisionalmente la duración estimada del servicio (30 minutos si se consulta con Lucía) durante 24 horas. Una profesional atiende una cita a la vez. Los huecos se ofrecen cada 15 minutos, desde 30 minutos de antelación y hasta 90 días. Las solicitudes caducadas dejan de bloquear sin necesidad de un proceso en segundo plano.

En `/admin/agenda` se ven la semana seleccionada y todas las solicitudes pendientes. El administrador valida el teléfono, fija la duración definitiva y confirma, rechaza, cancela o completa. Confirmar exige que el intervalo siga dentro del horario y que no se solape. No se modifica el historial de visitas ni se registra automáticamente un cobro.

El contador y los avisos se actualizan cada cinco segundos mientras el panel está visible. Cada consulta exige la sesión vigente del panel. Un aviso enlaza a la agenda, sin incluir teléfonos en la notificación.

## Horarios

La migración crea un horario inicial **propuesto**, que debe revisarse con Lucía antes de abrir reservas reales: lunes a viernes 09:45–13:00 y 15:00–19:00; sábado 09:45–13:00; domingo cerrado. Se pueden añadir/quitar tramos y cerrar/reabrir días en `/admin/agenda/horarios`. No se permite cerrar un día con solicitudes activas o citas confirmadas sin gestionarlas primero. Cambiar un tramo no cancela las citas confirmadas existentes.

Las horas de cita son locales de Oviedo (`Europe/Madrid`); la auditoría es UTC. No se permiten citas que atraviesen medianoche. El bloqueo `sp_getapplock` es transaccional en SQL, compartido por todas las instancias: leer disponibilidad y guardar se realiza bajo el mismo bloqueo. Un identificador único del formulario evita duplicados al reenviar y `rowversion` evita que dos administradores pisen cambios.

## SMS preparado, desactivado

Se ha elegido Twilio SMS para permitir avisos y respuestas sin depender de que el cliente tenga WhatsApp. [Twilio documenta SMS bidireccional para España](https://www.twilio.com/en-us/guidelines/es/sms). Antes de activar, confirmar con el proveedor la capacidad de envío y recepción del número contratado y los permisos de destino. No basta con introducir el número móvil habitual del salón como remitente. No se ha contratado un número ni realizado ningún envío.

El aviso se guarda en `MensajesCitas` en la misma transacción que la confirmación/rechazo/cancelación. Desde Comunicaciones se puede llamar, abrir un SMS en el móvil o marcar el aviso como comunicado personalmente. La web nunca lanza ese SMS manual por sí sola.

Configurar bajo la identidad de ejecución de la web:

```powershell
dotnet run --project src/PeluqueriaLucia.Web -- configurar-sms
```

Se solicitan Account SID, Auth Token oculto y número remitente. Se guardan en el almacén de credenciales Windows (`PeluqueriaLucia/TwilioSms`), fuera del repositorio, appsettings y User Secrets. Configurar no activa envíos.

Para activarlos, establecer en el entorno del servidor y reiniciar:

```powershell
$env:PELUQUERIA_SMS_ACTIVO='true'
$env:PELUQUERIA_SMS_URL_PUBLICA='https://tu-dominio.es'
```

La URL debe ser HTTPS, sin subruta ni parámetros. Configurar en Twilio el webhook entrante POST `https://tu-dominio.es/sms/entrada`; el estado de entrega POST `https://tu-dominio.es/sms/estado` se incluye en cada envío. La [firma del proveedor](https://www.twilio.com/docs/usage/webhooks/webhooks-security) se valida con el token, la URL pública exacta y todos los campos recibidos; no se confía en el Host enviado por el cliente. Peticiones no firmadas reciben 401.

El trabajador procesa la cola cada 15 segundos. Solo envía avisos creados después de configurar la credencial, para evitar enviar un historial antiguo al activarlo. `AceptadoProveedor` no significa entregado: el webhook cambia a `Entregado` o `Error`. Un timeout/caída deja `Incierto`, sin repetir automáticamente un mensaje que podría haberse enviado. Resolver estos casos revisando el proveedor o contactando personalmente. Los avisos pendientes anteriores a un cambio de cita pasan a `Obsoleto`.

Las respuestas se ven solo dentro del panel. STOP/BAJA bloquea nuevos SMS; START/ALTA permite volver a recibirlos. No hay interfaz para enviar mensajes libres ni campañas. La gestión de proveedores se prueba con datos locales; la entrega real y recepción requieren una comprobación final con la cuenta y el número configurados.

## Migraciones y endpoints

```powershell
dotnet ef database update --project src/PeluqueriaLucia.Infrastructure --startup-project src/PeluqueriaLucia.Web
```

`AgendaCitasYComunicaciones` añade `ReservasCitas`, `TramosAgenda`, `CierresAgenda`, `MensajesCitas`, `SmsEntrantes` y el horario inicial. No modifica las visitas existentes. La migración del catálogo conserva los 42 servicios actuales y las seis categorías al reconstruir la base.

| Ruta | Acceso / función |
|---|---|
| GET/POST `/citas` | Formulario público; POST con antifalsificación, consentimiento y límite por IP |
| GET `/citas/disponibilidad` | Solo horas y duración; nunca datos de reservas o teléfonos |
| GET `/admin/agenda`, `/admin/agenda/avisos` | Sesión de administrador con MFA |
| POST `/admin/agenda/gestionar` | Validación del teléfono, duración y estado; antifalsificación y versión |
| `/admin/agenda/horarios` | Configuración del calendario dentro del panel |
| POST `/sms/entrada`, `/sms/estado` | Webhooks firmados del proveedor |

Todos los botones de reserva dirigen a `/citas`. Se mantiene un único enlace a Booksy en el pie público. Las limitaciones por IP y por teléfono reducen solicitudes abusivas, sin sustituir la comprobación telefónica. Los teléfonos, notas y respuestas solo se exponen en el panel autenticado.
