# Publicación en Azure

La web se publica en **Azure App Service (Linux, B1)** con **Azure SQL (Basic)** y **Key Vault**, todo en *Spain Central* y en el grupo `rg-lucia-prod`, detrás de **Cloudflare** con el dominio `luciapeluqueriaecologica.com`. El IIS local sigue funcionando igual: la app detecta dónde está.

| Recurso | Nombre |
|---|---|
| Web App | `WebAppLuciaPeluqueria` (`webappluciapeluqueria-a9dycjgph4f0etaz.spaincentral-01.azurewebsites.net`) |
| Servidor SQL | `sql-lucia-peluqueria.database.windows.net`, solo autenticación Microsoft Entra |
| Base de datos | `PeluqueriaLuciaProd`, Basic 5 DTU, `Modern_Spanish_CI_AS` |
| Key Vault | `kv-lucia-peluqueria`, permisos por roles, protección de purga; clave `proteccion-datos` |

Ninguna pieza usa contraseñas: la Web App entra en SQL y en Key Vault con su **identidad administrada**, tú con tu cuenta (`az login`) y GitHub Actions con una **identidad federada**.

## Qué hace la app en Azure

| Necesidad | En el IIS local | En Azure |
|---|---|---|
| Cadena de conexión | Administrador de credenciales de Windows | Cadena `Peluqueria` (tipo SQLAzure) de la Web App, con `Authentication=Active Directory Managed Identity` |
| Cifrado de las claves (Authenticator, enlaces de citas, códigos de reseña) | DPAPI del equipo | Clave `proteccion-datos` de Key Vault (`ProteccionDatos__ClaveKeyVault`) |
| Contraseña SMTP | Administrador de credenciales | Variable `PELUQUERIA_CORREO_SMTP` como referencia a un secreto de Key Vault |
| HTTPS | IIS | Frontal de App Service (la app lee `X-Forwarded-Proto`) |
| IP del visitante | La de la conexión | Cabecera `CF-Connecting-IP` de Cloudflare (`Seguridad__CabeceraIpCliente`) |
| Fotos del catálogo y de reseñas | Carpeta del sitio | `/home/data`, que App Service conserva entre despliegues |
| Impresora térmica de tickets | El navegador la manda por USB (WebUSB) | Igual: el servidor solo construye el ticket ESC/POS; el navegador del salón (Chrome/Edge, HTTPS) lo envía a la impresora USB |
| Hora local | La del equipo | `TZ=Europe/Madrid` |

## 1. Configuración de la Web App

**Configuración → Configuración general → Comando de inicio:** `dotnet PeluqueriaLucia.Web.dll`

**Variables de entorno → Configuración de la aplicación:**

| Nombre | Valor | Notas |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Ya puesto |
| `TZ` | `Europe/Madrid` | Ya puesto |
| `ProteccionDatos__ClaveKeyVault` | `https://kv-lucia-peluqueria.vault.azure.net/keys/proteccion-datos` | Sin versión: Key Vault usa siempre la vigente |
| `Imagenes__Carpeta` | `/home/data/imagenes` | Fotos del catálogo |
| `FotosResenas__Carpeta` | `/home/data/resenas` | Fotos de las opiniones |
| `FotosProductos__Carpeta` | `/home/data/productos` | Fotos de los productos de la tienda. Solo en Azure: en desarrollo la carpeta sale de `appsettings.Development.json` (ver docs/PRODUCTOS.md) |
| `AllowedHosts` | `luciapeluqueriaecologica.com;www.luciapeluqueriaecologica.com;webappluciapeluqueria-a9dycjgph4f0etaz.spaincentral-01.azurewebsites.net` | Dominios que acepta la app |
| `Seguridad__CabeceraIpCliente` | `CF-Connecting-IP` | **Solo cuando Cloudflare esté delante y la Web App restringida a sus IPs** (paso 5) |
| `PELUQUERIA_URL_PUBLICA` | `https://luciapeluqueriaecologica.com/` | Cuando el dominio funcione |
| `PELUQUERIA_CORREO_SMTP` | `@Microsoft.KeyVault(SecretUri=https://kv-lucia-peluqueria.vault.azure.net/secrets/correo-smtp/)` | Referencia al secreto con el JSON de SMTP (ver docs/AGENDA.md, «Correo con Gmail en producción y en desarrollo»; se actualiza desde tu PC con `scripts/actualizar-correo-azure.ps1`) |
| `PELUQUERIA_CORREO_ACTIVO` | `true` | Activa el correo; sin él los avisos esperan en Comunicaciones |
| `PELUQUERIA_CORREO_AUTOMATICO` | `true` | Opcional: la cola envía los correos sola en vez de esperar a revisarlos |

**Cadenas de conexión:** `Peluqueria`, tipo `SQLAzure`:

```
Server=tcp:sql-lucia-peluqueria.database.windows.net,1433;Initial Catalog=PeluqueriaLuciaProd;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;
```

## 2. Identidad de GitHub Actions (una vez)

