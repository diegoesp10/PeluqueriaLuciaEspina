# Sorteos

El salón sortea un lote de premios entre los clientes que elige, avisa a quien gana, anota cuándo se le entregó y lo anuncia en la web. Los premios pueden ser servicios del catálogo, productos de la tienda y **productos de sorteo**: cosas que no son de la peluquería (una cena, unas entradas, una cesta de un comercio vecino…). Todo se gestiona en **Sorteos** (`/admin/Sorteos`), en el menú del panel, con tres pestañas: *En marcha*, *Historial* y *Productos de sorteo*.

## Productos de sorteo

En la pestaña **Productos de sorteo** (`/admin/ProductosSorteo`) se crean con su **nombre y una descripción, en español y en inglés** (el nombre en inglés es obligatorio; la descripción, si se escribe, también en los dos idiomas). No se repiten nombres entre los disponibles.

- **Editar** cambia también el premio en los sorteos **en marcha** que lo llevan; los ya sorteados conservan el nombre y la descripción con los que se sortearon.
- **Eliminar** no borra nada: deja de ofrecerse como premio y pasa a *Eliminados*, desde donde se puede **recuperar**. Si sigue en algún sorteo en marcha, el panel lo avisa para quitarlo de allí si ya no se sortea.
- Cada uno tiene su **ficha** con el uso: en cuántos sorteos en marcha está, cuántas veces se ha sorteado y cuántas se ha entregado, y la lista de sorteos en los que estuvo con la fecha, quién lo ganó y si se le entregó.

También se pueden crear desde el propio sorteo («Nuevo producto de sorteo» junto a los premios): al guardarlo se añade como premio y se vuelve al sorteo.

## Cómo se usa

1. **Nuevo sorteo**: el título y, si se quiere, una descripción, **en español y en inglés** (se ven en la web), y cuándo se sortea. Al crearlo lleva a añadir los premios.
2. **Premios**: mientras el sorteo está abierto se añaden con un buscador que agrupa *Productos de sorteo*, *Servicios del catálogo* y *Productos de la tienda*, con una **cantidad** (1 a 99; añadir otra vez el mismo premio suma la cantidad). Hasta 20 premios por sorteo, y se pueden quitar. **Quien gana se lleva el lote entero.** Se guarda el nombre (y la descripción) del momento: si luego se borra el servicio, el producto o el producto de sorteo, el sorteo lo conserva.
3. **Participantes**: se añaden con el buscador de clientes (nombre, teléfono o correo) o, de golpe, **todos los clientes que vinieron entre dos fechas**. Los repetidos no se duplican. Mientras el sorteo está abierto se pueden quitar.
4. **Sortear**: el botón *Sortear ahora* elige al ganador **al azar entre todos los participantes** (hacen falta al menos dos y al menos un premio), con un generador criptográfico: cada uno tiene la misma probabilidad y nadie puede predecir el resultado. La pantalla lo descubre con una ruleta de nombres y confeti (sin animación si el sistema pide reducir movimiento). También se puede **elegir el ganador a mano** («Prefiero elegir yo al ganador»), por ejemplo si el sorteo se hizo en el salón con papeletas.
5. **Avisar al ganador**: la pantalla propone un mensaje (con los premios, el sorteo y el teléfono del salón), que se puede retocar, y dos botones:
   - *Enviar por correo*: sale con la imagen de los correos del salón y los premios destacados. Hace falta que su ficha tenga correo y que el SMTP del servidor esté configurado.
   - *Abrir WhatsApp*: abre la conversación con su móvil y el mensaje escrito; se envía desde WhatsApp. Hace falta un móvil en su ficha.
   En los dos casos queda anotado por dónde y cuándo se le avisó.
6. **Entregar**: cuando el ganador recoge el premio, *Marcar premio entregado* anota la fecha. A partir de ahí el ganador ya no se puede cambiar.
7. **Si el ganador no responde o no puede venir** (y aún no se le ha entregado): *Repetir el sorteo* vuelve a sortear entre los que aún no han ganado, o *Elegir otro ganador* lo designa el salón. Las dos cosas piden un **motivo**; la extracción anterior se conserva.
8. **Cancelar**: un sorteo abierto se puede cancelar (con un motivo opcional); queda en el historial y la web deja de anunciarlo. Un sorteo sin ganador se puede eliminar; con ganador, no, porque forma parte del historial.

