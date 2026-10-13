# Agenda y comunicaciones

## Flujo

En `/citas` son obligatorios el móvil español, el correo electrónico, el día, la hora, el consentimiento y la elección de correo o WhatsApp. Se valida el formato; el equipo del salón comprueba la legitimidad contactando con el teléfono. La disponibilidad se comprueba otra vez al guardar, bajo un bloqueo SQL compartido por todas las instancias.

La solicitud reserva provisionalmente la duración del servicio durante 24 horas. El administrador valida el teléfono y fija la duración en `/admin/agenda`. «Preparar propuesta» crea un enlace privado y deja la cita pendiente de aceptación; todavía no está confirmada. El enlace caduca a las 24 horas o al comenzar la cita, lo que suceda primero. Un cambio de duración genera una nueva propuesta e invalida el enlace anterior. Las solicitudes y propuestas caducadas dejan de bloquear el horario.

Abrir el enlace no acepta la cita: evita confirmaciones accidentales por lectores automáticos de correo. El cliente revisa servicio, fecha, hora y duración y pulsa «Aceptar cita». El POST exige antifalsificación y comprueba vigencia y disponibilidad en una transacción. La aceptación se registra con fecha UTC, consume el token y cambia a Confirmada. Ni enviar el mensaje ni marcar WhatsApp como enviado equivale a aceptar la cita.

Los enlaces contienen 256 bits aleatorios; la reserva guarda solo su hash. La cola guarda el token cifrado con Data Protection. La página no muestra nombres, correo, teléfono ni notas internas; no admite caché ni reenvía el enlace en Referer. El dominio del enlace procede de configuración del servidor, nunca de la cabecera Host.

## Bibliotecas reutilizables y capas

- `Comunicaciones.Correo`: biblioteca de clases independiente para SMTP; contratos `IEnviadorCorreo`, `IConfiguracionSmtp`, `CorreoSaliente` y `ConfiguracionSmtp`. Utiliza MailKit con STARTTLS obligatorio o TLS directo y validación normal de certificados. No referencia otros proyectos de la solución, MVC, Entity Framework ni clases de la peluquería.
- `Comunicaciones.WhatsApp`: biblioteca de clases independiente; `IWhatsAppManual` / `WhatsAppManual` preparan un enlace seguro `https://wa.me/` con texto codificado. No conoce reservas ni base de datos, no usa una API de pago ni envía automáticamente.
- `PeluqueriaLucia.Application`: gestiona horarios, reservas, propuestas, aceptación, composición de mensajes, cola y reintentos; llama a las bibliotecas mediante interfaces.
- `PeluqueriaLucia.Infrastructure`: implementa `IRepositorioAgenda`, las transacciones SQL y los adaptadores para obtener SMTP y la URL pública del anfitrión.
- `PeluqueriaLucia.Web`: formularios, controladores y el trabajador que solicita procesar la cola. `Program.cs` solo delega en `ArranqueAplicacion`; los comandos de configuración tienen sus propias clases.

Las bibliotecas tienen sus propios TargetFramework, Nullable e ImplicitUsings. Para reutilizarlas basta copiarlas o referenciar sus proyectos, aportar `IConfiguracionSmtp` y construir `EnviadorCorreoSmtp`; `WhatsAppManual` no requiere configuración. Su configuración puede proceder del almacén de secretos de cualquier otra aplicación.

## SMTP pendiente de configurar

No se ha configurado ninguna cuenta ni se han enviado correos reales. Para guardar SMTP bajo la misma identidad Windows que ejecutará la aplicación:

```powershell
dotnet run --project src/PeluqueriaLucia.Web -- configurar-correo
```

Solicita servidor, puerto, seguridad (`StartTls` o `Tls`), usuario, contraseña oculta y remitente. Se guarda en el Administrador de credenciales de Windows, destino `PeluqueriaLucia/CorreoSmtp`, fuera de appsettings, User Secrets y Git. Configurar no conecta al proveedor ni activa el envío. Algunos proveedores necesitan contraseña de aplicación u OAuth; cuando se elija la cuenta se comprobará su método de autenticación antes de activar.

En el entorno del proceso/IIS, configurar y reiniciar:

```powershell
$env:PELUQUERIA_URL_PUBLICA='https://tu-dominio.es/'
$env:PELUQUERIA_CORREO_ACTIVO='true'
```

La URL debe ser HTTPS, de raíz, sin usuario, parámetros ni fragmentos; es necesaria tanto para correo como para WhatsApp. El panel muestra lo que falta configurar. Sin SMTP activo los correos permanecen pendientes; las propuestas ya caducadas se descartan cuando se procesa la cola. Revisa las propuestas vigentes antes de activar, porque se enviarán los correos pendientes aún válidos.