1. **Identidades administradas → Crear**: grupo `rg-lucia-prod`, Spain Central, nombre `id-lucia-github`.
2. En la identidad → **Credenciales federadas → Agregar**: escenario **GitHub Actions implementando recursos de Azure**; organización `diegoesp10`, repositorio `PeluqueriaLuciaEspina`, entidad **Entorno**, entorno `produccion`; nombre `github-produccion`.
3. En la **Web App → Control de acceso (IAM) → Agregar asignación de roles**: rol **Colaborador del sitio web**, miembro: identidad administrada `id-lucia-github`.
4. En GitHub, **Settings → Environments → New environment** `produccion` (opcional: *Required reviewers* con tu usuario, para aprobar cada publicación).
5. En **Settings → Secrets and variables → Actions → New repository secret**:
   - `AZURE_CLIENT_ID`: el *Id. de cliente* de `id-lucia-github`.
   - `AZURE_TENANT_ID`: `az account show --query tenantId -o tsv`.
   - `AZURE_SUBSCRIPTION_ID`: `az account show --query id -o tsv`.

No son contraseñas: solo identifican qué identidad usar. Azure acepta el inicio de sesión porque la petición viene de este repositorio y de ese entorno.

## 3. Migraciones y administrador (desde tu PC)

Las migraciones **no** las aplica GitHub Actions: se aplican a mano, antes de publicar el código que las necesita. Son aditivas, así que la versión anterior sigue funcionando mientras tanto. Requiere `az login` con tu cuenta (administradora del servidor SQL) y tu IP en el firewall del servidor.

```powershell
$env:ConnectionStrings__Peluqueria = "Server=tcp:sql-lucia-peluqueria.database.windows.net,1433;Initial Catalog=PeluqueriaLuciaProd;Encrypt=True;Authentication=Active Directory Default;"
dotnet ef database update --project src/PeluqueriaLucia.Infrastructure --startup-project src/PeluqueriaLucia.Web
```

Crear el administrador del panel contra Azure (pide usuario y contraseña ocultos):

```powershell
$env:ConnectionStrings__Peluqueria = "Server=tcp:sql-lucia-peluqueria.database.windows.net,1433;Initial Catalog=PeluqueriaLuciaProd;Encrypt=True;Authentication=Active Directory Default;"
$env:ProteccionDatos__ClaveKeyVault = "https://kv-lucia-peluqueria.vault.azure.net/keys/proteccion-datos"
dotnet run --project src/PeluqueriaLucia.Web -c Release -- administrador
```

Cierra la terminal al acabar (o `Remove-Item Env:ConnectionStrings__Peluqueria`) para que la app local vuelva a usar tu base local.

**Última migración aplicada en producción:** `TelefonosDeContacto` (15-oct-2026), que crea la tabla de los teléfonos de **Contacto de la web**. Hasta que se guarden teléfonos desde el panel publicado, la web sigue mostrando el de `contenido-publico.json`.

**Pendientes de aplicar antes de publicar los productos, la dualidad y la preferencia de aviso:** `ControlDeProductos` (crea `Productos`, `FotosProductos`, `MovimientosStock` y `ProductosVisitas`), `DualidadDeServicios` (añade la columna `Dualidad`, por defecto 0, a `ServiciosGenericos` y `ReservasCitas`) y `PreferenciaDeAviso` (añade `CanalPreferido`, que admite nulos, a `Clientes`). Ninguna borra ni cambia datos. Detalles en [PRODUCTOS.md](PRODUCTOS.md) y [AGENDA.md](AGENDA.md).

## 4. Publicar

- `main` está protegida: solo cambia por pull request (normalmente desde `develop`), con la comprobación «Compilar y probar» en verde y la aprobación del propietario del repositorio (`CODEOWNERS`); el propietario puede fusionar las suyas sin aprobación. `develop` no lanza ningún workflow.
- Cada pull request a `main` compila y pasa las pruebas en Linux con un SQL Server temporal.
- Al fusionar en **`main`**, el workflow repite las pruebas, espera la aprobación del entorno `produccion` en Actions («Review deployments»), publica en Azure y comprueba que la web responde. Tarda unos 5 minutos. La comprobación se hace contra el origen (`azurewebsites.net`) abriendo paso a la IP del runner en las restricciones de acceso solo durante el job, porque Cloudflare desafía a las IP de centros de datos con un 403; la consulta por el dominio público es solo informativa.
- También se puede lanzar a mano: **Actions → Pruebas y despliegue en Azure → Run workflow** (sobre `main`).

## 5. Cloudflare (dominio)

1. En la Web App → **Dominios personalizados → Agregar**: `luciapeluqueriaecologica.com` y `www.luciapeluqueriaecologica.com`. Azure indica un registro **TXT `asuid`** y un **A** o **CNAME**; créalos en Cloudflare con la nube en **gris** (solo DNS) hasta que Azure valide el dominio.
2. Cloudflare → **SSL/TLS → Servidores de origen → Crear certificado** (15 años). Conviértelo a PFX y súbelo en la Web App → **Certificados → Traer su propio certificado**; enlázalo a los dos dominios (SNI).
3. Cloudflare → **SSL/TLS → modo Completo (estricto)**; activa la nube **naranja** en los registros.
4. Web App → **Redes → Restricciones de acceso**: permite solo los rangos de IP de Cloudflare (https://www.cloudflare.com/ips/) y deniega el resto. Desde ese momento, añade `Seguridad__CabeceraIpCliente = CF-Connecting-IP`.

## Coste aproximado

App Service B1 Linux ~13 $/mes + Azure SQL Basic ~5 $/mes + Key Vault céntimos. Conviene un presupuesto con aviso (Administración de costos → Presupuestos).
