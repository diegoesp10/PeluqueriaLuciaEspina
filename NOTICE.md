# Avisos legales y licencias

Esta aplicación es un proyecto independiente. **No está afiliada, patrocinada ni respaldada por Apple Inc.** Apple y SF Pro son marcas de Apple Inc.; aquí solo se nombran para explicar la inspiración del diseño y no aparecen en la interfaz.

## Diseño

Las [Human Interface Guidelines](https://developer.apple.com/design/human-interface-guidelines/) de Apple son públicas y describen patrones funcionales: listas agrupadas, controles segmentados, interruptores, jerarquía tipográfica, colores del sistema y medidas táctiles. La interfaz de esta aplicación usa esas ideas con una implementación propia:

| No se usa | Qué se usa en su lugar |
|---|---|
| Logotipos o iconos de Apple | El logotipo propio de la peluquería (`wwwroot/Img`) |
| Tipografía San Francisco empaquetada | Georgia para títulos y la fuente del sistema del equipo para los controles: SF si ya está instalada en un Mac, Segoe UI en Windows. No se incluye ningún fichero de fuentes |
| SF Symbols | Iconos de línea en SVG al estilo de Lucide (licencia ISC), en `Infraestructura/IconoTagHelper.cs` |
| Capturas, textos o pantallas concretas de Apple | Nada: composición, textos y estilos son propios |

## Componentes de terceros

| Componente | Licencia |
|---|---|
| Trazos de iconos al estilo de [Lucide](https://lucide.dev) | ISC |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) (exportación a Excel) | MIT |
| [Entity Framework Core](https://github.com/dotnet/efcore) y ASP.NET Core | MIT |

La foto pública del salón procede de la [ficha de Lucía en Booksy](https://booksy.com/es-es/173411_lucia-peluqueria-ecologica_peluqueria_79758_oviedo), aportada como referencia por el propietario del proyecto. Se guarda localmente en `wwwroot/publico/salon.jpg`. El logo ya formaba parte del proyecto. Booksy conserva los derechos sobre su plataforma; la aplicación enlaza allí para las reservas.

### Licencia ISC de Lucide

```
ISC License

Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as part of Feather (MIT).
All other copyright (c) for Lucide are held by Lucide Contributors 2022.

Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby
granted, provided that the above copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF
THIS SOFTWARE.
```
