// Nueva visita: buscador de clientes (combo-clientes.js), buscador y filtro de servicios, y total en vivo.
'use strict';

(() => {
  const form = document.querySelector('[data-nueva-visita]');
  if (!form) return;

  const { euros } = window.Peluqueria;
  const sexos = ['Hombre', 'Mujer', 'Otro'];

  // ── Cliente ──────────────────────────────────────────────────────────────
  const clienteId = form.querySelector('[data-cliente-id]');
  const elegido = form.querySelector('[data-cliente-elegido]');
  const comboRaiz = form.querySelector('[data-combo]');

  function elegir(c) {
    clienteId.value = c.id;
    elegido.querySelector('[data-cliente-nombre]').textContent = c.nombre;
    elegido.querySelector('[data-cliente-detalle]').textContent = c.detalle ?? '';
    const avatar = elegido.querySelector('[data-cliente-avatar]');
    avatar.className = `avatar ${window.ComboClientes.tinte(c)}`;
    avatar.textContent = c.iniciales;
    elegido.hidden = false;
    comboRaiz.hidden = true;
    // Los servicios se filtran por el sexo del cliente
    const filtro = form.querySelector(`[data-filtro-servicios][value="${sexos[c.sexo]}"]`);
    if (filtro) {
      filtro.checked = true;
      filtrarServicios();
    }
  }

  const combo = window.ComboClientes?.montar(comboRaiz, { alElegir: elegir });

  form.querySelector('[data-cliente-cambiar]').addEventListener('click', () => {
    clienteId.value = '';
    elegido.hidden = true;
    comboRaiz.hidden = false;
    combo?.limpiar();
    combo?.enfocar();
  });

  // ── Servicios ────────────────────────────────────────────────────────────
  const opciones = [...form.querySelectorAll('.servicio-opcion')];
  const sinServicios = form.querySelector('[data-sin-servicios]');
  const textoTotal = form.querySelector('[data-total]');
  const textoNumero = form.querySelector('[data-numero-servicios]');
  const buscadorServicios = form.querySelector('[data-filtro-texto]');

  // «Ampolla» encuentra «AMPOLLA caída / hidratación»: sin mayúsculas ni acentos
  const normalizar = (texto) => texto.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();

  // Un servicio ya marcado sigue a la vista aunque no sea del público filtrado ni coincida con lo escrito
  function filtrarServicios() {
    const filtro = form.querySelector('[data-filtro-servicios]:checked')?.value ?? 'Todos';
    const texto = normalizar(buscadorServicios?.value ?? '');
    let visibles = 0;
    opciones.forEach((opcion) => {
      const marcado = opcion.querySelector('input[type=checkbox]').checked;
      const delPublico = filtro === 'Todos' || opcion.dataset.sexo === filtro;
      const coincide = !texto || normalizar(opcion.querySelector('.row-title').textContent).includes(texto);
      const visible = marcado || (delPublico && coincide);
      opcion.hidden = !visible;
      if (visible) visibles++;
    });
    if (sinServicios) sinServicios.hidden = visibles > 0;
  }

  const recalcular = () => {
    const marcados = opciones.map((o) => o.querySelector('input[type=checkbox]')).filter((i) => i.checked);
    const centimos = marcados.reduce((suma, i) => suma + Math.round((Number(form.querySelector(`[data-importe-servicio="${i.value}"]`).value.replace(",",".")) || 0) * 100), 0);
    textoTotal.textContent = euros.format(centimos / 100);
    textoNumero.textContent = marcados.length === 1 ? '1 servicio' : `${marcados.length} servicios`;
  };

  form.querySelectorAll('[data-filtro-servicios]').forEach((r) => r.addEventListener('change', filtrarServicios));
  buscadorServicios?.addEventListener('input', filtrarServicios);
  // Enter en el buscador de servicios filtra, no guarda la visita
  buscadorServicios?.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') e.preventDefault();
  });
  opciones.forEach((o) => {
    const check = o.querySelector('input[type=checkbox]'), precio = o.querySelector('[data-importe-servicio]');
    check.addEventListener('change',() => {
      precio.disabled = !check.checked;
      o.querySelector('.importe-visita').hidden = !check.checked;
      o.querySelector('[data-importe-base]').hidden = check.checked;
      recalcular();
    });
    precio.addEventListener('input',recalcular);
  });
  filtrarServicios();
  recalcular();
})();
