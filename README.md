# Peluquería Lucía

Aplicación de gestión interna de la Peluquería Ecológica Lucía Espina: visitas y cobros del día, clientes con su historial, catálogo de servicios con precios, tickets de caja y exportación a Excel para la gestoría.

Web pública del salón y gestión privada con contraseña y Authenticator obligatorio. Corre con ASP.NET Core 10 y SQL Server, en el ordenador de la peluquería o en su servidor IIS.

- `/`: presentación del salón, logo, foto real, contacto y reservas en Booksy.
- `/servicios`: servicios y precios publicados, almacenados en SQL y editables desde administración.
- `/opiniones`: reseñas con estrellas y fotos opcionales, verificadas mediante un código de ticket cobrado.
- `/admin`: dashboard, visitas, clientes, catálogo, cobros y exportaciones. Requiere una sesión válida.
- Las visitas y sus exportaciones se pueden filtrar por día, mes, año o todas las fechas. El calendario permite cambiar directamente de mes y año.
- `/admin/Seguridad`: sesiones, contraseña con MFA, cambio de Authenticator, intentos de acceso y llamadas por IP.
- `/admin/Tratamientos`: categorías, duración, orden y publicación de servicios elegidos del catálogo común.
- `/admin/Resenas`: revisión, publicación, retirada y respuesta a las opiniones.

La separación de capas existente se conserva; la gestión se ha trasladado al área MVC `Areas/Admin`. Ver [docs/SEGURIDAD.md](docs/SEGURIDAD.md) para activar o recuperar el acceso.

## Qué hace

- **Visitas**: registro de lo que se le hace a cada cliente, por día, por mes o histórico completo, con lo facturado, lo cobrado (efectivo y tarjeta) y lo pendiente.
- **Cobro** en efectivo, con tarjeta o mixto. En el mixto, al escribir una parte se completa la otra y no deja cobrar si no cuadra.
- **Ticket** de caja imprimible (factura simplificada). Los datos fiscales salen de la sección `Negocio` de `appsettings.json`.
- **Clientes** con buscador, ficha, historial de visitas y total gastado.
- **Catálogo** con fotos. Cambiar un precio no altera las visitas ya registradas, porque cada visita guarda el nombre y el precio de cada servicio. Un servicio que aparece en visitas se archiva en lugar de borrarse.
- **Exportación a Excel** del periodo que se está viendo.

## Arquitectura

Capas al estilo de una arquitectura limpia. Cada capa solo conoce a la de debajo:

```
src/
  PeluqueriaLucia.Domain/          Entidades y reglas del negocio (Cliente, Servicio, Visita, LineaVisita). Sin dependencias.
  PeluqueriaLucia.Application/     Casos de uso (ClientesService, CatalogoService, VisitasService) y los contratos que
                                   necesitan (repositorios, almacén de imágenes, exportador). Devuelven Resultado, no excepciones.
  PeluqueriaLucia.Infrastructure/  EF Core + SQL Server, migraciones, fotos en disco y Excel (ClosedXML).
  PeluqueriaLucia.Web/             ASP.NET Core MVC: web pública y área Admin protegida, vistas Razor, CSS y JS propios.
                                   Program.cs solo arranca; Startup.cs registra las capas y Configuracion/ tiene el detalle.
tests/
  PeluqueriaLucia.Tests/           Dominio, formato web e integración contra SQL Server (incluida la migración con datos).
```

Decisiones importantes:

- **Base de datos con datos reales.** Las tablas conservan sus nombres de la versión 1 (`ServiciosClientes`, `ServiciosGenericos`…) y las entidades se mapean a ellas. Las migraciones solo añaden. Ver [docs/DESPLIEGUE.md](docs/DESPLIEGUE.md).
- **Sin dependencias de internet.** La versión 1 cargaba Bootstrap, jQuery, Select2, Flatpickr y Font Awesome desde CDN: si se caía la conexión de la peluquería, la aplicación se rompía. Ahora todo (estilos, iconos y scripts) va dentro de la aplicación.
- **Carga rápida.** CSS y JS se sirven comprimidos (Brotli) y con caché de un año gracias a la huella en la URL; al arrancar se prepara la conexión con la base de datos, y la publicación va compilada con ReadyToRun. El panel y sus fotos se sirven con `Cache-Control: no-store`.
- **Acceso al backend.** Cada acceso crea un token aleatorio de 256 bits en una cookie cifrada y HttpOnly; SQL guarda solo su hash. Cada petición comprueba caducidad y revocación. Hay protección CSRF, bloqueo de cuenta y límites por IP.
- **Importes independientes del idioma de Windows.** Los formularios aceptan `12,50` y `12.50`. La aplicación fija la cultura `es-ES` para fechas e importes.

