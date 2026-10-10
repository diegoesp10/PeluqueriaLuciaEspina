// Nueva visita: buscador de clientes, filtro de servicios por público y total en vivo.
'use strict';

(() => {
  const form = document.querySelector('[data-nueva-visita]');
  if (!form) return;

  const { euros } = window.Peluqueria;
  const sexos = ['Hombre', 'Mujer', 'Otro'];
  const tintes = { Hombre: 'tint-blue', Mujer: 'tint-pink', Otro: 'tint-purple' };

  // ── Cliente ──────────────────────────────────────────────────────────────
  const clienteId = form.querySelector('[data-cliente-id]');
  const elegido = form.querySelector('[data-cliente-elegido]');
  const combo = form.querySelector('[data-combo]');
  const buscador = form.querySelector('[data-combo-input]');
  const lista = form.querySelector('[data-combo-lista]');
  let sugerencias = [];
  let activa = -1;
  let peticion;

  const abrirLista = (abrir) => {
    lista.hidden = !abrir;
    buscador.setAttribute('aria-expanded', String(abrir));
  };

  const pintarSugerencias = () => {
    lista.replaceChildren();
    if (sugerencias.length === 0) {
      const vacia = document.createElement('li');
      vacia.className = 'row row-vacia';
      vacia.textContent = 'No hay clientes con ese nombre.';
      lista.append(vacia);
      return;
    }
    sugerencias.forEach((c, i) => {
      const fila = document.createElement('li');
      fila.className = 'row';
      fila.id = `cliente-opcion-${c.id}`;
      fila.setAttribute('role', 'option');
      fila.setAttribute('aria-selected', String(i === activa));

      const avatar = document.createElement('span');
      avatar.className = `avatar ${tintes[sexos[c.sexo]] ?? ''}`;
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
      const respuesta = await fetch(`/Clientes/Buscar?q=${encodeURIComponent(buscador.value.trim())}`, {
        signal: peticion.signal,
        headers: { Accept: 'application/json' },
      });
      if (!respuesta.ok) throw new Error(respuesta.statusText);
      sugerencias = await respuesta.json();
      activa = sugerencias.length > 0 ? 0 : -1;
      pintarSugerencias();
      abrirLista(true);
    } catch (error) {
      if (error.name !== 'AbortError') abrirLista(false);
    }
  };

  const elegir = (c) => {
    clienteId.value = c.id;
    elegido.querySelector('[data-cliente-nombre]').textContent = c.nombre;
    elegido.querySelector('[data-cliente-detalle]').textContent = c.detalle ?? '';
    const avatar = elegido.querySelector('[data-cliente-avatar]');
    avatar.className = `avatar ${tintes[sexos[c.sexo]] ?? ''}`;
    avatar.textContent = c.iniciales;
    elegido.hidden = false;
    combo.hidden = true;
    abrirLista(false);
    // Los servicios se filtran por el sexo del cliente
    const filtro = form.querySelector(`[data-filtro-servicios][value="${sexos[c.sexo]}"]`);
    if (filtro) {
      filtro.checked = true;
      filtrarServicios();
    }
  };

  let espera;
  buscador.addEventListener('input', () => {
    clearTimeout(espera);
    espera = setTimeout(buscar, 180);
  });
  buscador.addEventListener('focus', buscar);
  buscador.addEventListener('blur', () => setTimeout(() => abrirLista(false), 120));
  buscador.addEventListener('keydown', (e) => {
    if (lista.hidden || sugerencias.length === 0) return;
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      activa = (activa + (e.key === 'ArrowDown' ? 1 : -1) + sugerencias.length) % sugerencias.length;
      pintarSugerencias();
      lista.children[activa]?.scrollIntoView({ block: 'nearest' });
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (activa >= 0) elegir(sugerencias[activa]);
    } else if (e.key === 'Escape') {
      abrirLista(false);
    }
  });

  form.querySelector('[data-cliente-cambiar]').addEventListener('click', () => {
    clienteId.value = '';
    elegido.hidden = true;
    combo.hidden = false;
    buscador.value = '';
    buscador.focus();
  });

  // ── Servicios ────────────────────────────────────────────────────────────
  const opciones = [...form.querySelectorAll('.servicio-opcion')];
  const sinServicios = form.querySelector('[data-sin-servicios]');
  const textoTotal = form.querySelector('[data-total]');
  const textoNumero = form.querySelector('[data-numero-servicios]');

  // Un servicio ya marcado sigue a la vista aunque no sea del público filtrado
  function filtrarServicios() {
    const filtro = form.querySelector('[data-filtro-servicios]:checked')?.value ?? 'Todos';
    let visibles = 0;
    opciones.forEach((opcion) => {
      const marcado = opcion.querySelector('input').checked;
      const visible = filtro === 'Todos' || opcion.dataset.sexo === filtro || marcado;
      opcion.hidden = !visible;
      if (visible) visibles++;
    });
    if (sinServicios) sinServicios.hidden = visibles > 0;
  }

  const recalcular = () => {
    const marcados = opciones.map((o) => o.querySelector('input')).filter((i) => i.checked);
    const centimos = marcados.reduce((suma, i) => suma + Math.round(Number(i.dataset.precio) * 100), 0);
    textoTotal.textContent = euros.format(centimos / 100);
    textoNumero.textContent = marcados.length === 1 ? '1 servicio' : `${marcados.length} servicios`;
  };

  form.querySelectorAll('[data-filtro-servicios]').forEach((r) => r.addEventListener('change', filtrarServicios));
  opciones.forEach((o) => o.querySelector('input').addEventListener('change', recalcular));
  filtrarServicios();
  recalcular();

  // Enter en el buscador no debe enviar el formulario
  form.addEventListener('submit', (e) => {
    if (document.activeElement === buscador) e.preventDefault();
  });
})();
