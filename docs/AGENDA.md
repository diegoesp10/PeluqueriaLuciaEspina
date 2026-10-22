# Agenda y comunicaciones

## Flujo

En `/citas` son obligatorios el móvil español, el correo electrónico, el día, la hora, el consentimiento y la elección de correo o WhatsApp. Se valida el formato; el equipo del salón comprueba la legitimidad contactando con el teléfono. La disponibilidad se comprueba otra vez al guardar, bajo un bloqueo SQL compartido por todas las instancias.

**Para quién y qué cuidado.** «¿Para quién es la cita?» (Hombre, Mujer, Otro) es el sexo de la reserva: el formulario de datos ya no lo pregunta y lo envía en un campo oculto (`Formulario.Sexo`). Es obligatorio; sin elegirlo, la solicitud no sale y se avisa junto a esa pregunta. También filtra la lista: con Hombre o Mujer se ven los servicios de ese público y siempre los unisex (público «Otro»: manicura, maquillaje…); con Otro, todos. Viaja como `?para=` y los enlaces del calendario lo conservan. Al verificar el teléfono con el código del correo, el autocompletado marca la opción de la ficha. La lista se busca escribiendo (`select-buscable.js`, sin mayúsculas ni acentos) y, si el cuidado no está, la última fila ofrece «Preguntar por «lo escrito»». Elegir un cuidado carga su disponibilidad sin pulsar «Ver disponibilidad» (`agenda-publica.js`). Lo escrito viaja como `?otro=` (hasta 80 caracteres, en una sola línea) y la reserva se guarda sin servicio del catálogo, con la duración de una consulta (30 minutos) y el nombre «Consulta: lo escrito», que es lo que ve el salón en la agenda y en los avisos. Sin JavaScript se usa el desplegable de siempre y un campo de texto para la consulta.

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
| GET/POST `/citas` | Solicitud pública, consentimiento, antifalsificación y límites por IP/teléfono; `?para=Hombre\|Mujer\|Otro` es para quién es la cita y `?otro=` lleva la consulta escrita |
| GET `/citas/disponibilidad` | Solo horas y duración |
| GET/POST `/citas/confirmar/{token}` | Revisión y aceptación privada; GET no modifica |
| GET `/admin/agenda`, `/admin/agenda/avisos` | Panel protegido por sesión con MFA |
| GET `/admin/agenda/huecos?dia=&minutos=` | Horas libres (y ocupadas) de un día para una cita de esa duración; lo usa el selector de horas de Nueva cita |
| POST `/admin/agenda/gestionar` | Validación y propuesta con control de versión |
| ~~POST `/admin/agenda/whatsapp-enviado`~~ (ahora en Comunicaciones) | Registro manual del envío, sin aceptar la cita |
| POST `/admin/comunicaciones/texto` | Guarda el texto retocado de un aviso aún no enviado |
| POST `/admin/comunicaciones/enviar` | Envía ahora un correo pendiente o rechazado (con el texto retocado, si se envía) |
| POST `/admin/comunicaciones/whatsapp-enviado` | Marca un WhatsApp como enviado desde el teléfono |
| POST `/admin/agenda/eliminar` | Elimina de la agenda una cita cerrada (cancelada, rechazada, no presentada o caducada) con sus avisos |

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

Conocer un teléfono o un correo no autoriza a consultar datos de clientes. Al salir del móvil o del correo, en cuanto los dos son válidos, se pide **un** código (`POST /citas/contacto/solicitar` con `telefono` y `correo`): el servidor busca una ficha inequívoca por el móvil y, si no la hay, por el correo (así se reconoce también a quien ha cambiado de móvil), y el código va exclusivamente al correo guardado en esa ficha, con la plantilla de la peluquería. La respuesta es siempre un identificador: nunca confirma si el contacto existe ni indica el correo destinatario. Al teclear la sexta cifra se verifica solo y se rellenan nombre, apellidos, correo y «¿Para quién es la cita?»; el móvil escrito se respeta. Cambiar después el móvil quita lo rellenado. Un contacto ambiguo o sin correo requiere rellenar los datos manualmente. Rellenar con solo escribir un teléfono o un correo, sin código, expondría datos personales de los clientes a cualquiera: no se hace.

