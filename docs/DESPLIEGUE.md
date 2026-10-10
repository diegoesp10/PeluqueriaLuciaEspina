# Despliegue en el equipo de la peluquería

La aplicación corre en IIS contra SQL Server. La portada y los servicios son públicos; toda la gestión está dentro de `/admin` y exige autenticación. El servidor de producción debe tener un certificado HTTPS. Ver [SEGURIDAD.md](SEGURIDAD.md) para los ajustes de acceso.

> **Regla de oro:** la base de datos tiene datos reales que no se pueden perder. Antes de cada actualización se hace una copia de seguridad, y las migraciones solo **añaden** (nunca borran ni renombran tablas o columnas). Hay una prueba automática que reproduce la base de datos de la versión anterior con datos, la migra y comprueba que no se pierde nada (`tests/.../Integracion/MigracionTests.cs`).

## Qué hay que tener instalado en ese equipo

- IIS con el **ASP.NET Core Hosting Bundle 10** ([descarga oficial de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0), apartado *Hosting Bundle*). La versión 1 usaba .NET 8: hay que instalar el 10 una vez y reiniciar IIS (`iisreset`). Pueden convivir los dos.
- SQL Server con la base de datos `PeluqueriaLuciaDb`.
- SQL Server Management Studio (SSMS) o `sqlcmd`, para la copia de seguridad.

## Paso a paso

### 1. En tu equipo: generar la publicación

```powershell
dotnet publish src/PeluqueriaLucia.Web -c Release -r win-x64 -o artifacts/publicacion
```

`-r win-x64` publica el código ya compilado para Windows (ReadyToRun): la aplicación arranca bastante más rápido porque no tiene que compilarlo en la primera petición. Sigue usando el .NET instalado en el equipo.

`artifacts/publicacion` es la aplicación lista para copiar. **No** incluye `wwwroot/imagenes` (las fotos del catálogo) ni `appsettings.Production.json`, para no pisar nunca los del equipo de la peluquería.

### 2. En el equipo de la peluquería: copia de seguridad

En SSMS: botón derecho sobre `PeluqueriaLuciaDb` → *Tareas* → *Copia de seguridad…*, o con `sqlcmd`:

```powershell
sqlcmd -S localhost -E -C -Q "BACKUP DATABASE PeluqueriaLuciaDb TO DISK = N'C:\CopiasPeluqueria\PeluqueriaLuciaDb-antes-de-actualizar.bak' WITH INIT"
```

Haz también una copia de la carpeta actual del sitio de IIS (por ejemplo, `C:\inetpub\PeluqueriaLucia` → `C:\inetpub\PeluqueriaLucia-anterior`).

### 3. Solo la primera vez que se pasa de la versión 1 a la 2: la configuración

En la versión 1 la cadena de conexión estaba dentro de `appsettings.json`. Ahora va aparte, en `appsettings.Production.json`, que no está en el repositorio y no se sobrescribe al publicar.

