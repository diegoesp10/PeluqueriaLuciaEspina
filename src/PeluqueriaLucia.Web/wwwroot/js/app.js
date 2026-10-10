// Comportamiento común a todas las pantallas. Sin dependencias: funciona sin conexión a internet.
'use strict';

(() => {
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
window.Peluqueria = {
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

  euros: new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' }),

  /** Importe para un campo de texto: «12,50». */
  importeCampo(importe) {
    return importe.toFixed(2).replace('.', ',');
  },
};