El autocompletado se habilita cuando el transporte SMTP esté configurado y activado. Mientras tanto el formulario funciona con entrada manual. La biblioteca independiente Comunicaciones.Correo sigue siendo el transporte; VerificacionContactoService contiene las reglas y el proceso existente de correo atiende también esta cola.

Cada desafío caduca a los diez minutos, admite cinco intentos y es de un solo uso. Solicitar otro código invalida el anterior. Se limitan las solicitudes por IP, por teléfono y por correo de destino (tres códigos por hora); los códigos y la clave utilizada en su hash se cifran con Data Protection. Los endpoints son POST con antifalsificación y respuestas sin caché. Un cambio del contacto registrado invalida la verificación. Los resultados de envío inciertos no se reintentan automáticamente. No hay envíos de SMS ni consulta pública de fichas sin verificación.

La migración DatosClienteYVerificacionPublica amplía Nombre sin recortar datos y añade Apellido, Sexo y VerificacionesContacto. Las reservas históricas conservan su información anterior y las columnas nuevas vacías; se completan desde administración, sin inventar datos personales.


## Citas del salón, bloqueos y confirmación directa

**Nueva cita** (`/admin/agenda/nueva`) anota las citas dadas por teléfono o en persona. Se busca la ficha por nombre, teléfono o correo; si no tiene ficha basta con un nombre y, opcionalmente, un teléfono (se admiten fijos). Nace confirmada y se marca «Dada en el salón»; el aviso al cliente sigue la preferencia de su ficha (ver «Preferencia de aviso del cliente»). Al elegir el servicio su duración se escribe sola en «Duración» (se puede ajustar; sin servicio vale 30 minutos). La hora **solo se elige entre las libres**: el selector muestra, por mañana y tarde, las horas de inicio del horario de apertura en las que cabe esa duración sin solaparse con otra cita, desde ahora en adelante; las que pisan otra cita salen tachadas y no se pueden elegir. Al cambiar el día, el servicio o la duración, `nueva-cita.js` pide las horas otra vez a `GET /admin/agenda/huecos?dia=AAAA-MM-DD&minutos=N` y avisa si la hora que estaba elegida ya no cabe. El servidor repite la comprobación al guardar («Elige una de las horas libres de ese día.»), así que no se puede anotar una hora fuera del horario ni en un día cerrado o pasado. Las horas extra fuera del horario, si alguna vez hacen falta, se abren ampliando el horario de ese día en **Horarios**.

**Bloquear tiempo** reserva un rato sin cliente (descanso, médico, formación). Su hora se escribe libremente, porque no es una cita, pero tampoco puede solaparse con otra. La web deja de ofrecer esas horas; «Quitar bloqueo» las libera.

En una solicitud de la web, **Confirmar ya (acordada por teléfono)** confirma la cita sin esperar al enlace de aceptación, una vez validado el teléfono. Crea o vincula la ficha igual que la aceptación y deja en Comunicaciones un aviso informativo sin enlace. También sirve para reservas antiguas sin correo.

Las citas confirmadas o completadas con ficha muestran **Registrar visita**, que abre Nueva visita con el cliente elegido.

La migración `OrigenDeLasCitas` añade la columna `Origen` a `ReservasCitas` (0 web, 1 salón, 2 bloqueo). Las reservas existentes quedan como web. No borra ni modifica datos.

## Dualidad de servicio

Algunos servicios dejan tiempo libre mientras se hacen (un tinte que actúa, una mascarilla en reposo). En **Catálogo → Editar servicio**, la casilla **Dualidad de servicio** (solo en el panel; la web pública no la muestra ni la cambia) permite dar otras citas a la misma hora:

- Una cita de un servicio con dualidad **no ocupa la agenda**: otras citas pueden coincidir con ella, y ella puede darse a la hora de otras. Dos citas sin dualidad siguen sin poder solaparse.
- El **tiempo bloqueado ocupa siempre**: ni las citas con dualidad pueden caer en un bloqueo, ni se puede bloquear un rato que tenga citas (con o sin dualidad). Cerrar un día también exige que no tenga ninguna.
- Las horas libres de **Nueva cita** y de la reserva web tienen en cuenta el servicio elegido (`GET /admin/agenda/huecos?…&servicioId=N`); al elegir uno con dualidad lo indica («· dualidad» y un aviso).
- La cita guarda si es dual (`ReservasCitas.Dualidad`) y la agenda la marca con la etiqueta **Dualidad**. Al cambiar la casilla del servicio, cambian también sus citas que aún no han terminado (pendientes o confirmadas); las pasadas conservan cómo se dieron.

