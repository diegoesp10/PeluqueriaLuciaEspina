# Despliegue en el equipo de la peluquería

La aplicación corre en el propio ordenador de la peluquería, en IIS, contra un SQL Server local. Solo la usa una persona en ese equipo, así que no hay usuarios ni autenticación.

> **Regla de oro:** la base de datos tiene datos reales que no se pueden perder. Antes de cada actualización se hace una copia de seguridad, y las migraciones solo **añaden** (nunca borran ni renombran tablas o columnas). Hay una prueba automática que reproduce la base de datos de la versión anterior con datos, la migra y comprueba que no se pierde nada (`tests/.../Integracion/MigracionTests.cs`).

## Qué hay que tener instalado en ese equipo

- IIS con el **ASP.NET Core Hosting Bundle 8** (ya estaba instalado para la primera versión).
- SQL Server con la base de datos `PeluqueriaLuciaDb`.
- SQL Server Management Studio (SSMS) o `sqlcmd`, para la copia de seguridad y el script de migraciones.

## Paso a paso

### 1. En tu equipo: generar la publicación y el script de la base de datos

```powershell
dotnet publish src/PeluqueriaLucia.Web -c Release -o artifacts/publicacion
./scripts/generar-script-migraciones.ps1
```

Quedan en `artifacts/`:

- `publicacion/`: la aplicación lista para copiar. **No** incluye `wwwroot/imagenes` (las fotos del catálogo) ni `appsettings.Production.json`, para no pisar nunca los del equipo de la peluquería.
- `migraciones.sql`: script **idempotente**. Aplica solo las migraciones que falten y se puede ejecutar varias veces sin efecto.

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

En SSMS abre `migraciones.sql` y ejecútalo sobre `PeluqueriaLuciaDb`, o con `sqlcmd`:

```powershell
sqlcmd -S localhost -E -C -d PeluqueriaLuciaDb -b -i migraciones.sql
```

Todo va dentro de una transacción: si algo fallara, no se aplica nada y la base de datos queda como estaba.

Qué hace la migración `HistoricoPreciosYCatalogoArchivable` (versión 2):

| Cambio | Efecto en los datos existentes |
|---|---|
| Columnas `NombreAplicado` y `PrecioAplicado` en `ServiciosGenericosRealizados` | Se rellenan con el nombre y el precio **actuales** del catálogo, que es justo lo que la versión 1 mostraba en tickets y totales. Desde ahora, cada visita guarda su propio precio. |
| Columna `Activo` en `ServiciosGenericos` | Todos los servicios existentes quedan activos. |
| Índices en `ServiciosClientes.Fecha` y `Clientes(Nombre, Apellido)` | Ninguno; solo aceleran las búsquedas. |
| Las claves foráneas de clientes y servicios pasan de `CASCADE` a `NO ACTION` | Ninguno sobre los datos. Desde ahora la base de datos impide borrar un cliente o un servicio que tenga visitas (antes se habrían borrado sus visitas en cascada). |

> Si un servicio cambió de precio **antes** de esta actualización, sus visitas antiguas toman el precio actual, igual que hacía la versión 1. Los cobros guardados (`TotalEfectivo` y `TotalTarjeta`) no se tocan.

Como alternativa al script, se puede poner `"BaseDeDatos": { "AplicarMigracionesAlArrancar": true }` en `appsettings.Production.json`: la aplicación aplicará las migraciones pendientes al arrancar. Es más cómodo, pero con el script se ve y se controla lo que se ejecuta.

### 5. Copiar la aplicación

1. Para el sitio en IIS (o deja un `app_offline.htm` en su carpeta).
2. Copia el contenido de `artifacts/publicacion` sobre la carpeta del sitio. **No borres** antes la carpeta: así se conservan `wwwroot\imagenes` y `appsettings.Production.json`.
3. Arranca el sitio y abre la aplicación. La pantalla de inicio es la de *Visitas de hoy*.

Los ficheros de la versión 1 que ya no se usan (`PelqueriaLuciaEspina.dll` y similares) no molestan; se pueden borrar más adelante.

## Volver atrás

1. Para el sitio.
2. Restaura la copia de seguridad de la base de datos (SSMS → *Restaurar base de datos…*).
3. Devuelve la carpeta `PeluqueriaLucia-anterior` a su sitio.

## Dónde mirar si algo falla

- *Visor de eventos de Windows* → *Registros de aplicaciones* → origen *IIS AspNetCore Module V2*.
- Para más detalle, en el `web.config` del sitio pon `stdoutLogEnabled="true"` y crea la carpeta `logs`.
