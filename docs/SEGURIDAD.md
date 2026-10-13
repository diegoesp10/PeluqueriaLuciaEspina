# Acceso y seguridad del panel

## Rutas y capas

La presentación pública está en `Controllers/InicioController`, `Views/Inicio` y `wwwroot/css/publico.css`. El contacto se lee de `contenido-publico.json`; los tratamientos son una proyección de los servicios activos y publicados del catálogo común en SQL, y las opiniones proceden de reseñas aprobadas. Las respuestas públicas solo incluyen los campos públicos del servicio y de la opinión; no incluyen servicios internos, fotos internas, clientes, visitas ni tickets.

La gestión está en `Areas/Admin`: todos sus controladores heredan de `AdminController`, que exige la política `Administrador` y aplica el límite de llamadas del panel. Además, la política de autorización por defecto exige autenticación, incluso si se añade un controlador nuevo. Solo portada, servicios, acceso, errores y recursos estáticos propios permiten visitas anónimas.

La lógica de cuentas y sesiones vive en Application y Domain; los repositorios y el hash de contraseñas, en Infrastructure. No se han cambiado los nombres de las tablas de gestión existentes.

## Primera activación

1. Haz una copia de SQL y de la carpeta del sitio, como se explica en [DESPLIEGUE.md](DESPLIEGUE.md).
2. Aplica las migraciones pendientes:

   ```powershell
   dotnet tool restore
   dotnet ef database update --project src/PeluqueriaLucia.Infrastructure --startup-project src/PeluqueriaLucia.Web
   ```

3. Crea el administrador desde una consola del propio servidor:

   ```powershell
   # Desarrollo: desde la raíz del repositorio
   dotnet run --project src/PeluqueriaLucia.Web -- administrador

   # IIS: desde la carpeta publicada, con su configuración Production
   dotnet PeluqueriaLucia.Web.dll administrador
   ```

   Introduce un usuario y una contraseña de al menos 15 caracteres. La contraseña no aparece en pantalla; se confirma dos veces. No se envía por argumentos, URL ni logs. Si ese usuario ya existe, se cambia su contraseña y se revocan todas sus sesiones. Este comando también sirve para recuperar una cuenta bloqueada o una contraseña olvidada.

4. Abre `/admin/acceso`. Tras la contraseña, vincula Microsoft Authenticator escaneando el QR y confirma el código. Guarda los diez códigos de recuperación que se muestran una sola vez. Solo después se abre una sesión del panel. Consulta [CONTENIDO-Y-MFA.md](CONTENIDO-Y-MFA.md) para cambiar de móvil o recuperar MFA desde el servidor.

Para esta copia local, Codex ha aplicado la migración tras hacer y verificar una copia de SQL, y ha creado `lucia` con una contraseña aleatoria. Las credenciales están en `.artifacts/acceso-inicial.txt`, excluido de Git y accesible solo por tu usuario de Windows y SYSTEM. Guarda la contraseña en tu gestor y elimina ese archivo cuando no lo necesites. No se publican ni se incluyen en la distribución.

## Visual Studio y recarga del navegador

Los perfiles `http`, `https` e `IIS Express` de `Properties/launchSettings.json` excluyen los componentes que inyectan Browser Link y Browser Refresh. Sus conexiones a otros puertos locales chocan con `connect-src 'self'`; se elimina la inyección en lugar de ampliar la política de seguridad. También se suprime la recarga de navegador de `dotnet watch`. La depuración habitual sigue disponible; recarga manualmente el navegador para ver los cambios.

Si Visual Studio ya estaba ejecutando la aplicación cuando se modificó el perfil, detén y vuelve a iniciar esa ejecución. Después usa `Ctrl+F5` en el navegador para descartar los scripts de la página anterior. No hay que cambiar las opciones globales de Visual Studio ni desactivar la CSP.

## Token de uso del backend

