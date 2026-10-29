# Productos y stock de la tienda

La tienda del salón (champús, mascarillas, aceites…) se gestiona en **Productos** (`/admin/Productos`): fichas con referencia, código de barras, precio y fotos, y un stock que se descuenta solo al cobrar las visitas en las que se venden o se utilizan.

**Stock en negativo.** Vender o usar más unidades de las que hay nunca se impide: el stock baja a −1, −2… El producto se ve en rojo («−1 ud», «Faltan 1 por reponer»), cuenta como agotado y aparece en *Por reponer* hasta que una entrada o un recuento lo devuelven a lo que hay.

## Cómo se usa

- **Nuevo producto**: referencia (única, en mayúsculas: `CH-ARG-250`), código de barras opcional (EAN/UPC de 8 a 14 cifras), nombre, **categoría (obligatoria)**, marca, descripción, precio de venta (con impuestos, como en el ticket), precio de coste opcional, aviso de stock mínimo, unidades que hay ahora y hasta seis fotos. La categoría se elige con un buscador; si no existe, se escribe y se elige *Crear la categoría «…»*, y se crea al guardar (si ya hay una con ese nombre, aunque cambien las mayúsculas, se usa esa). Las marcas ya escritas se sugieren al teclear.
- **Inventario**: un panel de búsqueda con el texto (nombre, referencia, marca, categoría o código de barras, al escribir), un **buscador de categorías** (cada una con sus productos y unidades; las eliminadas también, para ver sus archivados) y los filtros *En venta*, *Por reponer* (agotados o con el mínimo o menos), *Archivados* y *Todos*. Encima, cuatro cifras: productos en venta, unidades en el almacén, por reponer y valor del stock (a precio de venta y de coste).
- **Categorías** (botón en la cabecera de Productos, `/admin/Productos/Categorias`): crear, renombrar y eliminar. **Eliminar es un borrado lógico**: antes se avisa de cuántos productos y unidades afecta; la categoría pasa a *Eliminadas* y sus productos a la venta se **archivan con su stock** (dejan de ofrecerse en visitas y de contar en el inventario; las unidades se conservan). **Recuperar** la categoría los devuelve a la venta tal cual. Los productos que ya estaban archivados a mano siguen archivados. Mientras la categoría esté eliminada, sus productos no se pueden volver a vender sueltos: hay que recuperarla o cambiarles la categoría en *Editar*.
- **Ficha**: galería de fotos, stock con su estado, precio, coste, margen y código de barras, y el **historial de stock**: cada alta, entrada, venta (con enlace a la visita), uso en el salón y recuento, con quién, cuándo, la nota y el stock con que quedó.
- **Coste de compra con historial** (apartado *Coste de compra* de la ficha): lo que ha ido costando el producto («enero de 2026, 100 €; febrero, 120 €»), lo más reciente primero, con la diferencia respecto al anterior (en rojo si sube, en verde si baja) y la nota (proveedor, pedido…). Se anota solo:
  - al dar de alta el producto con su coste y cada vez que se cambia el coste en *Editar* (desde ese día);
  - al registrar una **entrada** de mercancía, si se escribe el *coste de cada unidad* (opcional);
  - a mano, en *Anotar un coste*: importe, desde qué fecha (no futura) y nota.
  El coste del producto (el de margen y valor del inventario) es siempre el del registro más reciente; anotar uno de un mes pasado solo completa el historial.
- **Movimientos a mano** desde la ficha:
  - *Entrada*: llega mercancía; suma unidades.
  - *Uso en el salón*: se gasta como muestra o se abre para el salón; resta unidades (puede quedar en negativo).
  - *Recuento*: se escribe lo contado y el stock pasa a ser eso; el movimiento guarda la diferencia.
- **Archivar** retira el producto de las visitas nuevas sin perder historial ni tickets. **Eliminar** borra del todo (fotos e historial incluidos) solo si nunca ha aparecido en una visita; si ha aparecido, se archiva.
- **Panel**: la tarjeta *Productos por reponer* muestra los primeros agotados o bajo mínimo.

## Venderlos en una visita

- **Nueva visita** tiene un bloque *Productos*: se busca el producto (también con el **lector de códigos de barras**: el código lo elige, el foco pasa a *Unidades* y Enter lo añade), se indican las unidades y si **se vende** o se ha **usado en el servicio**. Una visita puede ser solo de productos.
- **Venta** (el producto entero que se lleva el cliente): se cobra a su precio, que se puede cambiar, y sale del stock al cobrar.
- **Usado en el servicio** (tinte, oxidante, mascarilla…): **va incluido en el servicio**, así que sale en la visita y en el ticket **a 0,00 €**. Al añadirlo se pregunta **«¿Se ha terminado?»**:
  - *Sí, sale del stock*: al cobrar se descuenta como *Uso en el salón* enlazado a la visita.
  - *No, sigue en uso*: el envase sigue abierto en el salón y el stock no cambia.
