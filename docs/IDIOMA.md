# Web pública en dos idiomas y contrato `language`

La web pública existe en español (raíz) y en inglés (prefijo `/en`). El panel de administración solo existe en español. Este documento explica cómo funciona el frontal y qué envía al backend, para que los endpoints acepten el idioma en sus contratos.

## Cómo se elige el idioma

| Petición | Idioma | Cultura |
|---|---|---|
| `/`, `/servicios`, `/citas`, `/opiniones`, `/privacidad`, `/terminos`… | Español | `es-ES` |
| `/en`, `/en/servicios`, `/en/citas`… (las mismas rutas con `/en` delante) | Inglés | `en-GB` |
| `/en/admin/…` | No existe: 404 | — |

`Publico/Idioma.cs` reconoce el prefijo al principio del canal de peticiones y lo convierte en `PathBase`. Por eso los controladores no cambian: `/en/citas` llega a la misma acción que `/citas`, y los enlaces generados con `asp-action`, `Url.Action` o `~/` mantienen el `/en` solos. `RequestLocalization` fija la cultura (`CultureInfo.CurrentCulture/CurrentUICulture`) según ese prefijo.

El selector ES · EN de la cabecera enlaza la misma página en el otro idioma. Cada página publica `<link rel="alternate" hreflang>` para los buscadores.

## Traducciones del frontal

- **Textos de las vistas:** `@T["texto en español"]`. La clave es el propio texto español; la traducción está en `src/PeluqueriaLucia.Web/Publico/textos.en.json` (recurso incrustado). Si falta una traducción se muestra el español. La prueba `Todas_las_claves_de_las_vistas_publicas_tienen_traduccion` falla si una vista usa una clave sin traducir.
- **Validaciones:** los `ErrorMessage` de `ReservaFormulario` y `OpinionFormulario` y los mensajes de enlace de modelo (valor no válido, obligatorio…) pasan por el mismo diccionario.
- **Páginas legales:** una vista por idioma (`Views/Legal/Privacidad.es.cshtml`, `Privacidad.en.cshtml`…). Ante discrepancias prevalece el español.
- **Scripts:** el layout deja los textos que necesitan en `<script type="application/json" id="textos-js">`. `Peluqueria.texto(clave)` los lee y `Peluqueria.idioma` da `es` o `en`.
- **Mensajes del servidor (provisional):** los controladores públicos traducen `Resultado.Error.Mensaje` buscando el texto exacto en el diccionario. Funciona hoy, pero se rompe si alguien cambia una coma del mensaje en Application. Ver «Pendiente en el backend».

## Contrato: qué envía el frontal

Todas las llamadas públicas llevan el idioma de tres formas redundantes, para que el backend use la que le convenga:

1. **Ruta:** las páginas y endpoints se piden bajo `/en/…` en inglés.
2. **Campo `language`** con valor `es` o `en`:
   - Formularios HTML: `IdiomaFormularioTagHelper` añade `<input type="hidden" name="language">` a todos los `<form>` de la web pública (GET y POST). El panel no lo lleva.
   - `fetch` desde JavaScript: `Peluqueria.enviar()` lo añade al cuerpo.
3. **Cabecera `Accept-Language: es|en`** en las llamadas `fetch`.

| Método y ruta | Datos que envía el frontal | Respuesta que espera |
|---|---|---|
| GET `[/en]/citas?dia&servicioId&language` | Consulta | HTML |
| POST `[/en]/citas` | `Formulario.*`, `__RequestVerificationToken`, `language` | 302 a `/citas/recibida` o 400 con el formulario y los errores |
| POST `[/en]/citas/contacto/solicitar` | `telefono`, `__RequestVerificationToken`, `language` | 200 `{ "id": guid }` siempre; 429 texto plano con `Retry-After` |
| POST `[/en]/citas/contacto/verificar` | `id`, `codigo`, `__RequestVerificationToken`, `language` | 200 `{ nombre, apellido, correo, sexo }` (`sexo`: 0 Hombre, 1 Mujer, 2 Otro); 400 `{ "mensaje": "…" }` |
| GET/POST `[/en]/citas/confirmar/{token}` | `__RequestVerificationToken`, `language` en el POST | HTML; 410 si caducó |
| GET/POST `[/en]/opiniones` | Multipart: `Codigo`, `Alias`, `Estrellas`, `Comentario`, `Foto`, `Consentimiento`, `language` | HTML; 400 con errores |
| GET `[/en]/api/tratamientos` | — | JSON de categorías con precios ya formateados en el idioma |
| GET `[/en]/citas/disponibilidad` | `dia`, `servicioId` | JSON (no lo usa el frontal actualmente) |

### Errores

- Cualquier ruta inexistente o fallo muestra la página de error del sitio (`/error/{código}`) en el idioma de la URL: 400, 403, 404, 405, 410, 429, 500 y 503 tienen textos propios.
- Si una llamada `fetch` recibe 403, 404 o un 5xx, `Peluqueria.enviar()` lleva a `[/en]/error/{código}`. Esa página responde con el mismo código de estado.
- Los 400 (validación) y 429 (límite) se muestran junto al formulario sin salir de la página.
- Un 429 en una navegación normal muestra la página de error 429; en una llamada `fetch` responde con texto plano en el idioma de la ruta.

## Pendiente en el backend (para Codex)

1. **Leer `language`** (formulario o consulta, si no la cultura de la ruta) en todos los endpoints públicos y devolver los mensajes en ese idioma. Mejor aún: devolver un **código de error estable** (`Error.Codigo`, p. ej. `agenda.hora-ocupada`) además del mensaje, para que el frontal no dependa del texto exacto.
2. **Guardar el idioma de la reserva** (`ReservasCitas.Idioma`, migración aditiva, `es` por defecto) y usarlo en los correos y mensajes de WhatsApp. El enlace de confirmación debe apuntar a `/en/citas/confirmar/{token}` cuando la reserva se hizo en inglés.
3. **Nombres y descripciones del catálogo en inglés** (`NombreEn`, `DescripcionEn` o una tabla de traducciones) y devolverlos en `/api/tratamientos` y en la carta de `/servicios` según `language`. Hoy se muestran en español en la web inglesa; las categorías conocidas se traducen en el frontal.
4. **`/api/tratamientos`**: devolver el precio como número más su tipo (`fijo`, `desde`, `consultar`) en lugar de una cadena formateada.
5. **`/citas/contacto/verificar`**: devolver `sexo` como texto (`"Mujer"`) en lugar de número; el frontal depende hoy del orden del enum.
6. **Foto de las opiniones:** `ResenasService` construye la URL `/opiniones/foto/{id}` en Application; mejor devolver solo el identificador y que la web construya la URL (ya la ajusta con `Url.Content` para respetar el directorio virtual de IIS).
