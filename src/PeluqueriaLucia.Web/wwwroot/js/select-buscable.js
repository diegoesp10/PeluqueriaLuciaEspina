// Desplegables con buscador: <select data-buscable="texto de ayuda"> se convierte en un campo donde se escribe y se
// filtran las opciones, sin distinguir mayúsculas ni acentos, con teclado y ratón. El <select> sigue en el formulario
// (es el que se envía) y sin JavaScript se usa tal cual.
// · En cada opción, el texto antes del primer « · » es el título y lo de después el detalle («Corte · 30 min»).
// · data-buscable-vacio: texto cuando nada coincide.
// · data-buscar en una <option>: palabras que también la encuentran sin mostrarse (código de barras, marca…).
// · data-buscable-libre="nombre": si lo escrito no está en la lista, se ofrece una fila para usarlo tal cual
//   (data-buscable-libre-texto, con {0}); el texto va al campo «nombre» del mismo formulario, que se oculta
//   (o su contenedor [data-buscable-libre-campo]) y el <select> queda en su opción vacía.
// · select.buscable.filtrar(fn) limita la lista a las <option> para las que fn devuelve true (null: todas);
//   la opción elegida nunca se oculta.
// · El evento change del <select> solo salta cuando la elección cambia de verdad.
'use strict';

