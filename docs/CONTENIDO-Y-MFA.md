# Authenticator, catálogo público y opiniones

## MFA obligatorio

El acceso consta de contraseña y TOTP de seis dígitos, SHA-1, periodo de 30 segundos, compatible con [Microsoft Authenticator](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/mfa?view=aspnetcore-10.0). El primer acceso presenta un QR local; en la app selecciona **Añadir cuenta → Otra cuenta**. Es necesario confirmar un código generado por esa cuenta. El secreto no se envía a servicios externos de QR.

La contraseña genera únicamente una cookie de verificación cifrada, HttpOnly y SameSite Strict, limitada a cinco minutos, al mismo IP y a cinco fallos. Esta cookie no permite consultar el panel. Solo el segundo factor genera el token administrativo. La migración revoca las sesiones previas a esta actualización.

Los secretos TOTP se cifran con Data Protection. Los diez códigos de recuperación son aleatorios, de un solo uso, y se almacenan mediante hash; se muestran una sola vez y no se imprimen en logs. Se acepta una tolerancia de un periodo por reloj. Un código ya usado no se vuelve a aceptar. Las versiones de fila de SQL impiden consumir el mismo desafío o código simultáneamente. Sincroniza el reloj del servidor. Siguen activos el bloqueo de cuenta, CSRF, los límites por IP, la revocación por petición y la comprobación de las sesiones en pestañas.

Cambiar la contraseña exige contraseña actual, contraseña nueva confirmada y un TOTP fresco. **Seguridad → Vincular otro Authenticator** exige contraseña y código actual o de reserva. La app anterior sigue activa hasta confirmar el nuevo QR; al confirmarlo se revocan las sesiones anteriores y los códigos de reserva antiguos. Guarda los nuevos códigos de recuperación.

Si se pierde el móvil, utiliza un código de recuperación para acceder y vincula otro. Si también se pierden los códigos, desde una consola del servidor ejecuta:

```powershell
dotnet run --project src/PeluqueriaLucia.Web -- recuperar-mfa
# Desde la carpeta publicada:
dotnet PeluqueriaLucia.Web.dll recuperar-mfa
```

Pide usuario y contraseña actual sin mostrarlos. Revoca MFA y sesiones; el siguiente acceso exige un nuevo QR confirmado. No existe endpoint web para esta recuperación. El comando `administrador` restablece una contraseña olvidada y cierra las sesiones; conserva el MFA existente.

## Servicios y precios de la web

**Catálogo** contiene todos los servicios para visitas y cobros. En su ficha se editan el nombre y el precio compartidos, y se decide si se publica en la web, con categoría, descripción, duración, precio fijo, desde o consultar, destacado y orden. Se pueden crear servicios internos sin publicarlos. **Servicios de la web → Publicar del catálogo** permite elegir un servicio existente sin duplicarlo; editarlo abre su ficha común. Retirarlo de la web conserva el servicio para cobrarlo.

Las categorías conservan su identificador de enlace al renombrarlas; no se puede borrar una asignada a servicios. Los cambios se guardan en SQL y se reflejan en `/servicios` y `/api/tratamientos`. La migración incorpora los 41 servicios públicos al catálogo, reutilizando los existentes con el mismo nombre sin sustituir su precio ni su foto. Las siguientes cargas no sobreescriben lo editado.

Al seleccionar un servicio en **Nueva visita**, aparece el importe aplicable a esa visita. Puede ajustarse sin cambiar el precio del catálogo. Un servicio por definir puede tener precio de catálogo cero, pero requiere un importe positivo para registrar la visita. Cada línea guarda su nombre y precio históricos; editar, retirar o archivar el servicio no cambia tickets anteriores. Archivar un servicio lo oculta también de la web.

## Opiniones con ticket

Al abrir un ticket cobrado se genera un código aleatorio de 128 bits, válido durante seis meses desde la emisión del código. Las reimpresiones conservan el mismo código, incluso simultáneas. SQL conserva un hash para verificarlo y una copia cifrada para reimprimirlo. Un ticket admite una sola opinión y no vuelve a habilitarse si esta se rechaza. Los tickets pendientes no habilitan opiniones.

