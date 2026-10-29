// Visita pendiente (un único formulario): el total y el botón «Cobrar» siguen a los precios escritos; al elegir un servicio
// para añadir se propone su precio del catálogo; al añadir un producto se elige si se vende o se usa en el servicio y, si se
// usa, se pregunta si se ha terminado. Enter en «Añadir…» añade; en los precios, los guarda (el botón por defecto).
'use strict';

(() => {
  const form = document.querySelector('[data-visita-editor]');
  if (!form) return;

  const { leerImporte, euros, importeCampo } = window.Peluqueria;

  // ── Total ────────────────────────────────────────────────────────────────
  const textoTotal = form.querySelector('[data-total-texto]');
  const textoCobrar = form.querySelector('[data-cobro-texto]');
  const precios = [...form.querySelectorAll('[data-precio-linea]')];

  // Cada precio por sus unidades (los servicios, una); lo usado en el servicio va a 0,00 € y no tiene campo
  const recalcular = () => {
    let centimos = 0;
    let valido = true;
    precios.forEach((campo) => {
      const precio = leerImporte(campo.value);
      const ok = campo.value.trim() !== '' && !Number.isNaN(precio) && precio > 0;
      campo.setAttribute('aria-invalid', String(!ok));
      if (ok) centimos += Math.round(precio * 100) * Number(campo.dataset.cantidad || 1);
      else valido = false;
    });
    const texto = euros.format(centimos / 100);
    textoTotal.textContent = valido ? texto : '—';
    textoCobrar.textContent = valido ? `Cobrar ${texto}` : 'Cobrar';
    form.dataset.total = valido ? (centimos / 100).toFixed(2) : 'NaN';
    form.dispatchEvent(new Event('visita:total'));
  };
  precios.forEach((campo) => campo.addEventListener('input', recalcular));

  // ── Añadir un servicio: se propone el precio del catálogo ───────────────────
  const servicio = form.querySelector('[data-servicio-elegir]');
  const precioServicio = form.querySelector('[data-servicio-precio]');
  servicio?.addEventListener('change', () => {
    precioServicio.value = servicio.selectedOptions[0]?.dataset.precio ?? '';
    if (servicio.value) precioServicio.focus();
  });

  // ── Añadir un producto: venta o usado en el servicio ────────────────────────
  const bloqueProducto = form.querySelector('[data-agregar-producto]');
  const enVenta = bloqueProducto?.querySelector('[data-uso-venta]');
  const enServicio = bloqueProducto?.querySelector('[data-uso-servicio]');
  const alternarUso = () => {
    const usado = form.elements.EnServicio?.value === 'true';
    enVenta.hidden = usado;
    enServicio.hidden = !usado;
  };
  bloqueProducto?.querySelectorAll('[data-uso]').forEach((radio) => radio.addEventListener('change', alternarUso));
  if (bloqueProducto) alternarUso();

  // Con el lector de códigos: el código elige el producto (Enter en el buscador) y el foco pasa a las unidades
  const producto = form.querySelector('[data-producto-elegir]');
  producto?.addEventListener('change', () => {
    if (producto.value) form.elements.Cantidad.focus();
  });

  // ── Enter ─────────────────────────────────────────────────────────────────
  // En lo de añadir, Enter pulsa su «Añadir a la visita»; en lo demás, el botón por defecto (guardar precios)
  [[form.querySelector('[data-agregar-servicio]'), '[data-servicio-anadir]'], [bloqueProducto, '[data-producto-anadir]']]
    .filter(([bloque]) => bloque)
    .forEach(([bloque, boton]) => bloque.addEventListener('keydown', (e) => {
      if (e.key !== 'Enter' || !e.target.matches('input:not([type=radio]):not([type=checkbox])') || e.defaultPrevented) return;
      e.preventDefault();
      bloque.querySelector(boton).click();
    }));

  // Mientras se cambian los precios, el formato se respeta: «12,5» se deja como «12,50» al salir del campo
  precios.forEach((campo) => campo.addEventListener('change', () => {
    const precio = leerImporte(campo.value);
    if (campo.value.trim() !== '' && !Number.isNaN(precio) && precio > 0) campo.value = importeCampo(precio);
  }));

  recalcular();
})();