## Diseño

Una identidad compartida para la web, el login y la administración: crema, verde bosque y oliva, títulos en Georgia y controles claros con tipografía del sistema. Conserva las superficies agrupadas, los controles segmentados y la suavidad del panel inspirado en Apple, adaptados a la marca del salón. El login se presenta en una tarjeta centrada y las pantallas de contenido aprovechan todo el ancho. Día, noche y automático comparten la misma elección entre ambas capas; en móvil, las opciones de cuenta y apariencia están en el menú Cuenta. Los tokens y componentes comunes están en `wwwroot/css/tokens.css` y `componentes.css`. Criterios para futuras pantallas en [docs/DISENO.md](docs/DISENO.md). Avisos legales en [NOTICE.md](NOTICE.md).

## Desarrollo

Requisitos: SDK de .NET 10 y SQL Server local (la cadena de desarrollo está en `appsettings.Development.json`: `localhost`, base de datos `PeluqueriaLuciaDb`, autenticación de Windows).

```powershell
dotnet tool restore
dotnet ef database update --project src/PeluqueriaLucia.Infrastructure --startup-project src/PeluqueriaLucia.Web
dotnet run --project src/PeluqueriaLucia.Web -- administrador
dotnet run --project src/PeluqueriaLucia.Web
```

El comando `administrador` pide usuario y contraseña en consola, sin mostrarla. Crea el usuario o restablece su contraseña revocando sus sesiones. No hay contraseña por defecto ni registro público.

Pruebas (crean y borran bases temporales aisladas en SQL Server; otro servidor con `PELUQUERIA_PRUEBAS_SQL`). Incluyen migraciones, dominio y flujos HTTP de seguridad:

```powershell
dotnet test
```

El contacto y las reservas se editan en `src/PeluqueriaLucia.Web/contenido-publico.json`. **Catálogo** es la fuente común de nombres y precios: todos los servicios de la web se pueden seleccionar al registrar visitas, y se pueden crear servicios internos sin publicarlos. La ficha del catálogo permite publicar, categorizar y describir cada servicio; **Servicios de la web** permite elegir uno ya existente. Cambiar un precio no altera visitas ni cobros anteriores. Para precios por definir, se introduce el importe acordado en la visita. Las reseñas proceden de tickets reales y se moderan desde **Reseñas**. Logo y foto se sirven desde archivos locales.

En el primer acceso, escanea el QR con Microsoft Authenticator (**Añadir cuenta → Otra cuenta**) y confirma el código de seis dígitos. Guarda los diez códigos de recuperación que se muestran una sola vez. En accesos posteriores se exigen contraseña y código. Para recuperar un móvil perdido utiliza un código de reserva y **Seguridad → Vincular otro Authenticator**. La recuperación excepcional desde el servidor es `dotnet run --project src/PeluqueriaLucia.Web -- recuperar-mfa`; pide la contraseña y revoca todas las sesiones, sin abrir acceso sin MFA. Detalles y endpoints en [docs/CONTENIDO-Y-MFA.md](docs/CONTENIDO-Y-MFA.md).

### Cambios en la base de datos

1. Cambia la entidad y su configuración en `Infrastructure/Persistencia/Configuraciones.cs`.
2. Crea la migración:

   ```powershell
   dotnet ef migrations add NombreDelCambio --project src/PeluqueriaLucia.Infrastructure --startup-project src/PeluqueriaLucia.Web --output-dir Persistencia/Migraciones
   ```

3. **Revisa el código generado.** Nada de `DropTable`, `DropColumn` ni `RenameColumn` sobre datos existentes. Una columna nueva obligatoria lleva valor por defecto o un `UPDATE` que la rellene. Ese `UPDATE` va dentro de `EXEC(N'…')`, para que se compile cuando las columnas nuevas ya existen.
4. `dotnet test`: la prueba `El_modelo_coincide_con_las_migraciones` falla si se te olvida el paso 2, y las de `MigracionTests` comprueban la actualización desde la versión anterior.
