# Guía de diseño de Lucía

Una sola identidad para el salón, el acceso y la administración: natural, cálida y serena. La composición editorial de la web se combina con la claridad de una interfaz de gestión, sus listas agrupadas y sus controles cómodos. Las diferencias entre pantallas responden a su función; comparten siempre paleta, tipografía, formas y componentes.

## Identidad

Conservar el logo original y la fotografía real del salón. El verde bosque organiza las acciones y la marca; el oliva aporta el carácter ecológico; el crema y la arena dan calidez. Evitar colores eléctricos, brillos intensos y decoraciones que distraigan del contenido.

| Uso | Tema día | Tema noche | Variable |
| --- | --- | --- | --- |
| Fondo | Crema `#f8f6ef` | Bosque `#10231f` | `--bg` |
| Tarjetas | Marfil `#fffdf8` | Verde `#19302a` | `--surface` |
| Texto principal | Bosque `#183a35` | Marfil `#f4f1e7` | `--label` |
| Acción principal | Verde `#244c40` | Salvia `#c5d3ac` | `--accent` |
| Identidad secundaria | Oliva `#596644` | Salvia `#c2cfaa` | `--olive` |
| Separadores | Arena `#dcded0` | Verde `#3a5145` | `--separator` |

Los colores se definen exclusivamente en `wwwroot/css/tokens.css`, que cargan ambos layouts. Los tintes de información, cobros y pendientes son suaves. El rojo se reserva para errores y acciones destructivas; el estado también debe expresarse con texto o iconos.

## Tipografía y composición

- Una sola familia en toda la aplicación: **Source Serif 4** (Frank Grießhammer, Adobe; licencia SIL OFL), servida desde `wwwroot/fonts` y declarada en `css/fuentes.css`. Es variable en tamaño óptico: los titulares usan el corte fino y el texto de formularios y tablas el robusto, sin configurar nada. No usar Times New Roman ni fuentes externas.
- Títulos, nombre del salón y bienvenidas en peso normal, con espaciado contenido y cursiva oliva con moderación. Formularios, navegación, listas y datos con la misma familia; los importes usan números tabulares para alinearse.
- Etiquetas de sección pequeñas, con separación entre letras. El texto secundario mantiene un contraste legible.
- En pantallas anchas, las secciones ocupan el ancho disponible con márgenes interiores fluidos. Distribuir el contenido en columnas útiles, sin encerrar toda la página en una columna central.
- Los formularios de gestión mantienen un ancho de lectura cómodo y se alinean con el contenido. El login es la excepción deliberada: una única tarjeta centrada, de hasta 480 px.
- En móvil, apilar las columnas; conservar botones, navegación y etiquetas completas sin desplazamiento horizontal.
- Dar presencia al logo original: emblema mayor en la navegación, firma junto al pie de la fotografía y en el pie de página. La portada usa la fachada real (`publico/fachada-lucia.png`) completa, en un marco suave con ubicación y acceso al mapa; conservar el rótulo y la entrada visibles. Usar una marca de fondo tenue detrás del contenido, sin capturar clics ni aparecer en tickets impresos.

## Componentes comunes

`wwwroot/css/componentes.css` es la fuente de botones, campos, tarjetas, estados, listas y selector de apariencia. Usar `btn btn-primary` para la acción principal, `btn btn-gray` para la secundaria y `btn btn-light` sobre las franjas verdes de la web. No crear botones independientes por pantalla.

Las tarjetas tienen esquinas de 24 px, campos de 14 px y botones de forma ovalada. Las líneas son sutiles y las sombras suaves. Los controles principales tienen al menos 44 px de alto. El vidrio translúcido se limita a navegación y elementos flotantes; los datos y los formularios reposan en superficies sólidas.

Los errores se muestran junto al formulario, con mensajes en español. El foco de teclado permanece visible. Se respeta la preferencia de movimiento reducido. El selector Automática/Día/Noche y el script inicial son compartidos y mantienen la misma elección al pasar de la web al panel.

El calendario comparte los campos y colores del resto de la aplicación. Permite elegir mes y año directamente, además del día. El listado y la exportación de visitas ofrecen **Día, Mes, Año y Todo**; el periodo anual incluye del 1 de enero al 31 de diciembre. Los formularios de clientes, visitas y servicios se centran dentro del espacio disponible del panel.

En pantallas grandes, la foto de portada se limita a 560 px dentro de una composición centrada de hasta 1760 px. La agenda pública limita su calendario a 460 px y el selector emergente a 304 px; en móvil conservan su ancho adaptable. Cambiar el día, el mes o la semana y consultar disponibilidad conserva la posición de lectura. Cerrar o elegir una fecha devuelve el foco sin desplazar la página.