La migración `DualidadDeServicios` añade la columna `Dualidad` (`bit`, por defecto 0) a `ServiciosGenericos` y a `ReservasCitas`: nada cambia hasta que se marque un servicio.

## Preferencia de aviso del cliente

Cada ficha de **Clientes** tiene **Avisos de sus citas**: *Sin preferencia*, *Correo* (necesita email) o *WhatsApp* (necesita móvil). Se ve en la ficha y en el listado.

- **Con preferencia, los avisos de sus citas van siempre por ese canal**, se den en el salón o se pidan en la web:
  - **Web**: si quien pide la cita es una ficha reconocida sin dudas (la única con ese móvil o ese correo, y coincide en los dos), la solicitud se guarda con su canal preferido aunque marque el otro. Si se verifica con el código de la reserva, la página deja marcado su canal y bloquea el otro, con una nota. La propuesta, la confirmación y la cancelación usan la preferencia de la ficha vinculada al gestionarla.
  - **Panel**: en **Nueva cita**, con una ficha con preferencia se ve «Se le avisará por WhatsApp/correo, como indica su ficha» y no se elige nada; al guardar se crea el aviso de confirmación por ese canal.
- **Sin preferencia, se elige a mano**: en la web, el cliente marca correo o WhatsApp como hasta ahora; en Nueva cita, *Sin aviso*, *Correo* o *WhatsApp* (solo los canales de los que la ficha tiene el dato; sin ficha, WhatsApp necesita un móvil).
- Si una cita del salón se avisó al darla y después se cancela, se avisa también de la cancelación por el mismo canal y el aviso de confirmación que no había salido queda obsoleto.
- Los avisos quedan en **Comunicaciones**: el correo se envía desde allí (o solo, con el envío automático) y WhatsApp con su enlace, como el resto.

La migración `PreferenciaDeAviso` añade a `Clientes` la columna `CanalPreferido` (`int` que admite nulos: 0 correo, 1 WhatsApp; la restricción `CK_Clientes_CanalPreferido` no admite otros valores). Todas las fichas empiezan sin preferencia.

## Calendario del panel y Comunicaciones

`/admin/agenda` es un calendario mensual: cada día muestra cuántas citas tiene (abiertas, completadas, canceladas y bloqueos) y los días de cierre. Al pulsar un día se ven sus citas a la derecha, con el filtro **Abiertas · Canceladas · Completadas · Todas** (`?dia=AAAA-MM-DD&estado=…`). Las solicitudes pendientes de otros días se listan debajo. Una cita cerrada (cancelada, rechazada, no presentada o caducada) se puede **eliminar de la agenda** con sus avisos; una cita con falta registrada se conserva como historial.

`/admin/comunicaciones` reúne los avisos a clientes, fuera de la agenda: **Pendientes · Enviados · Todos**. Arriba, un buscador filtra mientras se escribe por destinatario o texto del aviso (`?q=`), sin distinguir mayúsculas ni acentos; los tres filtros conservan la búsqueda. Cada aviso pendiente se muestra con su texto en un cuadro editable:

- **Correo:** «Enviar correo» lo manda en ese momento con el texto que haya en el cuadro (el enlace de aceptación se añade solo, al final, y no se edita); «Guardar sin enviar» solo conserva el cambio. Los correos **no salen solos**: esperan a esta revisión. Si se prefiere el envío automático de antes, se activa con `PELUQUERIA_CORREO_AUTOMATICO=true` y la cola los manda cada 15 segundos.
- **WhatsApp:** «Guardar texto» actualiza el mensaje y el botón «Abrir en WhatsApp» lo lleva al teléfono; después, «Ya lo he enviado en WhatsApp».
- Un correo rechazado por el servidor queda editable para reenviarlo. Un envío incierto no se repite desde la app.

## Plantilla de los avisos

Los avisos hablan en nombre de la peluquería, no «del equipo»: «Tu cita en Lucía Peluquería Ecológica queda confirmada el viernes 16 de octubre a las 15:00, para…», con los teléfonos de **Contacto de la web** para cambios («llámanos al 659 306 659 o al 985 757 692»), también en WhatsApp. Una solicitud sin servicio se guarda como «Consulta en la peluquería».

