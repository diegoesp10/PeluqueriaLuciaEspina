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

- Georgia para el nombre del salón, los títulos de página y los mensajes de bienvenida. Usar peso normal, espaciado contenido y cursiva oliva con moderación.
- La fuente del sistema para formularios, navegación, listas y datos. Los importes usan números tabulares y tipografía sin serif para facilitar su lectura.
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