1. Abre el `appsettings.json` **del sitio actual** y copia el valor de `ConnectionStrings:DefaultConnection`.
2. En la carpeta del sitio crea `appsettings.Production.json` tomando como modelo `appsettings.Production.example.json`, y pega ahí la cadena:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=PeluqueriaLuciaDb;User Id=…;Password=…;TrustServerCertificate=True;"
     }
   }
   ```

IIS arranca la aplicación en entorno *Production*, que lee este fichero automáticamente. Si falta, la aplicación no arranca y el registro de IIS lo indica con un mensaje claro.

### 4. Actualizar la base de datos

Desde la carpeta del repositorio, con `dotnet ef database update` apuntando a la base de datos de la peluquería (la misma cadena de conexión del paso 3):

```powershell
dotnet tool restore
dotnet ef database update --project src/PeluqueriaLucia.Infrastructure --startup-project src/PeluqueriaLucia.Web --connection "Server=localhost;Database=PeluqueriaLuciaDb;User Id=…;Password=…;TrustServerCertificate=True;"
```

Se ejecuta en el equipo de la peluquería (necesita el SDK de .NET), o desde el tuyo si llegas a su SQL Server por la red (cambiando `localhost` por el nombre de su equipo). Solo aplica las migraciones que falten: si ya está al día, no hace nada. Para ver antes cuáles faltan:

```powershell
dotnet ef migrations list --project src/PeluqueriaLucia.Infrastructure --startup-project src/PeluqueriaLucia.Web --connection "…"
```

Qué hace la migración `HistoricoPreciosYCatalogoArchivable` (versión 2):

| Cambio | Efecto en los datos existentes |
|---|---|
| Columnas `NombreAplicado` y `PrecioAplicado` en `ServiciosGenericosRealizados` | Se rellenan con el nombre y el precio **actuales** del catálogo, que es justo lo que la versión 1 mostraba en tickets y totales. Desde ahora, cada visita guarda su propio precio. |
| Columna `Activo` en `ServiciosGenericos` | Todos los servicios existentes quedan activos. |
| Índices en `ServiciosClientes.Fecha` y `Clientes(Nombre, Apellido)` | Ninguno; solo aceleran las búsquedas. |
| Las claves foráneas de clientes y servicios pasan de `CASCADE` a `NO ACTION` | Ninguno sobre los datos. Desde ahora la base de datos impide borrar un cliente o un servicio que tenga visitas (antes se habrían borrado sus visitas en cascada). |

> Si un servicio cambió de precio **antes** de esta actualización, sus visitas antiguas toman el precio actual, igual que hacía la versión 1. Los cobros guardados (`TotalEfectivo` y `TotalTarjeta`) no se tocan.

Como alternativa, se puede poner `"BaseDeDatos": { "AplicarMigracionesAlArrancar": true }` en `appsettings.Production.json` y la aplicación aplicará las migraciones pendientes al arrancar. Con `dotnet ef database update` se controla mejor cuándo se ejecutan.

### 5. Copiar la aplicación

1. Para el sitio en IIS (o deja un `app_offline.htm` en su carpeta).
2. Copia el contenido de `artifacts/publicacion` sobre la carpeta del sitio. **No borres** antes la carpeta: así se conservan `wwwroot\imagenes` y `appsettings.Production.json`.
3. Desde la carpeta publicada ejecuta `dotnet PeluqueriaLucia.Web.dll administrador` para crear el usuario o restablecer su contraseña, sin mostrarla ni pasarla como argumento.
4. Arranca el sitio y abre la aplicación. La raíz muestra el salón; `/admin` abre el acceso al panel.

La migración `SeguridadAdministrador` solo crea `Administradores`, `SesionesAdministrador`, `IntentosAcceso` y `DataProtectionKeys`; no modifica las tablas de clientes, servicios, visitas o cobros. Aplícala antes de arrancar esta versión. Las claves de cookies se guardan en SQL y, en Windows, se cifran con DPAPI para el equipo; la herramienta de consola y el grupo de IIS deben ejecutarse en el mismo equipo.

Los ficheros de la versión 1 que ya no se usan (`PelqueriaLuciaEspina.dll` y similares) no molestan; se pueden borrar más adelante.

## Que la primera pantalla del día no tarde

IIS apaga la aplicación tras 20 minutos sin uso y la vuelve a arrancar con la siguiente petición, que entonces tarda unos segundos. Para que esté siempre lista (una vez, en el *Administrador de IIS*):

1. *Grupos de aplicaciones* → el grupo del sitio → *Configuración avanzada*:
   - *Modo de inicio*: `AlwaysRunning`.
   - *Tiempo de espera de inactividad (minutos)*: `0`.
2. *Sitios* → el sitio → *Configuración avanzada* → *Precarga habilitada*: `True`.
   Requiere la característica de Windows *Inicialización de aplicaciones* de IIS (*Activar o desactivar las características de Windows* → *Internet Information Services* → *Servicios World Wide Web* → *Características de desarrollo de aplicaciones*).

Al arrancar, la aplicación prepara la conexión con la base de datos antes de atender a nadie, así que la primera pantalla ya sale rápida.

## Volver atrás

1. Para el sitio.
2. Restaura la copia de seguridad de la base de datos (SSMS → *Restaurar base de datos…*).
3. Devuelve la carpeta `PeluqueriaLucia-anterior` a su sitio.

## Dónde mirar si algo falla

- *Visor de eventos de Windows* → *Registros de aplicaciones* → origen *IIS AspNetCore Module V2*.
- Para más detalle, en el `web.config` del sitio pon `stdoutLogEnabled="true"` y crea la carpeta `logs`.
