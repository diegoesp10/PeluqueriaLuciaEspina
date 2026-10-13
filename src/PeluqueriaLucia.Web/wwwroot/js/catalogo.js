// Edición de un servicio: vista previa de la foto elegida antes de guardar.
'use strict';

(() => {
  const editor = document.querySelector('[data-foto]');
  if (!editor) return;

  const input = editor.querySelector('[data-foto-input]');
  const preview = editor.querySelector('[data-foto-preview]');
  const vacia = editor.querySelector('[data-foto-vacia]');
  const quitar = editor.querySelector('[data-foto-quitar]');
  const original = preview.getAttribute('src');
  let url;

  const mostrar = (src) => {
    preview.hidden = !src;
    vacia.hidden = Boolean(src);
    if (src) preview.src = src;
  };

  // Si la foto guardada ya no está en disco, se muestra el hueco vacío en lugar de una imagen rota
  preview.addEventListener('error', () => {
    if (preview.getAttribute('src') === original) mostrar(null);
  });
  if (original && preview.complete && preview.naturalWidth === 0) mostrar(null);

  input.addEventListener('change', () => {
    if (url) URL.revokeObjectURL(url);
    const archivo = input.files?.[0];
    url = archivo ? URL.createObjectURL(archivo) : undefined;
    if (quitar) quitar.checked = false;
    mostrar(url ?? original);
  });

  quitar?.addEventListener('change', () => {
    if (quitar.checked) {
      input.value = '';
      mostrar(null);
    } else {
      mostrar(original);
    }
  });
})();
