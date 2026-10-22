// Cobro: «¿Qué productos has utilizado?». El buscador (select-buscable.js, también con el lector de códigos) elige el
// producto y «Añadir» (o Enter en las unidades) crea su fila; elegir uno que ya está suma unidades. No se cobran: al
// cobrar salen del stock. Nada se impide si no hay bastante: se avisa de que el stock quedará en negativo.
'use strict';

(() => {
  const bloque = document.querySelector('[data-productos-usados]');
  const elegir = bloque?.querySelector('[data-usado-elegir]');
  if (!bloque || !elegir) return;

  const cantidadNueva = bloque.querySelector('[data-usado-cantidad]');
  const lista = bloque.querySelector('[data-usados-lista]');
  const plantilla = bloque.querySelector('[data-usado-plantilla]');
  const aviso = bloque.querySelector('[data-usado-aviso]');

  const filas = () => [...lista.querySelectorAll('[data-usado-linea]')];
  const unidades = (fila) => Math.max(0, Math.trunc(Number(fila.querySelector('[data-linea-cantidad]').value)) || 0);
  const unidadesTexto = (n) => (n === 1 ? '1 unidad' : `${n} unidades`);
  const enStock = (n) => (n > 0 ? `${n} en stock` : n === 0 ? 'agotado' : `en negativo (−${-n})`);

  /** «is-error» para errores; «is-aviso» para avisos que no impiden cobrar. */
  const avisar = (texto, tipo = 'is-error') => {
    aviso.hidden = !texto;
    aviso.textContent = texto ?? '';
    aviso.classList.remove('is-error', 'is-aviso');
    if (texto) aviso.classList.add(tipo);
  };

  // Aviso, nunca bloqueo: si se usan más de las que hay, el stock queda en negativo para reponerlo
  const revisar = () => {
    const negativos = filas()
      .map((fila) => ({ nombre: fila.querySelector('[data-linea-nombre]').textContent, queda: Number(fila.dataset.stock) - unidades(fila) }))
      .filter((f) => f.queda < 0);
    avisar(negativos.length === 0 ? null
      : `Al cobrar quedará en negativo: ${negativos.map((f) => `«${f.nombre}» (−${-f.queda})`).join(', ')}. Recuerda reponerlo.`, 'is-aviso');
  };

  // Los nombres van seguidos (Usados[0], Usados[1]…): el servidor deja de leer en el primer hueco
  const renumerar = () => {
    filas().forEach((fila, i) => {
      fila.querySelectorAll('[name^="Usados["]').forEach((campo) => {
        campo.name = campo.name.replace(/^Usados\[\d+\]/, `Usados[${i}]`);
      });
    });
    lista.hidden = filas().length === 0;
  };

  const preparar = (fila) => {
    fila.querySelector('[data-linea-cantidad]').addEventListener('input', revisar);
    fila.querySelector('[data-linea-quitar]').addEventListener('click', () => {
      fila.remove();
      renumerar();
      revisar();
      elegir.buscable?.enfocar();
    });
  };

  function anadir() {
    const opcion = elegir.selectedOptions[0];
    if (!opcion || !opcion.value) {
      avisar('Elige primero un producto de la lista.');
      elegir.buscable?.enfocar();
      return;
    }
    const cantidad = Math.max(1, Math.trunc(Number(cantidadNueva.value)) || 1);
    const existente = filas().find((f) => f.dataset.productoId === opcion.value);
    if (existente) {
      const campo = existente.querySelector('[data-linea-cantidad]');
      campo.value = String(unidades(existente) + cantidad);
    } else {
      const fila = plantilla.content.firstElementChild.cloneNode(true);
      const nombre = opcion.dataset.nombre;
      fila.dataset.productoId = opcion.value;
      fila.dataset.stock = opcion.dataset.stock;
      fila.querySelector('[data-linea-nombre]').textContent = nombre;
      fila.querySelector('[data-linea-referencia]').textContent = opcion.dataset.referencia;
      fila.querySelector('[data-linea-stock]').textContent = enStock(Number(opcion.dataset.stock));
      fila.querySelector('[data-linea-id]').value = opcion.value;
      const campo = fila.querySelector('[data-linea-cantidad]');
      campo.value = String(cantidad);
      campo.setAttribute('aria-label', `Unidades utilizadas de ${nombre}`);
      fila.querySelector('[data-linea-quitar]').setAttribute('aria-label', `Quitar ${nombre}`);
      if (opcion.dataset.foto) {
        const imagen = document.createElement('img');
        imagen.src = opcion.dataset.foto;
        imagen.alt = '';
        imagen.width = 40;
        imagen.height = 40;
        imagen.addEventListener('error', () => imagen.remove(), { once: true });
        fila.querySelector('.producto-miniatura').append(imagen);
      }
      lista.append(fila);
      preparar(fila);
      renumerar();
    }

    cantidadNueva.value = '1';
    // Listo para el siguiente producto (o el siguiente código del lector)
    elegir.value = '';
    elegir.dispatchEvent(new Event('change', { bubbles: true }));
    elegir.buscable?.enfocar();
    revisar();
  }

  filas().forEach(preparar);
  bloque.querySelector('[data-usado-anadir]').addEventListener('click', anadir);
  // Con el lector de códigos: el código elige el producto (Enter en el buscador), el foco pasa a las unidades y Enter lo añade
  elegir.addEventListener('change', () => {
    if (elegir.value) cantidadNueva.focus();
  });
  cantidadNueva.addEventListener('keydown', (e) => {
    if (e.key !== 'Enter') return;
    e.preventDefault();
    anadir();
  });
  // Enter en una fila no envía el cobro a medias
  lista.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && e.target.matches('[data-linea-cantidad]')) e.preventDefault();
  });
  renumerar();
  revisar();
})();