Cada inicio de sesión crea un token aleatorio de 32 bytes mediante `RandomNumberGenerator`. SQL conserva el SHA-256 del token. El token y el identificador de sesión viajan dentro de una cookie cifrada por ASP.NET Core Data Protection, `HttpOnly`, `SameSite=Strict`, restringida a `/admin` y `Secure` cuando HTTPS está habilitado. No se entrega una clave fija a JavaScript ni se guarda en localStorage.

Todas las peticiones al panel, incluidas búsquedas JSON, exportaciones, tickets y fotos, validan el token contra una sesión abierta en SQL. Los tokens se comparan en tiempo constante. Las sesiones caducan tras 12 horas de inactividad y a los 14 días como máximo. Se permiten hasta diez sesiones activas por cuenta. Cerrar sesión revoca el token en SQL; cambiar la contraseña cierra las otras sesiones; restablecerla desde consola cierra todas.

El navegador envía automáticamente la cookie en las llamadas del mismo origen. Los POST además incluyen `__RequestVerificationToken` de ASP.NET Core; una cookie válida por sí sola no permite ejecutar cambios desde otra página. Las respuestas privadas llevan `Cache-Control: no-store`.

Las fotos históricas conservan `/imagenes/...` en SQL; las vistas las enlazan a `/admin/imagenes/...`, donde un controlador protegido verifica el acceso. Se han excluido de los recursos estáticos anónimos. No se publica ni sobrescribe el directorio físico de fotos.

## Cierre y expulsión del panel

En **Seguridad** se puede cerrar una sesión concreta (incluida **Esta sesión**), cerrar las otras o cerrar todas. Cerrar la actual o todas revoca el token en SQL, elimina la cookie y redirige inmediatamente a `/admin/acceso`. Los cierres requieren POST con antifalsificación y solo afectan a sesiones de la misma cuenta. Las cookies copiadas también quedan inutilizadas.

