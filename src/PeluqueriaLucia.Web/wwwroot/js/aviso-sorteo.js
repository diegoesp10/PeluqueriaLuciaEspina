// Aviso flotante de sorteo en la web pública: aparece al poco de cargar, con una animación; si se cierra, no vuelve a
// salir para ese anuncio (se recuerda en este navegador).
'use strict';

(() => {
  const aviso = document.querySelector('[data-aviso-sorteo]');
  if (!aviso) return;

  const clave = `aviso-sorteo-${aviso.dataset.avisoSorteo}`;
  let cerrado = false;
  try {
    cerrado = localStorage.getItem(clave) === '1';
  } catch {
    // Sin almacenamiento (modo privado estricto): el aviso sale en cada página
  }
  if (cerrado) {
    aviso.remove();
    return;
  }

  aviso.hidden = false;
  // Un momento después de cargar, para que no compita con la portada
  setTimeout(() => aviso.classList.add('is-visible'), 900);

  aviso.querySelector('[data-aviso-sorteo-cerrar]').addEventListener('click', () => {
    try {
      localStorage.setItem(clave, '1');
    } catch {
      // Se cierra igual, aunque no se pueda recordar
    }
    aviso.classList.remove('is-visible');
    setTimeout(() => aviso.remove(), 350);
  });
})();