En `/opiniones` se introducen código, nombre público, estrellas, comentario y foto opcional del peinado, con consentimiento de publicación. La comprobación del código no muestra datos de la visita. Las opiniones se guardan pendientes en **Reseñas**; allí se pueden publicar, retirar y responder. Las valoraciones negativas también son válidas: la revisión debe comprobar contenido pertinente y datos personales, sin seleccionar únicamente reseñas favorables.

Las fotos admiten JPG, PNG y WebP, hasta 5 MB y 20 megapíxeles. Se decodifican y se generan de nuevo como JPEG de hasta 1600 píxeles, con orientación corregida y sin metadatos del original. Se rechazan archivos dañados, animados y formatos ejecutables. Los nombres son aleatorios y el almacén queda fuera de `wwwroot`. El controlador público solo entrega fotos de opiniones publicadas; retirarlas retira también su foto pública. Las fotos pendientes solo se consultan en administración. Las subidas fallidas se limpian.

## Endpoints

| Método y ruta | Función / protección |
|---|---|
| GET/POST `/admin/acceso` | Contraseña; POST con CSRF y límite por IP |
| GET/POST `/admin/acceso/verificar` | QR inicial o segundo factor; desafío cifrado, POST con CSRF |
| GET/POST `/admin/Seguridad/Contrasena` | Sesión MFA; cambio con contraseña actual y TOTP |
| GET/POST `/admin/Seguridad/Authenticator` | Sesión MFA; cambio de app |
| GET `/api/tratamientos` | Solo categorías y tratamientos publicados |
| GET/POST `/admin/Catalogo/Nuevo`, `/admin/Catalogo/Editar/{id}` | Ficha común de nombre, precio y publicación; sesión MFA y POST con CSRF |
| GET `/admin/Tratamientos` | Gestión protegida de publicaciones del catálogo |
| GET/POST `/admin/Tratamientos/Nuevo` | Publicar un servicio existente; sesión MFA y POST con CSRF |
| GET `/admin/Tratamientos/Editar/{id}` | Redirige a la ficha común del catálogo |
| POST `/admin/Tratamientos/Editar/{id}` | Actualiza configuración pública del servicio existente; sesión MFA y CSRF |
| POST `/admin/Tratamientos/Eliminar/{id}` | Retira la publicación conservando el servicio; sesión MFA y CSRF |
| GET `/admin/Tratamientos/Categorias`, GET/POST `/Categoria/{id?}` | Gestión de categorías protegida |
| POST `/admin/Tratamientos/EliminarCategoria/{id}` | Categoría vacía; sesión MFA y CSRF |
| GET/POST `/opiniones` | Listado/formulario; POST con CSRF, ticket y 6 intentos/hora/IP |
| GET `/opiniones/foto/{guid}` | Solo foto de opinión publicada |
| GET `/admin/Resenas`, GET `/admin/Resenas/Foto/{guid}` | Revisión y fotos privadas; sesión MFA |
| POST `/admin/Resenas/Moderar/{guid}` | Publicar/retirar/responder; sesión MFA y CSRF |

## Copias y despliegue

Aplica `20261010130205_MfaTratamientosYResenas` y `20261010135843_CatalogoUnificadoConWeb` tras copiar y verificar SQL. La primera añade MFA y reseñas, inicializa los servicios públicos y cierra las sesiones existentes. La segunda incorpora esos servicios al catálogo común con sus campos públicos. Conserva clientes, visitas, cobros y precios históricos; al reutilizar un servicio existente, conserva su precio de catálogo y su foto.

Conserva SQL, sus claves Data Protection, las fotos internas y **AlmacenResenas**. `FotosResenas:Carpeta` elige un directorio persistente, con escritura solo para el proceso del sitio. La publicación excluye estos archivos. Las claves SQL cifradas con DPAPI necesitan el mismo equipo o un plan explícito de migración de claves; perderlas impide recuperar secretos TOTP y códigos imprimibles. En producción exige HTTPS y configura límites y proxies según [SEGURIDAD.md](SEGURIDAD.md).
