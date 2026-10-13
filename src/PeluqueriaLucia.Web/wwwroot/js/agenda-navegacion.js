'use strict';
// Conserva el punto de lectura al consultar otra fecha, sin cambiar la navegación GET.
(() => {
  const calendario = document.querySelector('.agenda-calendario, form.agenda-filtro');
  if (!calendario) return;
  const clave = 'lucia-agenda-posicion';

  try {
    const anterior = JSON.parse(sessionStorage.getItem(clave) || 'null');
    sessionStorage.removeItem(clave);
    if (anterior?.url === location.href && Date.now() - anterior.instante < 15000
        && Number.isFinite(anterior.x) && Number.isFinite(anterior.y)) {
      requestAnimationFrame(() => window.scrollTo({ left:anterior.x, top:anterior.y, behavior:'instant' }));
    }
  } catch {
    // La selección sigue funcionando si el navegador no permite almacenamiento.
  }

  const recordar = (destino) => {
    if (destino.origin !== location.origin || destino.pathname !== location.pathname) return;
    try {
      sessionStorage.setItem(clave, JSON.stringify({ url:destino.href, x:scrollX, y:scrollY, instante:Date.now() }));
    } catch {
      // Sin almacenamiento se mantiene el comportamiento normal de los enlaces.
    }
  };

  calendario.addEventListener('click', (evento) => {
    if (evento.defaultPrevented || evento.button !== 0 || evento.ctrlKey || evento.metaKey || evento.shiftKey || evento.altKey) return;
    const enlace = evento.target.closest('a[href]');
    if (enlace && calendario.contains(enlace)) recordar(new URL(enlace.href));
  });

  const filtro = calendario.matches('form[method="get"]') ? calendario : calendario.querySelector('form[method="get"]');
  filtro?.addEventListener('submit', (evento) => {
    if (evento.defaultPrevented) return;
    const formulario = evento.currentTarget;
    const destino = new URL(formulario.action);
    destino.search = new URLSearchParams(new FormData(formulario)).toString();
    recordar(destino);
  });
})();