El trabajador procesa la cola cada 15 segundos. Cada mensaje se reclama transaccionalmente antes de salir, evitando que varias instancias lo envíen a la vez. `AceptadoServidor` significa aceptación SMTP, no entrega, lectura ni aceptación de la cita. Un fallo durante el envío o una caída del proceso pasa a Incierto y no se reintenta automáticamente. Los correos rechazados pueden reintentarse explícitamente desde el panel mientras la propuesta siga vigente. Revisa los casos inciertos en el proveedor antes de contactar de nuevo.

## WhatsApp manual

En Comunicaciones, «Abrir mensaje en WhatsApp» abre el teléfono del cliente con el mensaje y el enlace preparados. El equipo pulsa enviar dentro de WhatsApp y después «Ya lo he enviado en WhatsApp» en el panel. La aplicación no determina si el número tiene WhatsApp ni comprueba la entrega. Si el cliente no puede usar ese canal, el equipo debe acordar cómo contactar con él; no hay cambio automático de canal ni coste de una API integrado.

## Migración y datos anteriores

Aplicar con la aplicación parada o antes de reiniciarla con la versión nueva:

```powershell
dotnet ef database update --project src/PeluqueriaLucia.Infrastructure --startup-project src/PeluqueriaLucia.Web
```

`CorreoWhatsAppYAceptacion` añade correo, canal, hash y aceptación a las reservas; convierte `MensajesCitas` en `NotificacionesCitas`, conserva destinatarios y texto del historial anterior y marca todos sus registros Obsoleto para impedir envíos. Archiva la tabla de respuestas anterior como `ComunicacionesHistoricas`, conservando los datos sin mantener código de SMS ni endpoints del proveedor. Las migraciones históricas permanecen intactas para reconstruir la base. Antes de actualizar una base con datos, guardar una copia SQL; para revertir con datos operativos, restaurar la copia en lugar de aplicar Down, que elimina datos nuevos.

Las reservas anteriores conservan su estado y no se inventa su correo ni su aceptación. Para proponer otra duración de una reserva antigua sin correo, contactar con el cliente y registrar una nueva solicitud con sus datos completos. Las visitas, administradores, MFA, sesiones y los 42 servicios del catálogo se conservan.

## Horarios y acceso

En `/admin/agenda/horarios` se gestionan tramos y cierres. Revisar el horario inicial con el equipo antes de abrir reservas reales. Los huecos se ofrecen cada 15 minutos, con 30 minutos de antelación y hasta 90 días. Las horas son locales `Europe/Madrid`; auditoría UTC. No se permiten intervalos ambiguos por cambio de hora ni citas que atraviesen medianoche. No se cierra un día con reservas activas sin gestionarlas antes.

| Ruta | Función |
|---|---|
| GET/POST `/citas` | Solicitud pública, consentimiento, antifalsificación y límites por IP/teléfono |
| GET `/citas/disponibilidad` | Solo horas y duración |
| GET/POST `/citas/confirmar/{token}` | Revisión y aceptación privada; GET no modifica |
| GET `/admin/agenda`, `/admin/agenda/avisos` | Panel protegido por sesión con MFA |
| POST `/admin/agenda/gestionar` | Validación y propuesta con control de versión |
| POST `/admin/agenda/whatsapp-enviado` | Registro manual del envío, sin aceptar la cita |
| POST `/admin/agenda/reintentar-correo` | Reintento explícito de un correo rechazado |

Todos los botones públicos de reserva llevan a la agenda propia; la web no enlaza a plataformas de reservas externas.


## Fichas automáticas e inasistencias

El formulario exige nombre o apodo, correo y móvil. La ficha no se crea al solicitar, preparar o enviar un aviso: únicamente al aceptar el enlace mediante POST. La aceptación y la vinculación se guardan juntas en la transacción exclusiva de agenda. La coincidencia exige ambos contactos; admite mayúsculas en correo y los formatos habituales del teléfono español. Una ficha existente conserva sus datos, aunque el visitante use otro apodo.

Sin coincidencias se crea una ficha con apodo, contactos y `PendienteCompletar=true`. Aparece al principio de Clientes y en los avisos de Agenda. Editar y guardar nombre y apellidos completa la ficha. Las coincidencias parciales o ambiguas permiten aceptar la cita, pero requieren que el administrador revise los contactos y seleccione la ficha en Agenda. No se fusionan ni sobrescriben clientes automáticamente. Las citas aceptadas antes de esta migración también aparecen para vinculación manual.

Solo administración puede registrar una inasistencia después de finalizar el horario de una cita confirmada y vinculada. Se conserva referencia a cliente, cita, fecha, administrador y nota. El contador deriva de los registros activos; la restricción única evita duplicar una falta por cita. Corregir exige motivo y conserva el registro con fecha y administrador de la rectificación. No hay penalizaciones automáticas ni exposición del contador, notas o historial en las respuestas públicas. Se impide eliminar clientes con citas o visitas vinculadas.