El correo usa `PlantillaCorreoCita` (HTML de correo con tablas y estilos en línea, que respetan Gmail, Outlook y Apple Mail): cabecera morada con el logo incrustado (`cid:`, se ve aunque el lector bloquee imágenes externas), el texto revisado en Comunicaciones, una ficha con día, hora, servicio y dirección (con enlace al mapa), el botón que corresponda («Revisar y aceptar mi cita» en una propuesta, «Llamar al…» si se ha cancelado), los teléfonos para cambios y un pie con los datos del salón. El asunto cambia con el tipo de aviso: «Tu cita en Lucía Peluquería Ecológica está confirmada», «Tu propuesta de cita en…», «Sobre tu solicitud de cita en…». Va también en texto plano, con el enlace y el contacto al final. Nombre, dirección y mapa salen de `contenido-publico.json`; el logo, de `wwwroot/Img/Icono.jpg`.

## Correo con Gmail en producción y en desarrollo

La cuenta de envío es `luciaespina.p.e@gmail.com`. Gmail no admite la contraseña normal desde aplicaciones: hace falta una **contraseña de aplicación**, y para eso la cuenta necesita la verificación en dos pasos (https://myaccount.google.com/security, con el móvil del salón).

Hay **dos contraseñas de aplicación**, creadas en https://myaccount.google.com/apppasswords: `Peluqueria Prod` para la Web App y `Peluqueria Dev` para el PC de desarrollo. Así se puede revocar una sin romper la otra, y la de producción nunca está en un PC. Google enseña cada una una sola vez, en bloques de cuatro letras; los espacios no forman parte de ella.

**Producción.** Se hace desde tu PC, no en el servidor: en una terminal, en la carpeta del proyecto y con la sesión de Azure abierta (`az login`), ejecuta el script. No depende de publicar: la versión que esté en la Web App ya lee el secreto de Key Vault, así que el correo funciona en cuanto termina. El script pide la contraseña `Peluqueria Prod` oculta, la guarda en Key Vault (secreto `correo-smtp`), desactiva las versiones anteriores y reinicia la Web App, porque las referencias a Key Vault se guardan en caché:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/actualizar-correo-azure.ps1
```

La Web App lee el secreto con su identidad (rol *Key Vault Secrets User*) a través de estas variables de entorno, que ya están puestas:

| Nombre | Valor |
|---|---|
| `PELUQUERIA_CORREO_SMTP` | `@Microsoft.KeyVault(SecretUri=https://kv-lucia-peluqueria.vault.azure.net/secrets/correo-smtp/)` |
| `PELUQUERIA_CORREO_ACTIVO` | `true` |

**Desarrollo.** Con la depuración parada (el comando compila en `bin\Debug`), guarda `Peluqueria Dev` en el almacén de credenciales de Windows, cifrada para tu usuario y fuera del repositorio:

```powershell
dotnet run --project src/PeluqueriaLucia.Web -- configurar-correo
```

Respuestas: `smtp.gmail.com`, `587`, `StartTls`, `luciaespina.p.e@gmail.com`, la contraseña (oculta, con o sin espacios) y otra vez `luciaespina.p.e@gmail.com`. Los perfiles `http` y `https` de `launchSettings.json` ya activan el correo (`PELUQUERIA_CORREO_ACTIVO=true`) y dan una URL pública para los enlaces de aceptación (`PELUQUERIA_URL_PUBLICA=https://localhost:7178/`, que solo responde con el perfil `https`). Sin la credencial guardada no se envía nada. **Cuidado:** la base local tiene clientes reales y «Enviar correo» les escribe de verdad; prueba solo con avisos dirigidos a tu propio correo. Las pruebas automáticas sustituyen el envío por uno falso.

**Comprobar.** Gestiona una cita de prueba con tu propio correo, ve a **Comunicaciones** y pulsa «Enviar correo». Si el panel dice «El servidor de correo ha rechazado el envío», Gmail no acepta la contraseña (revocada, mal copiada o sin verificación en dos pasos): crea otra y repite el paso del entorno afectado.

Límites de Gmail: unos 500 correos al día por cuenta, de sobra para el salón. Cambiar la contraseña de la cuenta de Google revoca todas las contraseñas de aplicación: después hay que crear y guardar las dos de nuevo.