- En el **detalle** de una visita pendiente se añaden o quitan servicios y productos (venta o usados) y se cambian los precios; ver «La visita hasta cobrarla» en [AGENDA.md](AGENDA.md). Si no hay bastante stock, se avisa de que quedará en negativo; se puede cobrar igualmente.
- **Al cobrar** se descuenta el stock: un movimiento *Venta* por producto vendido y uno de uso por producto usado y terminado, enlazados a la visita. Mientras la visita está pendiente el stock no cambia.
- El **ticket** (HTML y térmico) lista los productos con sus unidades; lo usado en el servicio sale a 0,00 marcado como incluido. El **Excel** de visitas añade la columna *Productos* (lo usado, con «(incluido)») y una hoja *Productos* con una fila por producto y su uso.
- Los totales de **Visitas** y del **Panel** (facturado, cobrado, pendiente) incluyen los productos, y cuentan las unidades vendidas (lo usado en el servicio no se cuenta como vendido).
- Las visitas cobradas antes de este cambio, cuando los productos usados se indicaban al cobrar («¿Qué productos has utilizado?»), los siguen mostrando en *Productos utilizados*, sacados de los movimientos de stock.

## Reglas que garantiza el código

- El stock **nunca se escribe a mano**: solo cambia con un movimiento, así que el historial explica siempre el stock actual (también cuando queda en negativo). El valor del inventario y las unidades del almacén solo cuentan el stock positivo.
- **Concurrencia**: `Productos.Version` es `rowversion`. Si dos cobros mueven a la vez el mismo producto, el segundo falla al guardar sin cobrar nada y la pantalla pide repetirlo con el stock actualizado: ninguna unidad se pierde ni se cuenta dos veces.
- Cada línea vendida guarda referencia, nombre y precio del momento: cambiar o archivar el producto no altera tickets, totales ni exportaciones anteriores.
- Un producto con ventas no se puede borrar (también lo impide la clave ajena `ProductosVisitas → Productos`).
- Todo producto tiene categoría (`Productos.CategoriaId`, obligatoria, clave ajena sin borrado en cascada). Las categorías nunca se borran de la base: eliminarlas las marca (`Activa = 0`, `EliminadaEn`) y archiva sus productos con `ArchivadoConCategoria = 1`, que es lo que permite devolver a la venta exactamente esos al recuperarla. No puede haber dos categorías **activas** con el mismo nombre (índice único filtrado; la intercalación no distingue mayúsculas), pero sí una eliminada y otra nueva que se llamen igual.

## Fotos

- Se aceptan JPG, PNG y WebP de hasta 10 MB (seis por producto). Se comprueba el contenido real (no la extensión), se enderezan según la cámara, se reducen a 1600 px y se guardan como JPEG nuevo **sin metadatos** (ni GPS ni modelo del móvil), más una miniatura de 480 px para listados y visitas. El procesado es el mismo que el de las fotos de opiniones (`NormalizadorFotos`).
- Se guardan en una **carpeta privada** como `{id}.jpg` y `{id}-mini.jpg`, y solo se sirven con sesión en el panel (`/admin/productos/foto/{id}`).
- La carpeta la declara cada entorno en `FotosProductos:Carpeta`:

  | Entorno | Dónde se define | Carpeta |
  |---|---|---|
  | Desarrollo (tu PC, Visual Studio) | `appsettings.Development.json` | `src/PeluqueriaLucia.Web/AlmacenProductos` (ignorada por Git) |
  | Producción en Azure | Web App → Variables de entorno: `FotosProductos__Carpeta` | `/home/data/productos`, que sobrevive a las publicaciones (`/home/site/wwwroot` se reemplaza en cada una) |
  | Producción en un IIS | `appsettings.Production.json` del servidor (ver `appsettings.Production.example.json`) | Una carpeta del servidor fuera del sitio publicado |

  Si falta, se usa `AlmacenProductos` en la raíz de la web y, fuera de desarrollo, el arranque deja un aviso en el log, porque esa carpeta puede borrarse al publicar.

## Base de datos

La migración `ControlDeProductos` solo **añade** cuatro tablas; no toca ninguna existente:

| Tabla | Qué guarda |
|---|---|
| `Productos` | Ficha y stock. Índices únicos en `Referencia` y en `CodigoBarras` (cuando lo hay). |
| `FotosProductos` | Fotos y su orden (la 0 es la principal). Se borran con el producto. |
| `MovimientosStock` | Historial: tipo, cantidad con signo, stock resultante (puede ser negativo), nota, visita (en ventas y productos utilizados) y administrador. |
| `ProductosVisitas` | Productos de cada visita (`ServiciosClientes`): unidades, precio unitario, importe y uso (columna `Uso`, ver abajo). |

La migración `CategoriasDeProductos` convierte la categoría de texto libre en una tabla, **sin perder datos**:

| Cambio | Detalle |
|---|---|
| Tabla `CategoriasProductos` | `Nombre`, `Activa`, `CreadaEn`, `EliminadaEn`. Índice único en `Nombre` solo para las activas. |
| `Productos.CategoriaId` | Obligatoria. Antes de exigirla, cada texto distinto de la antigua columna `Categoria` (sin espacios sobrantes; mayúsculas indiferentes) se convierte en una categoría y los productos sin categoría van a **«Sin categoría»**. Después se quita la columna de texto. |
| `Productos.ArchivadoConCategoria` | Por defecto 0. |

La vuelta atrás (`Down`) recupera el texto de cada producto. Se ha probado con datos en una base desechable: repetidas con distinta mayúscula, vacías y nulas.

La migración `VisitasDeCitasYProductosDelServicio` también **solo añade**:

| Cambio | Detalle |
|---|---|
| `ProductosVisitas.Uso` | `int`, por defecto 0: 0 venta, 1 usado en el servicio y terminado, 2 usado y sigue en uso. Las líneas que ya había quedan como venta. La restricción `CK_ProductosVisitas_Uso` exige que lo usado tenga precio e importe 0. |
| `ServiciosClientes.ReservaId` | La cita de la agenda que se cobra con la visita (ver [AGENDA.md](AGENDA.md)). Admite nulos; índice único filtrado y clave ajena a `ReservasCitas` con `ON DELETE SET NULL`. |

La migración `SorteosYCostesDeProductos` añade la tabla `CostesProductos` (producto, `Coste`, `Desde` (date), nota, cuándo y quién lo anotó; se borra con el producto, que solo se borra si nunca se ha vendido) y empieza el historial de cada producto que ya tenía coste con ese coste, desde el día de su alta (nota «Coste anterior al historial»). No cambia el coste de ningún producto. Los sorteos que añade la misma migración están en [SORTEOS.md](SORTEOS.md).

Estado: `ControlDeProductos` y `DualidadDeServicios` (ver [AGENDA.md](AGENDA.md)) aplicadas en la base **local** el 22-oct-2026, y `CategoriasDeProductos` también, con copia previa `PeluqueriaLuciaDb_antes_CategoriasDeProductos_20261022.bak` en la carpeta de copias de SQL Server. **En producción**, las cuatro (`ControlDeProductos`, `DualidadDeServicios`, `PreferenciaDeAviso` y `CategoriasDeProductos`) se aplicaron el 22-oct-2026, y `FotosProductos__Carpeta` ya está en la Web App.

## Puesta en producción

1. **Antes de publicar el código**, aplicar las migraciones `ControlDeProductos`, `DualidadDeServicios`, `PreferenciaDeAviso` y `CategoriasDeProductos`, en ese orden, contra Azure SQL como indica [AZURE.md](AZURE.md) («Migraciones y administrador»). La versión publicada ahora no usa ninguna de estas tablas ni columnas, así que sigue funcionando mientras tanto. Si el código llegara antes que las tablas, las pantallas de visitas, productos y agenda fallarían.
2. En la Web App (Configuración → Variables de entorno), añadir `FotosProductos__Carpeta` = `/home/data/productos`, junto a `Imagenes__Carpeta` y `FotosResenas__Carpeta`. Solo es para Azure: en desarrollo la carpeta sale de `appsettings.Development.json`.
3. Publicar, crear las categorías (o escribirlas al dar de alta cada producto) y dar de alta los productos con su stock inicial.

Para publicar el cobro desde la agenda, los productos usados en el servicio, los sorteos y el historial de costes hay que aplicar antes `VisitasDeCitasYProductosDelServicio` y `SorteosYCostesDeProductos`, en ese orden (la versión publicada no usa sus tablas ni columnas, así que sigue funcionando mientras tanto). `/salud` avisa si falta alguna.