La migración `ClientesDeAgendaYFaltas` añade columnas y tabla sin borrar datos existentes ni modificar ServiciosGenericos. Su reversión elimina estos nuevos datos: para recuperar historial debe usarse una copia SQL verificada.


## Intervalos de inicio y reconocimiento en administración

En Agenda → Horarios y días de cierre, cada tramo semanal tiene un `IntervaloMinutos` editable de 1 a 600 minutos. Las horas se ofrecen desde su apertura a ese ritmo; los horarios existentes conservan 15 minutos hasta que se cambien. El intervalo no modifica la duración de las citas ya reservadas. Administración puede fijar duraciones de 5 a 600 minutos, sin obligarlas a ser múltiplos de 15 ni del intervalo.

El motor comprueba todo el intervalo `[Inicio, Fin)`: un trabajo de 10:00 a 11:00 bloquea cualquier propuesta que se solape y permite comenzar a las 11:00 si el servicio completo cabe en horario y no hay otra reserva. Las solicitudes vigentes y las propuestas pendientes de aceptación también retienen su duración. Las horas bloqueadas aparecen como «Ocupado», sin datos personales. Un cambio de duración que choque con otra reserva se rechaza; no cancela citas ajenas automáticamente.

Nuevo cliente comienza por teléfono y correo. Su coincidencia exacta reconoce una única ficha y autocompleta nombre, apellidos, sexo y apodo. Guardar verifica ambos contactos de nuevo en el servidor y actualiza esa ficha, evitando duplicados. La consulta es un POST protegido por sesión administrativa, MFA y antifalsificación; no se publica ningún endpoint anónimo de reconocimiento. En el formulario público también se pide primero teléfono y correo, y después nombre o apodo, sin exponer datos de fichas existentes.


## Datos personales en la reserva pública

El formulario exige teléfono, correo, nombre o apodo, apellidos y sexo (Mujer, Hombre u Otro). Apellidos y sexo se conservan en la solicitud y se copian a una nueva ficha pendiente de revisar únicamente después de aceptar la cita. No sobrescriben fichas existentes.

Conocer un teléfono no autoriza a consultar datos de clientes: al salir del campo se solicita un código enviado exclusivamente al correo ya guardado en una ficha inequívoca. La respuesta nunca confirma si ese teléfono existe ni indica el correo destinatario. Introducir el código correcto permite autocompletar nombre, apellidos, correo y sexo. Un teléfono ambiguo o sin correo requiere rellenar los datos manualmente.

El autocompletado se habilita cuando el transporte SMTP esté configurado y activado. Mientras tanto el formulario funciona con entrada manual. La biblioteca independiente Comunicaciones.Correo sigue siendo el transporte; VerificacionContactoService contiene las reglas y el proceso existente de correo atiende también esta cola.

Cada desafío caduca a los diez minutos, admite cinco intentos y es de un solo uso. Solicitar otro código invalida el anterior. Se limitan las solicitudes por IP y por teléfono; los códigos y la clave utilizada en su hash se cifran con Data Protection. Los endpoints son POST con antifalsificación y respuestas sin caché. Un cambio del contacto registrado invalida la verificación. Los resultados de envío inciertos no se reintentan automáticamente. No hay envíos de SMS ni consulta pública de fichas sin verificación.

La migración DatosClienteYVerificacionPublica amplía Nombre sin recortar datos y añade Apellido, Sexo y VerificacionesContacto. Las reservas históricas conservan su información anterior y las columnas nuevas vacías; se completan desde administración, sin inventar datos personales.


## Citas del salón, bloqueos y confirmación directa

**Nueva cita** (`/admin/agenda/nueva`) anota las citas dadas por teléfono o en persona. Se busca la ficha por nombre, teléfono o correo; si no tiene ficha basta con un nombre y, opcionalmente, un teléfono (se admiten fijos). Nace confirmada, no envía avisos y se marca «Dada en el salón». La duración por defecto es la del servicio (o 30 minutos). Puede quedar fuera del horario público (horas extra), pero no puede solaparse con otra cita ni caer en un día cerrado.

**Bloquear tiempo** reserva un rato sin cliente (descanso, médico, formación). La web deja de ofrecer esas horas; «Quitar bloqueo» las libera.

En una solicitud de la web, **Confirmar ya (acordada por teléfono)** confirma la cita sin esperar al enlace de aceptación, una vez validado el teléfono. Crea o vincula la ficha igual que la aceptación y deja en Comunicaciones un aviso informativo sin enlace. También sirve para reservas antiguas sin correo.

Las citas confirmadas o completadas con ficha muestran **Registrar visita**, que abre Nueva visita con el cliente elegido.

La migración `OrigenDeLasCitas` añade la columna `Origen` a `ReservasCitas` (0 web, 1 salón, 2 bloqueo). Las reservas existentes quedan como web. No borra ni modifica datos.
