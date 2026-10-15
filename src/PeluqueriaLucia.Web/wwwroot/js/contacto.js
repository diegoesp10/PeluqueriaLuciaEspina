// «Contacto de la web»: añadir y quitar filas de teléfono sin recargar. Sin JavaScript, «Añadir otro teléfono» pide
// al servidor el formulario con una fila más, y un teléfono se quita dejándolo vacío.
'use strict';

(() => {
  const lista = document.querySelector('[data-telefonos]');
  const anadir = document.querySelector('[data-anadir-telefono]');
  const plantilla = document.getElementById('plantilla-telefono');
  if (!lista || !anadir || !plantilla) return;

  const maximo = Number(lista.dataset.maximo) || 4;
  const filas = () => [...lista.querySelectorAll('[data-telefono]')];
  const actualizar = () => {
    const total = filas().length;
    anadir.hidden = total >= maximo;
    // Siempre queda al menos una fila: con una sola, no se puede quitar
    filas().forEach((fila) => {
      fila.querySelector('[data-quitar-telefono]').hidden = total <= 1;
    });
  };

  lista.addEventListener('click', (evento) => {
    const boton = evento.target.closest('[data-quitar-telefono]');
    if (!boton) return;
    boton.closest('[data-telefono]').remove();
    actualizar();
    filas()[0]?.querySelector('input')?.focus();
  });
  anadir.addEventListener('click', (evento) => {
    evento.preventDefault();
    if (filas().length >= maximo) return;
    lista.append(plantilla.content.cloneNode(true));
    filas().at(-1)?.querySelector('input')?.focus();
    actualizar();
  });
  actualizar();
})();
