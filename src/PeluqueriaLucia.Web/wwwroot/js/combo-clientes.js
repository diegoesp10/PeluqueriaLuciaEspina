// Buscador de clientes compartido (Nueva visita, Nueva cita): sugiere fichas mientras se escribe, con teclado y ratón.
// Sin JavaScript, cada pantalla conserva su formulario de búsqueda normal; con él, el botón «Buscar» sobra y se oculta.
'use strict';

window.ComboClientes = (() => {
  const sexos = ['Hombre', 'Mujer', 'Otro'];
  const tintes = { Hombre: 'tint-blue', Mujer: 'tint-pink', Otro: 'tint-purple' };
  const tinte = (cliente) => tintes[sexos[cliente.sexo]] ?? '';

  /**
   * raiz: el elemento [data-combo], con un [data-combo-input] (data-url-buscar) y una lista [data-combo-lista].
   * alElegir(cliente): recibe { id, nombre, detalle, iniciales, sexo } al pulsar una sugerencia o Enter sobre ella.
   */
  function montar(raiz, { alElegir }) {
    const buscador = raiz?.querySelector('[data-combo-input]');
    const lista = raiz?.querySelector('[data-combo-lista]');
    if (!buscador || !lista) return null;
    let sugerencias = [];
    let activa = -1;
    let peticion;
    let espera;

    const abrirLista = (abrir) => {
      lista.hidden = !abrir;
      buscador.setAttribute('aria-expanded', String(abrir));
    };

    const pintar = () => {
      lista.replaceChildren();
      if (sugerencias.length === 0) {
        const vacia = document.createElement('li');
        vacia.className = 'row row-vacia';
        vacia.textContent = 'No hay clientes con ese texto.';
        lista.append(vacia);
        buscador.setAttribute('aria-activedescendant', '');
        return;
      }
      sugerencias.forEach((c, i) => {
        const fila = document.createElement('li');
        fila.className = 'row';
        fila.id = `cliente-opcion-${c.id}`;
        fila.setAttribute('role', 'option');
        fila.setAttribute('aria-selected', String(i === activa));

        const avatar = document.createElement('span');
        avatar.className = `avatar ${tinte(c)}`;
        avatar.textContent = c.iniciales;

        const texto = document.createElement('span');
        texto.className = 'row-main';
        const nombre = document.createElement('span');
        nombre.className = 'row-title';
        nombre.textContent = c.nombre;
        texto.append(nombre);
        if (c.detalle) {
          const detalle = document.createElement('span');
          detalle.className = 'row-sub';
          detalle.textContent = c.detalle;
          texto.append(detalle);
        }

        fila.append(avatar, texto);
        // mousedown no quita el foco al buscador: así el blur no cierra la lista antes del clic
        fila.addEventListener('mousedown', (e) => e.preventDefault());
        fila.addEventListener('click', () => elegir(c));
        lista.append(fila);
      });
      buscador.setAttribute('aria-activedescendant', activa >= 0 ? `cliente-opcion-${sugerencias[activa].id}` : '');
    };

    const buscar = async () => {
      peticion?.abort();
      peticion = new AbortController();
      try {
        const respuesta = await fetch(`${buscador.dataset.urlBuscar}?q=${encodeURIComponent(buscador.value.trim())}`, {
          signal: peticion.signal,
          headers: { Accept: 'application/json' },
          silencioso: true,
        });
        if (respuesta.status === 401 || respuesta.status === 403) {
          window.SesionAdministrador?.expulsar();
          return;
        }
        if (!respuesta.ok) throw new Error(respuesta.statusText);
        sugerencias = await respuesta.json();
        activa = sugerencias.length > 0 ? 0 : -1;
        pintar();
        abrirLista(true);
      } catch (error) {
        if (error.name !== 'AbortError') abrirLista(false);
      }
    };

    const elegir = (c) => {
      abrirLista(false);
      alElegir(c);
    };

    buscador.addEventListener('input', () => {
      clearTimeout(espera);
      espera = setTimeout(buscar, 180);
    });
    buscador.addEventListener('focus', buscar);
    buscador.addEventListener('blur', () => setTimeout(() => abrirLista(false), 120));
    buscador.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        // Enter elige la sugerencia activa y nunca envía el formulario que contiene al buscador
        e.preventDefault();
        if (!lista.hidden && activa >= 0) elegir(sugerencias[activa]);
        return;
      }
      if (lista.hidden || sugerencias.length === 0) return;
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault();
        activa = (activa + (e.key === 'ArrowDown' ? 1 : -1) + sugerencias.length) % sugerencias.length;
        pintar();
        lista.children[activa]?.scrollIntoView({ block: 'nearest' });
      } else if (e.key === 'Escape') {
        abrirLista(false);
      }
    });
    raiz.querySelectorAll('[data-solo-sin-js]').forEach((elemento) => {
      elemento.hidden = true;
    });

    return {
      tinte,
      limpiar() {
        clearTimeout(espera);
        peticion?.abort();
        buscador.value = '';
        sugerencias = [];
        activa = -1;
        abrirLista(false);
      },
      enfocar() {
        buscador.focus();
      },
    };
  }

  return { montar, tinte };
})();
