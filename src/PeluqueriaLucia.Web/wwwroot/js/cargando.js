// Indicador de carga del panel y de la web pública. Se carga antes que el resto de scripts.
// Aparece con cada navegación (enlaces y formularios) y con cada llamada fetch al servidor, y se retira cuando
// la página nueva llega o la respuesta termina: primero una barra fina arriba (si pasa de 150 ms) y, si la espera
// sigue, una tarjeta con la hoja del salón. Las llamadas con { silencioso: true } (comprobación de sesión, avisos
// periódicos) no lo activan. window.Cargando.iniciar() / terminar() sirven para procesos propios.
'use strict';

(() => {
  const RETARDO_BARRA = 150;
  const RETARDO_TARJETA = 900;
  const TOPE_NAVEGACION = 20000;
  let activas = 0;
  let plazoBarra = null;
  let plazoTarjeta = null;
  let plazoTope = null;
  let elemento = null;
  // Botones pulsados que esperan: su giro aparece con la barra, no antes, para que un cambio rápido no parpadee
  const botonesEsperando = new Set();

  const hoja =
    '<svg class="cargando-hoja" viewBox="0 0 200 320" aria-hidden="true" focusable="false">' +
    '<path d="M100 8C40 70 18 150 40 222c14 46 42 78 60 90 18-12 46-44 60-90C182 150 160 70 100 8Z" fill="currentColor"/>' +
    '<path d="M100 30v280M100 110l-34-30M100 150l38-34M100 196l-44-36M100 236l46-38" class="cargando-vena" fill="none" stroke-width="10" stroke-linecap="round"/>' +
    '</svg>';

  const texto = () => (document.documentElement.lang === 'en' ? 'Loading…' : 'Cargando…');

  function crear() {
    if (elemento) return elemento;
    elemento = document.createElement('div');
    elemento.id = 'cargando';
    elemento.setAttribute('role', 'status');
    elemento.setAttribute('aria-live', 'polite');
    elemento.hidden = true;
    elemento.innerHTML = `<div class="cargando-barra"></div><div class="cargando-tarjeta">${hoja}<span>${texto()}</span></div>`;
    document.body.append(elemento);
    return elemento;
  }

  function mostrar(nivel) {
    if (activas === 0) return;
    const e = crear();
    e.hidden = false;
    if (!(nivel === 'barra' && e.dataset.nivel === 'tarjeta')) e.dataset.nivel = nivel;
    botonesEsperando.forEach((boton) => boton.classList.add('is-cargando'));
  }

  function limpiar() {
    clearTimeout(plazoBarra);
    clearTimeout(plazoTarjeta);
    clearTimeout(plazoTope);
    plazoBarra = plazoTarjeta = plazoTope = null;
  }

  /** Empieza una espera. Cada iniciar() necesita su terminar(); varias esperas a la vez comparten el indicador. */
  function iniciar() {
    activas++;
    if (activas > 1) return;
    plazoBarra = setTimeout(() => mostrar('barra'), RETARDO_BARRA);
    plazoTarjeta = setTimeout(() => mostrar('tarjeta'), RETARDO_TARJETA);
  }

  /** Termina una espera; con todo=true, todas (al volver a una página desde el historial). */
  function terminar(todo = false) {
    activas = todo ? 0 : Math.max(0, activas - 1);
    if (activas > 0) return;
    limpiar();
    if (elemento) {
      elemento.hidden = true;
      delete elemento.dataset.nivel;
    }
    [...botonesEsperando, ...document.querySelectorAll('.btn.is-cargando')].forEach((boton) => {
      boton.classList.remove('is-cargando');
      boton.removeAttribute('aria-busy');
    });
    botonesEsperando.clear();
  }

  // ── Llamadas al servidor: cualquier fetch, salvo las silenciosas ──
  const fetchOriginal = window.fetch;
  if (typeof fetchOriginal === 'function') {
    window.fetch = function (recurso, opciones) {
      const promesa = fetchOriginal.call(this, recurso, opciones);
      if (opciones && opciones.silencioso) return promesa;
      iniciar();
      return promesa.finally(() => terminar());
    };
  }

  // ── Navegación: enlaces de esta web que cargan otra página ──
  function navegacionDe(enlace, evento) {
    if (!enlace || evento.defaultPrevented || evento.button !== 0) return false;
    if (evento.metaKey || evento.ctrlKey || evento.shiftKey || evento.altKey) return false;
    if (enlace.hasAttribute('download') || enlace.hasAttribute('data-sin-cargando')) return false;
    if (enlace.target && enlace.target !== '_self') return false;
    const href = enlace.getAttribute('href');
    if (!href || href.startsWith('#')) return false;
    let destino;
    try {
      destino = new URL(href, window.location.href);
    } catch {
      return false;
    }
    if (destino.origin !== window.location.origin) return false;
    // Misma página con otra ancla (#contacto, el menú móvil…): no se recarga nada
    return destino.pathname + destino.search !== window.location.pathname + window.location.search || !destino.hash;
  }

  function iniciarNavegacion() {
    iniciar();
    // Una descarga o un diálogo cancelado no cambian de página: el indicador no se queda para siempre
    clearTimeout(plazoTope);
    plazoTope = setTimeout(() => terminar(true), TOPE_NAVEGACION);
  }

  document.addEventListener('click', (evento) => {
    const origen = evento.target;
    const enlace = origen && typeof origen.closest === 'function' ? origen.closest('a[href]') : null;
    if (navegacionDe(enlace, evento)) iniciarNavegacion();
  });

  document.addEventListener('submit', (evento) => {
    const form = evento.target;
    if (evento.defaultPrevented || !form || (form.target && form.target !== '_self') || form.hasAttribute?.('data-sin-cargando')) return;
    const boton = evento.submitter;
    if (boton && boton.classList) {
      botonesEsperando.add(boton);
      boton.setAttribute('aria-busy', 'true');
    }
    iniciarNavegacion();
  });

  // Al volver atrás desde la caché del navegador la página reaparece tal cual estaba: sin indicador
  window.addEventListener('pageshow', () => terminar(true));

  window.Cargando = { iniciar, terminar, activas: () => activas };
})();
