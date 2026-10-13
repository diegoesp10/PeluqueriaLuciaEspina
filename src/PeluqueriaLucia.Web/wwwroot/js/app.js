// Comportamiento común a todas las pantallas. Sin dependencias: funciona sin conexión a internet.
'use strict';

(() => {
  document.querySelectorAll('[data-imprimir]').forEach(b => b.addEventListener('click', () => window.print()));
  // ── Apariencia: automática, día o noche. Se recuerda en este navegador; el <head> la aplica antes de pintar ──
  const selectores = document.querySelectorAll('[data-selector-tema]');
  const leerTema = () => {
    try {
      return localStorage.getItem('tema') ?? 'auto';
    } catch {
      return 'auto';
    }
  };
  const aplicarTema = (tema) => {
    if (tema === 'dia' || tema === 'noche') document.documentElement.dataset.theme = tema;
    else delete document.documentElement.dataset.theme;
    selectores.forEach((selector) =>
      selector.querySelectorAll('[data-tema]').forEach((boton) =>
        boton.setAttribute('aria-checked', String(boton.dataset.tema === tema)),
      ),
    );
  };
  selectores.forEach((selector) =>
    selector.querySelectorAll('[data-tema]').forEach((boton) =>
      boton.addEventListener('click', () => {
        try {
          if (boton.dataset.tema === 'auto') localStorage.removeItem('tema');
          else localStorage.setItem('tema', boton.dataset.tema);
        } catch {
          // Sin almacenamiento (modo privado…): el cambio vale hasta recargar
        }
        aplicarTema(boton.dataset.tema);
      }),
    ),
  );
  aplicarTema(leerTema());

  // ── Notificaciones: las de éxito se ocultan solas; las de error se quedan hasta cerrarlas ──
  const cerrarToast = (toast) => {
    toast.classList.add('is-leaving');
    toast.addEventListener('transitionend', () => toast.remove(), { once: true });
  };

  document.querySelectorAll('.toast').forEach((toast) => {
    toast.querySelector('[data-cerrar-toast]')?.addEventListener('click', () => cerrarToast(toast));
    if (!toast.hasAttribute('data-persistente')) setTimeout(() => cerrarToast(toast), 4500);
  });

  // ── Confirmación antes de acciones destructivas: <form data-confirmar="texto"> ──
  const dialogo = document.getElementById('dialogo-confirmar');
  document.querySelectorAll('form[data-confirmar]').forEach((form) => {
    form.addEventListener('submit', (evento) => {
      if (form.dataset.confirmado === 'true' || !dialogo?.showModal) return;
      evento.preventDefault();
      dialogo.querySelector('#dialogo-texto').textContent = form.dataset.confirmar;
      dialogo.querySelector('#dialogo-aceptar').textContent = form.dataset.confirmarAccion ?? 'Eliminar';
      dialogo.returnValue = '';
      dialogo.showModal();
      dialogo.addEventListener(
        'close',
        () => {
          if (dialogo.returnValue !== 'confirmar') return;
          form.dataset.confirmado = 'true';
          form.requestSubmit();
        },
        { once: true },
      );
    });
  });

  // Fotos cuyo fichero ya no existe: se quitan para que se vea el icono de respaldo en vez de una imagen rota
  document.querySelectorAll('img[data-ocultar-si-falla]').forEach((img) => {
    const quitar = () => img.remove();
    if (img.complete && img.naturalWidth === 0) quitar();
    else img.addEventListener('error', quitar, { once: true });
  });

  // ── Filtros que se aplican al cambiar (radios, fechas) ──
  document.querySelectorAll('[data-enviar-al-cambiar]').forEach((control) => {
    control.addEventListener('change', () => control.form?.requestSubmit());
  });

  // Selector de mes: convierte «2026-10» en el primer día del mes y envía
  document.querySelectorAll('[data-mes-a-fecha]').forEach((control) => {
    control.addEventListener('change', () => {
      if (!control.value) return;
      control.form.elements.fecha.value = `${control.value}-01`;
      control.form.requestSubmit();
    });
  });

  // Buscador: busca solo cuando se deja de escribir un momento, y conserva el cursor al volver
  document.querySelectorAll('[data-buscar-al-escribir]').forEach((input) => {
    let espera;
    input.addEventListener('input', () => {
      clearTimeout(espera);
      espera = setTimeout(() => {
        sessionStorage.setItem('busqueda-foco', '1');
        input.form.requestSubmit();
      }, 350);
    });
    if (sessionStorage.getItem('busqueda-foco')) {
      sessionStorage.removeItem('busqueda-foco');
      input.focus();
      input.setSelectionRange(input.value.length, input.value.length);
    }
  });

  // Evita el doble envío de formularios (doble clic en «Cobrar», «Guardar»…)
  document.querySelectorAll('form[method="post"]').forEach((form) => {
    form.addEventListener('submit', (evento) => {
      if (evento.defaultPrevented) return;
      if (form.dataset.enviando === 'true') {
        evento.preventDefault();
        return;
      }
      form.dataset.enviando = 'true';
      setTimeout(() => delete form.dataset.enviando, 8000);
    });
  });
})();