## Pantallas

- **Web:** fotografía del salón, títulos editoriales, tarjetas de servicios, contacto y reserva.
- **Login:** logo, bienvenida y formulario en una tarjeta central. Campos alineados a la izquierda y acción principal a todo el ancho.
- **Panel:** bienvenida con la misma foto, cifras claras y accesos de gestión agrupados. Navegación lateral de tonos suaves; en móvil, barra superior e inferior.
- **Clientes, visitas y catálogo:** misma tipografía, botones, tarjetas y paleta; dar prioridad a búsqueda, datos y acciones.
- **Seguridad:** formularios y listas con los mismos componentes; las sesiones y los errores deben poder leerse en móvil.
- **Ticket impreso:** papel blanco y tinta negra para que la impresora produzca un recibo legible. Su formato responde al soporte de impresión.

Antes de dar por terminada una pantalla, revisar escritorio y móvil, ambos temas, los estados vacíos y los errores de formulario. No introducir recursos externos de fuentes o scripts para aplicar el diseño.

## Movimiento

La web pública se mueve con [GSAP](https://gsap.com) (ScrollTrigger y SplitText) y [Lenis](https://lenis.darkroom.engineering) para el scroll suave. Están copiadas en `wwwroot/lib` con versión fija: no se cargan de un CDN y respetan la CSP `script-src 'self'`. El panel no carga ninguna.

- Las vistas marcan qué se anima con atributos (`data-anima="subir"`, `data-anima-grupo`, `data-dividir`, `data-parallax`, `data-marquesina`, `data-magnetico`…). `wwwroot/js/movimiento.js` documenta cada uno; no hace falta escribir JavaScript por página.
- Fondo vivo: hojas y manchas de color tenues que derivan con el scroll, y un grano de papel muy suave. Son decorativos (`aria-hidden`, sin clics) y desaparecen al imprimir.
- Con «reducir movimiento» del sistema, o si una librería no carga, no se anima nada y todo el contenido se ve desde el principio. Un script del `<head>` oculta lo animable solo mientras se prepara la página, con un máximo de 3 segundos.
- Movimientos lentos y suaves (`power3`, `expo`), como el resto de la marca. Nada de rebotes salvo en la página de error.
- Al añadir una página pública: usa los mismos atributos y revisa que se lea bien con el movimiento reducido.

## Favicon e iconos

La cabeza del logo, en crema sobre el azul noche del logo (`#081c26`), con esquinas suaves. El texto «LUCÍA» no se incluye porque no se lee a 16 px. Se generan a partir de `wwwroot/Img/Icono_bn.png`:

| Fichero | Uso |
|---|---|
| `favicon.ico` (16, 32 y 48 px) | Pestañas, Bing y Google (exige 48 px o múltiplo) |
| `iconos/favicon-96.png`, `iconos/icono-192.png` | Pestañas de alta densidad y resultados de búsqueda |
| `iconos/apple-touch-icon.png` (180 px, opaco) | Acceso directo en iPhone y iPad |
| `iconos/icono-512.png`, `iconos/icono-mascara-512.png` | `site.webmanifest`, Android y `og:image` |

Los enlaza el partial `Views/Shared/_Iconos.cshtml` en la web pública y en el panel. La portada declara además el logo y la foto de la fachada en los datos estructurados (`HairSalon`) para Google.

## Indicador de carga

`wwwroot/js/cargando.js` se carga antes que el resto de scripts en la web pública y en el panel. Con cada navegación (enlaces de la propia web y envíos de formulario) y con cada `fetch` al servidor enseña una barra fina arriba si la espera pasa de 150 ms y, si sigue, una tarjeta verde bosque con la hoja del salón y «Cargando…» (o «Loading…»). Se retira cuando llega la página nueva o termina la respuesta; al volver atrás desde la caché del navegador también. El botón que envía un formulario queda marcado con `.is-cargando` (pequeño giro) hasta entonces.

- Las llamadas periódicas (`sesion.js`, `agenda-avisos.js`) pasan `{ silencioso: true }` a `fetch` y no lo activan.
- Un enlace que descarga sin cambiar de página lleva `data-sin-cargando` (la exportación a Excel); por seguridad el indicador se retira solo a los 20 s.
- En el panel, mientras `sesion.js` comprueba la sesión al abrir cada pantalla, se ve este indicador en lugar del texto «Comprobando tu sesión…», que queda para cuando la comprobación falla.
- Procesos propios: `window.Cargando.iniciar()` y `window.Cargando.terminar()`.