window.SelectBuscable = (() => {
  const normalizar = (texto) => texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();
  const icono = (trazos, ancho = 2) =>
    `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="${ancho}" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">${trazos}</svg>`;
  let contador = 0;

  function montar(select) {
    if (select.buscable) return select.buscable;
    contador += 1;

    const idLista = `buscable-lista-${contador}`;
    const vacia = [...select.options].find((o) => o.value === '');
    const opciones = [...select.options]
      .filter((o) => o.value !== '' && !o.disabled)
      .map((o) => {
        const [titulo, ...resto] = o.textContent.split(' · ');
        return { valor: o.value, texto: o.textContent.trim(), titulo: titulo.trim(), detalle: resto.join(' · ').trim(), clave: normalizar(`${o.textContent} ${o.dataset.buscar ?? ''}`), elemento: o };
      });

    // Texto libre: el campo del formulario que lo recibe deja de verse; lo escrito se elige desde la lista
    const campoLibre = select.dataset.buscableLibre ? select.form?.elements.namedItem(select.dataset.buscableLibre) : null;
    const plantillaLibre = select.dataset.buscableLibreTexto || '«{0}»';
    if (campoLibre) (campoLibre.closest('[data-buscable-libre-campo]') ?? campoLibre).hidden = true;

    const raiz = document.createElement('div');
    raiz.className = 'combo select-buscable';
    const caja = document.createElement('label');
    caja.className = 'search search-full';
    caja.innerHTML = icono('<circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>');
    const campo = document.createElement('input');
    campo.type = 'search';
    campo.className = 'input';
    campo.id = `${select.id || `buscable-${contador}`}-buscar`;
    campo.autocomplete = 'off';
    campo.placeholder = select.dataset.buscable || 'Buscar';
    campo.setAttribute('role', 'combobox');
    campo.setAttribute('aria-autocomplete', 'list');
    campo.setAttribute('aria-expanded', 'false');
    campo.setAttribute('aria-controls', idLista);
    campo.required = select.required;
    caja.append(campo);
    const lista = document.createElement('ul');
    lista.className = 'combo-lista list';
    lista.id = idLista;
    lista.setAttribute('role', 'listbox');
    lista.hidden = true;
    raiz.append(caja, lista);

    // La etiqueta del campo pasa a señalar el buscador; el <select> queda oculto pero dentro del formulario
    document.querySelectorAll(`label[for="${CSS.escape(select.id)}"]`).forEach((etiqueta) => {
      etiqueta.htmlFor = campo.id;
    });
    select.required = false;
    select.hidden = true;
    select.tabIndex = -1;
    select.after(raiz);

    let visibles = [];
    let activa = -1;
    let sinCoincidencias = false;
    let filtro = null;
    let cierre = null;

    const elegida = () => opciones.find((o) => o.valor === select.value) ?? null;
    const textoElegido = () => elegida()?.texto ?? (campoLibre?.value || '');
    const mostrarElegida = () => {
      campo.value = textoElegido();
    };
    const abrir = (si) => {
      lista.hidden = !si;
      campo.setAttribute('aria-expanded', String(si));
      if (!si) campo.setAttribute('aria-activedescendant', '');
    };

    const fila = (item, indice) => {
      const li = document.createElement('li');
      li.className = 'row';
      li.id = `${idLista}-${indice}`;
      li.setAttribute('role', 'option');
      li.setAttribute('aria-selected', String(indice === activa));
      const principal = document.createElement('span');
      principal.className = 'row-main';
      const titulo = document.createElement('span');
      titulo.className = 'row-title';
      if (item === null) {
        titulo.textContent = vacia.textContent.trim();
        li.classList.add('row-quitar');
      } else if (item.libre) {
        titulo.textContent = plantillaLibre.replace('{0}', item.libre);
        li.classList.add('row-libre');
        const marca = document.createElement('span');
        marca.className = 'row-icono';
        marca.innerHTML = icono('<path d="M12 5v14"/><path d="M5 12h14"/>');
        li.append(marca);
      } else {
        titulo.textContent = item.titulo;
      }
      principal.append(titulo);
      if (item?.detalle) {
        const detalle = document.createElement('span');
        detalle.className = 'row-sub';
        detalle.textContent = item.detalle;
        principal.append(detalle);
      }
      li.append(principal);
      if (item && !item.libre && item.valor === select.value) {
        const marca = document.createElement('span');
        marca.className = 'row-marca';
        marca.innerHTML = icono('<path d="M20 6 9 17l-5-5"/>', 2.4);
        li.append(marca);
      }
      // mousedown no quita el foco al campo, así el blur no cierra la lista antes del clic
      li.addEventListener('mousedown', (e) => e.preventDefault());
      li.addEventListener('click', () => elegir(item));
      return li;
    };

    const pintar = () => {
      lista.replaceChildren();
      if (sinCoincidencias) {
        const nada = document.createElement('li');
        nada.className = 'row row-vacia';
        nada.textContent = select.dataset.buscableVacio || 'No hay opciones con ese texto.';
        lista.append(nada);
      }
      visibles.forEach((item, i) => lista.append(fila(item, i)));
      campo.setAttribute('aria-activedescendant', activa >= 0 ? `${idLista}-${activa}` : '');
    };

    const filtrar = (texto) => {
      const clave = normalizar(texto);
      const actual = elegida();
      const permitidas = filtro ? opciones.filter((o) => o === actual || filtro(o.elemento)) : opciones;
      const coinciden = clave ? permitidas.filter((o) => clave.split(/\s+/).every((parte) => o.clave.includes(parte))) : permitidas;
      // Sin texto, la opción vacía («Sin indicar», «Consultar en la peluquería») encabeza la lista
      visibles = !clave && vacia ? [null, ...coinciden] : [...coinciden];
      const libre = texto.trim();
      if (campoLibre && libre.length >= 2 && !coinciden.some((o) => normalizar(o.titulo) === clave)) visibles.push({ libre });
      sinCoincidencias = clave !== '' && coinciden.length === 0;
      activa = visibles.length === 0 ? -1 : Math.max(0, visibles.indexOf(actual));
      pintar();
      abrir(true);
      lista.children[activa + (sinCoincidencias ? 1 : 0)]?.scrollIntoView({ block: 'nearest' });
    };

    function elegir(item) {
      const antes = `${select.value}|${campoLibre?.value ?? ''}`;
      if (item?.libre) {
        select.value = '';
        campoLibre.value = item.libre;
      } else {
        select.value = item ? item.valor : '';
        if (campoLibre) campoLibre.value = '';
      }
      mostrarElegida();
      campo.setCustomValidity('');
      abrir(false);
      if (`${select.value}|${campoLibre?.value ?? ''}` !== antes) select.dispatchEvent(new Event('change', { bubbles: true }));
    }

    campo.addEventListener('focus', () => {
      // Volver al campo enseguida no debe cerrar la lista con el cierre pendiente de la salida anterior
      clearTimeout(cierre);
      campo.select();
      filtrar('');
    });
    campo.addEventListener('input', () => filtrar(campo.value));
    campo.addEventListener('blur', () => {
      cierre = setTimeout(() => {
        abrir(false);
        // Un texto que no se ha elegido de la lista no cambia nada: si se ha vaciado, vuelve a la opción vacía
        if (campo.value.trim() === '' && vacia) elegir(null);
        else mostrarElegida();
      }, 120);
    });
    campo.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        // Enter elige y nunca envía el formulario a medias
        e.preventDefault();
        if (!lista.hidden && activa >= 0) elegir(visibles[activa]);
        return;
      }
      if (e.key === 'Escape') {
        // Sin preventDefault, el navegador vaciaría el campo de búsqueda
        e.preventDefault();
        mostrarElegida();
        abrir(false);
        return;
      }
      if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
      e.preventDefault();
      if (lista.hidden) {
        filtrar(campo.value === textoElegido() ? '' : campo.value);
        return;
      }
      if (visibles.length === 0) return;
      activa = (activa + (e.key === 'ArrowDown' ? 1 : -1) + visibles.length) % visibles.length;
      pintar();
      lista.children[activa + (sinCoincidencias ? 1 : 0)]?.scrollIntoView({ block: 'nearest' });
    });
    select.form?.addEventListener('reset', () => setTimeout(mostrarElegida));
    // Si otro script cambia la elección y lo avisa con change (vaciarlo tras añadir un producto…), el campo lo refleja
    select.addEventListener('change', () => mostrarElegida());

    mostrarElegida();
    const api = {
      /** Limita la lista a las <option> para las que fn devuelve true; null las enseña todas. */
      filtrar(fn) {
        filtro = typeof fn === 'function' ? fn : null;
        if (!lista.hidden) filtrar(campo.value === textoElegido() ? '' : campo.value);
      },
      enfocar() {
        campo.focus();
      },
    };
    select.buscable = api;
    return api;
  }

  document.querySelectorAll('select[data-buscable]').forEach(montar);
  return { montar };
})();