/** Utilidades compartidas por los scripts de cada pantalla. */
const textosPagina = (() => {
  try {
    return JSON.parse(document.getElementById('textos-js')?.textContent ?? '{}');
  } catch {
    return {};
  }
})();

window.Peluqueria = {
  /** Idioma de la página («es» o «en»); viaja como «language» en todas las llamadas al servidor. */
  idioma: document.documentElement.lang === 'en' ? 'en' : 'es',

  /** Cultura para Intl (fechas, importes). */
  get cultura() {
    return this.idioma === 'en' ? 'en-GB' : 'es-ES';
  },

  /** Texto traducido que la página deja en #textos-js; si falta, el texto de respaldo. */
  texto(clave, respaldo = '') {
    return textosPagina[clave] ?? respaldo;
  },

  /** Ruta dentro del idioma actual: «/error/500» → «/en/error/500» en la web inglesa. */
  ruta(ruta) {
    return (document.documentElement.dataset.base ?? '') + ruta;
  },

  /**
   * POST de formulario a la API pública. Añade «language» y Accept-Language. Los errores graves del servidor
   * (5xx, 403, 404) llevan a la página de error del sitio; los de validación (400) y el límite (429) se devuelven
   * para mostrarlos junto al formulario.
   */
  async enviar(url, valores, { signal } = {}) {
    let respuesta;
    try {
      respuesta = await fetch(url, {
        method: 'POST',
        credentials: 'same-origin',
        signal,
        headers: { Accept: 'application/json', 'Accept-Language': this.idioma },
        body: new URLSearchParams({ ...valores, language: this.idioma }),
      });
    } catch (error) {
      if (error.name === 'AbortError') throw error;
      throw new Error(this.texto('sinConexion', 'No hay conexión con el salón.'));
    }
    if (respuesta.status >= 500 || respuesta.status === 403 || respuesta.status === 404) {
      window.location.assign(this.ruta(`/error/${respuesta.status}`));
      throw Object.assign(new Error('redirigido'), { name: 'AbortError' });
    }
    return respuesta;
  },


  /** Lee un importe escrito con coma o punto («12,50» o «12.50»). */
  leerImporte(texto) {
    const limpio = String(texto ?? '').replace(/[€\s]/g, '');
    if (!limpio) return 0;
    const ultimo = Math.max(limpio.lastIndexOf(','), limpio.lastIndexOf('.'));
    const normalizado =
      ultimo < 0 ? limpio : limpio.slice(0, ultimo).replace(/[.,]/g, '') + '.' + limpio.slice(ultimo + 1);
    const numero = Number(normalizado);
    return Number.isFinite(numero) ? numero : NaN;
  },

  /** Importe en céntimos enteros, para comparar sin errores de coma flotante. */
  centimos(importe) {
    return Math.round(importe * 100);
  },

  euros: new Intl.NumberFormat(document.documentElement.lang === 'en' ? 'en-GB' : 'es-ES', { style: 'currency', currency: 'EUR' }),

  /** Importe para un campo de texto: «12,50». */
  importeCampo(importe) {
    return importe.toFixed(2).replace('.', ',');
  },
};
