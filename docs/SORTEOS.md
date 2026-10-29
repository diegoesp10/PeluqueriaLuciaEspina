# Sorteos

El salón sortea servicios del catálogo o productos de la tienda entre los clientes que elige, avisa a quien gana y lo anuncia en la web. Todo se gestiona en **Sorteos** (`/admin/Sorteos`), en el menú del panel.

## Cómo se usa

1. **Nuevo sorteo**: el premio (un servicio del catálogo o un producto de la tienda, con un buscador), el título y, si se quiere, una descripción, **en español y en inglés** (se ven en la web), y cuándo se sortea. Se guarda el nombre del premio del momento: si luego cambia o se borra el servicio o el producto, el sorteo lo conserva.
2. **Participantes**: se añaden con el buscador de clientes (nombre, teléfono o correo) o, de golpe, **todos los clientes que vinieron entre dos fechas**. Los repetidos no se duplican. Mientras el sorteo está abierto se pueden quitar.
3. **Sortear**: el botón *Sortear ahora* elige al ganador **al azar entre todos los participantes** (hacen falta al menos dos), con un generador criptográfico: cada uno tiene la misma probabilidad y nadie puede predecir el resultado. La pantalla lo descubre con una ruleta de nombres y confeti (sin animación si el sistema pide reducir movimiento). También se puede **elegir el ganador a mano** («Prefiero elegir yo al ganador»), por ejemplo si el sorteo se hizo en el salón con papeletas.
4. **Avisar al ganador**: la pantalla propone un mensaje (con el premio, el sorteo y el teléfono del salón), que se puede retocar, y dos botones:
   - *Enviar por correo*: sale con la imagen de los correos del salón y el premio destacado. Hace falta que su ficha tenga correo y que el SMTP del servidor esté configurado.
   - *Abrir WhatsApp*: abre la conversación con su móvil y el mensaje escrito; se envía desde WhatsApp. Hace falta un móvil en su ficha.
   En los dos casos queda anotado por dónde y cuándo se le avisó.
5. **Si el ganador no responde o no puede venir**: *Repetir el sorteo* vuelve a sortear entre los que aún no han ganado, o *Elegir otro ganador* lo designa el salón. Las dos cosas piden un **motivo**; la extracción anterior se conserva.
6. **Cancelar**: un sorteo abierto se puede cancelar (con un motivo opcional); queda en el historial y la web deja de anunciarlo. Un sorteo sin ganador se puede eliminar; con ganador, no, porque forma parte del historial.

En **Sorteos** se ven los sorteos *En marcha*, con un aviso destacado cuando llega el día de sortear (o ha pasado y aún no se ha hecho), y el **Historial**: cuándo se sorteó, quién ganó, cómo (al azar entre cuántos o elegido por el salón), si se le avisó y por dónde, y cuántas extracciones hubo. Cada sorteo guarda todas sus extracciones con su motivo.

## En la web

Cada sorteo puede tener **anuncios**: un texto en español y en inglés que la web enseña **entre las fechas que se elijan** (incluidas). Mientras haya alguno vigente:

- Sale un **aviso flotante** abajo a la izquierda, en todas las páginas de la web pública, con el título, el premio y la fecha del sorteo (o «¡Ya tenemos ganador!» si ya se hizo). Quien lo cierra no lo vuelve a ver para ese anuncio (se recuerda en su navegador).
- El menú enseña **Sorteos** (`/sorteos` y `/en/sorteos`), una página con cada sorteo anunciado: premio, fecha, texto y descripción.

La web **nunca enseña** a los participantes ni el nombre del ganador. Los anuncios se leen de la base de datos y se guardan 5 minutos en memoria; cambiar un sorteo en el panel los actualiza al momento. Si la base de datos fallara, la web sale sin el aviso.

## Base de datos

La migración `SorteosYCostesDeProductos` solo **añade** tablas (y el historial de costes de [PRODUCTOS.md](PRODUCTOS.md)):

| Tabla | Qué guarda |
|---|---|
| `Sorteos` | Título y descripción (español e inglés), premio (`TipoPremio` 0 servicio / 1 producto, `ServicioId` o `ProductoId` y su nombre), fecha, estado (0 abierto, 1 realizado, 2 cancelado), motivo de cancelación y `rowversion`: dos «Sortear» a la vez no dan dos ganadores. Si el servicio o el producto se borran, la clave queda a nulo y el nombre se conserva. |
| `ParticipantesSorteos` | Un cliente por sorteo (índice único). Borrar la ficha del cliente lo saca de los sorteos. |
| `ExtraccionesSorteos` | Cada extracción: ganador (ficha y nombre del momento), forma (0 al azar, 1 elegido por el salón), fecha, entre cuántos, motivo, administrador y aviso (canal y fecha). Si se borra la ficha del ganador, queda su nombre. |
| `AnunciosSorteos` | Texto en los dos idiomas y fechas `Desde`/`Hasta` (date). |

Estado: aplicada en la base **local** (`PeluqueriaLuciaDb`, copia previa `PeluqueriaLuciaDb_antes_SorteosYCostes_20261029.bak`) y en `PeluqueriaLucia_VistaPrevia`, probada también hacia atrás. **En producción, pendiente**: aplicar `VisitasDeCitasYProductosDelServicio` y `SorteosYCostesDeProductos` (en ese orden) antes de publicar, como indica [AZURE.md](AZURE.md); si faltan, `/salud` lo dice y el despliegue falla.
