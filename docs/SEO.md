# Buscadores: Google Search Console y Bing Webmaster Tools

La web ya sirve lo que los buscadores necesitan; lo que queda es darla de alta en cada uno y enviarle el mapa.

## Lo que sirve la app

| Qué | Dónde | Para qué |
|---|---|---|
| `robots.txt` | `SeoController` | Deja fuera `/admin`, `/api/`, los enlaces de confirmación de cita y las fotos de reseñas; indica el `sitemap.xml` |
| `sitemap.xml` | `SeoController` | Las seis páginas públicas en español e inglés, cada una con sus alternativas `hreflang` |
| Canónica y `og:url` | `_LayoutPublico` | Una sola dirección por página e idioma, sin parámetros |
| `hreflang` es / en / x-default | `_LayoutPublico` | Que Google entienda que `/en/…` es la misma página en inglés |
| `meta description` y Open Graph | `_LayoutPublico` | Texto y imagen al compartir o en los resultados |
| Datos estructurados `HairSalon` (JSON-LD) | `Views/Inicio/Index.cshtml` | Nombre, teléfono, dirección y logo para la ficha de negocio |
| `noindex` en el panel | `Areas/Admin/.../_Layout.cshtml` | Que la pantalla de acceso no aparezca en los resultados |

Las direcciones absolutas salen de `PELUQUERIA_URL_PUBLICA` (en Azure, `https://luciapeluqueriaecologica.com/`).

## 1. Google Search Console

1. Entra en https://search.google.com/search-console con la cuenta de Google que vaya a administrar la web.
2. **Añadir propiedad → Dominio**: `luciapeluqueriaecologica.com`. El tipo *Dominio* cubre a la vez `http`, `https`, `www` y `/en`.
3. Google muestra un registro **TXT** (`google-site-verification=…`). En Cloudflare → **DNS → Registros → Agregar registro**: tipo TXT, nombre `@`, contenido el valor que da Google, TTL automático. Guarda y vuelve a Search Console → **Verificar** (si falla, espera un minuto y repite).
4. En Search Console → **Sitemaps**: añade `https://luciapeluqueriaecologica.com/sitemap.xml` y envía.
5. En **Inspección de URL**, pega `https://luciapeluqueriaecologica.com/` y pulsa **Solicitar indexación**; repite con `/servicios` y `/en/`. El resto llega por el mapa.

Las primeras páginas suelen aparecer en unos días; el informe **Páginas** dice cuáles están indexadas y por qué no las demás.

## 2. Bing Webmaster Tools

1. Entra en https://www.bing.com/webmasters con una cuenta Microsoft (vale la de Azure).
2. Elige **Importar desde Google Search Console**: autoriza con la cuenta de Google y Bing copia la propiedad y el mapa ya verificados. No hay que tocar el DNS otra vez.
3. Si prefieres no enlazar las cuentas: **Añadir sitio manualmente** → `https://luciapeluqueriaecologica.com` → verificación por **CNAME** en Cloudflare (nombre y valor que indique Bing, nube gris) → después, en **Sitemaps**, envía `https://luciapeluqueriaecologica.com/sitemap.xml`.
4. En **Inspección de URL**, solicita la indexación de la portada.

Bing también alimenta a DuckDuckGo y a Yahoo, así que con esto quedan cubiertos.

## 3. Ficha del negocio en Google

Aparte del buscador, conviene reclamar o crear la ficha en https://www.google.com/business/ (Perfil de Empresa) con la misma dirección, teléfono y horario que la web, y poner `https://luciapeluqueriaecologica.com/` como sitio web y `https://luciapeluqueriaecologica.com/citas` como enlace de citas. Es lo que sale en Google Maps y en el lateral de los resultados.

## Comprobaciones rápidas

- `https://luciapeluqueriaecologica.com/robots.txt` y `/sitemap.xml` responden 200 (Cloudflare antepone a `robots.txt` su política de *content signals*, que no cambia las reglas).
- https://search.google.com/test/rich-results con la portada: debe reconocer `HairSalon`.
- Si cambian las rutas públicas, añade la página a `Paginas` en `SeoController` y a esta tabla.