En **En marcha** se ven los sorteos abiertos con sus premios, con un aviso destacado cuando llega el día de sortear (o ha pasado y aún no se ha hecho). El **Historial** guarda cada sorteo realizado: qué premios se sortearon, cuándo, quién ganó, cómo (al azar entre cuántos o elegido por el salón), si se le avisó y por dónde, si se le entregó y cuándo, y cuántas extracciones hubo. Cada sorteo guarda todas sus extracciones con su motivo.

## En la web

Cada sorteo puede tener **anuncios**: un texto en español y en inglés que la web enseña **entre las fechas que se elijan** (incluidas). Mientras haya alguno vigente:

- Sale un **aviso flotante** abajo a la izquierda, en todas las páginas de la web pública, con el título, los premios y la fecha del sorteo (o «¡Ya tenemos ganador!» si ya se hizo). Quien lo cierra no lo vuelve a ver para ese anuncio (se recuerda en su navegador).
- El menú enseña **Sorteos** (`/sorteos` y `/en/sorteos`), una página con cada sorteo anunciado: la lista de premios (con la descripción de los productos de sorteo), la fecha, el texto y la descripción.

La web **nunca enseña** a los participantes ni el nombre del ganador. Los anuncios se leen de la base de datos y se guardan 5 minutos en memoria; cambiar un sorteo o un producto de sorteo en el panel los actualiza al momento. Si la base de datos fallara, la web sale sin el aviso.

## Base de datos

| Tabla | Qué guarda |
|---|---|
| `Sorteos` | Título y descripción (español e inglés), fecha, estado (0 abierto, 1 realizado, 2 cancelado), motivo de cancelación y `rowversion`: dos «Sortear» a la vez no dan dos ganadores, ni dos cambios de premios a la vez se pisan. Las columnas `TipoPremio`, `ServicioId`, `ProductoId`, `Premio` y `PremioEn` son de cuando cada sorteo tenía un solo premio: se conservan con sus datos y los sorteos nuevos las dejan a nulo. |
| `PremiosSorteos` | Los premios de cada sorteo: tipo (0 servicio, 1 producto de la tienda, 2 producto de sorteo), la clave del que corresponda (`ServicioId`, `ProductoId` o `ProductoSorteoId`), nombre y descripción del momento en los dos idiomas y cantidad (1 a 99). Si se borra el servicio, el producto o el producto de sorteo, la clave queda a nulo y el nombre se conserva. |
| `ProductosSorteo` | Nombre y descripción en los dos idiomas, `Activo` y fechas de alta, cambio y eliminación. |
| `ParticipantesSorteos` | Un cliente por sorteo (índice único). Borrar la ficha del cliente lo saca de los sorteos. |
| `ExtraccionesSorteos` | Cada extracción: ganador (ficha y nombre del momento), forma (0 al azar, 1 elegido por el salón), fecha, entre cuántos, motivo, administrador, aviso (canal y fecha) y `EntregadoEn`, cuándo se entregó el premio. Si se borra la ficha del ganador, queda su nombre. |
| `AnunciosSorteos` | Texto en los dos idiomas y fechas `Desde`/`Hasta` (date). |

Migraciones:

- `SorteosYCostesDeProductos` creó los sorteos (y el historial de costes de [PRODUCTOS.md](PRODUCTOS.md)). Aplicada en producción el 29-oct-2026.
- `ProductosDeSorteoYVariosPremios` solo **añade**: las tablas `ProductosSorteo` y `PremiosSorteos`, la columna `ExtraccionesSorteos.EntregadoEn`, y deja admitir nulos a `Sorteos.TipoPremio` y `Sorteos.Premio`. Copia el premio de cada sorteo que ya hubiera como su primer premio en `PremiosSorteos`, sin tocar las columnas antiguas. La vuelta atrás devuelve a esas columnas el primer premio de los sorteos creados después (un producto de sorteo queda como producto sin enlace, con su nombre; un sorteo sin premios, «Sin premio»).

  Estado: aplicada en la base **local** (`PeluqueriaLuciaDb`, copia previa `PeluqueriaLuciaDb_antes_ProductosDeSorteo_20261105.bak`) y en `PeluqueriaLucia_VistaPrevia`, probada también hacia atrás con datos. **En producción, pendiente**: aplicarla antes de publicar, como indica [AZURE.md](AZURE.md); si falta, `/salud` lo dice y el despliegue falla.
