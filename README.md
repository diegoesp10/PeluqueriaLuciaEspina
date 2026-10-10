# Peluquería Lucía

Aplicación de gestión interna de la Peluquería Ecológica Lucía Espina: visitas y cobros del día, clientes con su historial, catálogo de servicios con precios, tickets de caja y exportación a Excel para la gestoría.

Corre en local, en el ordenador de la peluquería (IIS + SQL Server), y la usa una sola persona.

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
  PeluqueriaLucia.Web/             ASP.NET Core MVC: controladores, vistas Razor, CSS y JS propios (sin librerías externas).
                                   Program.cs solo arranca; Startup.cs registra las capas y Configuracion/ tiene el detalle.
tests/
  PeluqueriaLucia.Tests/           Dominio, formato web e integración contra SQL Server (incluida la migración con datos).
```

Decisiones importantes:

- **Base de datos con datos reales.** Las tablas conservan sus nombres de la versión 1 (`ServiciosClientes`, `ServiciosGenericos`…) y las entidades se mapean a ellas. Las migraciones solo añaden. Ver [docs/DESPLIEGUE.md](docs/DESPLIEGUE.md).
- **Sin dependencias de internet.** La versión 1 cargaba Bootstrap, jQuery, Select2, Flatpickr y Font Awesome desde CDN: si se caía la conexión de la peluquería, la aplicación se rompía. Ahora todo (estilos, iconos y scripts) va dentro de la aplicación.
- **Carga rápida.** CSS y JS se sirven comprimidos (Brotli) y con caché de un año gracias a la huella en la URL; las páginas se precargan al pasar el ratón por un enlace; al arrancar se prepara la conexión con la base de datos, y la publicación va compilada con ReadyToRun.
- **Importes independientes del idioma de Windows.** Los formularios aceptan `12,50` y `12.50`. La aplicación fija la cultura `es-ES` para fechas e importes.

## Diseño

Sistema de diseño propio inspirado en las pautas públicas de interfaz de Apple: superficies agrupadas sobre fondo neutro, colores del sistema, tipografía del sistema, controles segmentados, interruptores y vidrio translúcido solo en la navegación. Modo día, noche o automático (como el sistema), elegible desde la barra lateral o la superior en el móvil. Se adapta a ordenador, tablet (barra lateral solo con iconos) y móvil (barra de pestañas inferior). Los tokens están en `wwwroot/css/tokens.css`. Avisos legales en [NOTICE.md](NOTICE.md).

## Desarrollo

Requisitos: SDK de .NET 10 y SQL Server local (la cadena de desarrollo está en `appsettings.Development.json`: `localhost`, base de datos `PeluqueriaLuciaDb`, autenticación de Windows).

```powershell
dotnet tool restore
dotnet ef database update --project src/PeluqueriaLucia.Infrastructure --startup-project src/PeluqueriaLucia.Web
dotnet run --project src/PeluqueriaLucia.Web
```

Pruebas (crean y borran bases de datos temporales en el SQL Server local; otro servidor con `PELUQUERIA_PRUEBAS_SQL`):

```powershell
dotnet test
```

### Cambios en la base de datos

1. Cambia la entidad y su configuración en `Infrastructure/Persistencia/Configuraciones.cs`.
2. Crea la migración:

   ```powershell
   dotnet ef migrations add NombreDelCambio --project src/PeluqueriaLucia.Infrastructure --startup-project src/PeluqueriaLucia.Web --output-dir Persistencia/Migraciones
   ```

3. **Revisa el código generado.** Nada de `DropTable`, `DropColumn` ni `RenameColumn` sobre datos existentes. Una columna nueva obligatoria lleva valor por defecto o un `UPDATE` que la rellene. Ese `UPDATE` va dentro de `EXEC(N'…')`, para que se compile cuando las columnas nuevas ya existen.
4. `dotnet test`: la prueba `El_modelo_coincide_con_las_migraciones` falla si se te olvida el paso 2, y las de `MigracionTests` comprueban la actualización desde la versión anterior.