La validación de servidor se ejecuta en cada petición, sin intervalo de tolerancia, siguiendo el mecanismo [ValidatePrincipal de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0#react-to-back-end-changes). Se rechazan sesiones cerradas, caducadas, inexistentes, tokens ausentes o incorrectos e identidades que no coinciden con la sesión. Páginas y recargas redirigen al acceso; llamadas JSON devuelven 401. Las respuestas privadas siguen usando `no-store`.

Para las pantallas ya abiertas, `sesion.js` consulta `/admin/acceso/estado` al cargar, al recuperar el foco o la visibilidad y al restaurar el historial. Usa [pageshow](https://developer.mozilla.org/en-US/docs/Web/API/Window/pageshow_event) para revalidar también las páginas restauradas desde la memoria del navegador: `pagehide` oculta su contenido antes de guardarlo. Las pestañas del mismo navegador reciben un aviso tras un cierre confirmado; otras conexiones se comprueban cada 15 segundos mientras la pestaña esté visible. La comprobación automática no renueva el plazo de inactividad. Solo devuelve el identificador de sesión; el token permanece en la cookie HttpOnly.

Si la cookie cambia por otro inicio de sesión, se abandona la pantalla antigua. Si el servidor no responde, el panel oculta los datos y bloquea su uso hasta obtener una validación válida; ante límites de peticiones respeta `Retry-After`. La protección del backend no depende de JavaScript: sin él, cualquier nueva petición sigue exigiendo una sesión válida. Una petición ya autorizada antes de la revocación puede terminar; los datos que ya se descargaron o imprimieron no pueden retirarse del dispositivo.

## Contraseñas y intentos de acceso

Dentro del panel, abre **Cambiar contraseña** desde el dashboard, el menú lateral o **Seguridad** (en móvil, **Cuenta**). El formulario `/admin/Seguridad/Contrasena` exige la contraseña actual y la nueva por duplicado. Al guardar, la sesión actual sigue abierta y las demás se revocan; la contraseña anterior deja de permitir el acceso.

- PBKDF2-HMAC-SHA512 con sal aleatoria y 210.000 iteraciones, mediante el hasher de ASP.NET Core Identity. Los hashes antiguos se actualizan al entrar.
- Contraseñas de 15 a 256 caracteres, sin el nombre de usuario. No hay contraseña por defecto ni formulario de registro público.
- Bloqueo de cuenta durante 15 minutos tras cinco fallos. Las verificaciones incorrectas al cambiar contraseña también cuentan. Una columna SQL `rowversion` impide sobrescribir silenciosamente los contadores con accesos concurrentes.
- Mensaje de acceso genérico, sin revelar si existe el usuario. Se verifica un hash señuelo para usuarios inexistentes y cuentas bloqueadas.
- Auditoría persistente de cada intento con usuario, IP, fecha y resultado. Nunca almacena contraseñas ni tokens completos.

## Límites y filtro de IP

Se ajustan en la sección `Seguridad` de `appsettings.Production.json`:

```json
{
  "Seguridad": {
    "RequerirHttps": true,
    "ProxiesConfiables": [],
    "RedesPermitidasPanel": [],
    "Limites": {
      "PublicoPorMinuto": 120,
      "PanelPorMinuto": 400,
      "AccesoPorCuartoDeHora": 10,
      "TotalPorMinuto": 1500
    }
  }
}
```

Cada límite se aplica por IP con ventana deslizante, sin cola. Al agotarlo se devuelve HTTP 429 con `Retry-After`. El total incluye recursos estáticos; el monitor muestra páginas y llamadas al servidor de la última hora, así como rechazos. Conserva como máximo 2.000 IP distintas en memoria.

`RedesPermitidasPanel` admite IPs y redes CIDR, por ejemplo `127.0.0.1`, `::1` o `192.168.1.0/24`. Vacío permite acceder al formulario desde cualquier IP; las credenciales siguen siendo obligatorias. Las conexiones fuera de la lista reciben 404. Se normalizan las IPv4 que llegan como IPv6.

Las cabeceras `X-Forwarded-For` y `X-Forwarded-Proto` solo se procesan si se han configurado IPs concretas en `ProxiesConfiables`. No confíes en cabeceras enviadas directamente por clientes. En IIS se conserva la integración del servidor; si añades un proxy externo, declara su IP y limita el acceso directo a Kestrel. Ver la [documentación oficial de proxies de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

Los límites y contadores son por proceso y se reinician al reiniciar la aplicación. Esta instalación está pensada para un único proceso IIS/Kestrel. Si se escala a varias instancias, se necesitan límites compartidos en el proxy o un almacén distribuido. Las sesiones y los intentos sí persisten en SQL.

## HTTPS, claves y publicación

HTTPS está activado por defecto en producción: redirección, HSTS y cookies Secure. Desarrollo permite HTTP en localhost. Configura el dominio real en `AllowedHosts` y el certificado en IIS antes de publicar en internet.

Data Protection guarda las claves en `DataProtectionKeys`; en Windows se cifran con DPAPI a nivel de equipo. No borres esa tabla durante actualizaciones. Una restauración de SQL en otro equipo requiere generar nuevas claves o configurar un certificado de protección; las sesiones antiguas pueden dejar de descifrarse. El despliegue previsto es Windows/IIS. Para otros sistemas configura cifrado de claves con certificado. Ver [protección de claves en reposo](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest).

La política CSP permite scripts locales y scripts en línea solo con nonce, impide iframes, plugins y envíos de formularios a otros orígenes. Se añaden `nosniff`, `DENY`, política de referencia y restricciones de cámara/micrófono/geolocalización.

## Verificación

```powershell
dotnet test -c Release
node --test tests/PeluqueriaLucia.Tests/Web/sesion.test.cjs
dotnet publish src/PeluqueriaLucia.Web -c Release -r win-x64 -o .artifacts/publicacion
```

Las pruebas crean bases temporales independientes: conservación de datos al migrar, bloqueo, caducidad, revocación, tokens manipulados, cierre de sesión, contraseñas, cookies, CSRF, redirecciones, acceso anónimo, fotos, JSON, límites y cabeceras IP falsificadas. No reinician contraseñas ni modifican datos de la base real.
