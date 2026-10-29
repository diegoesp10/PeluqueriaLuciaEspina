// Pantalla de cobro: en pago mixto, al escribir una parte se completa la otra y se comprueba que cuadre con el total. El
// total cambia si se cambian los precios (visita-editor.js lo avisa con el evento «visita:total»).
'use strict';

(() => {
  const form = document.querySelector('[data-cobro]');
  if (!form) return;

  const { leerImporte, centimos, euros, importeCampo } = window.Peluqueria;
  const total = () => Number(form.dataset.total);
  const bloqueMixto = form.querySelector('[data-mixto]');
  const estado = form.querySelector('[data-mixto-estado]');
  const efectivo = form.elements.Efectivo;
  const tarjeta = form.elements.Tarjeta;
  const boton = form.querySelector('[data-cobro-boton]');
  // Sin servicios ni productos no hay nada que cobrar
  const vacia = boton.dataset.vacia === 'true';

  const esMixto = () => form.elements.FormaPago.value === 'Mixto';

  const comprobar = () => {
    if (!esMixto()) {
      boton.disabled = vacia;
      return;
    }
    const e = leerImporte(efectivo.value);
    const t = leerImporte(tarjeta.value);
    const diferencia = centimos(total()) - centimos(e) - centimos(t);
    let mensaje;
    let ok = false;
    if (Number.isNaN(total())) mensaje = 'Revisa los precios de la visita.';
    else if (Number.isNaN(e) || Number.isNaN(t) || e < 0 || t < 0) mensaje = 'Revisa los importes.';
    else if (centimos(e) === 0 || centimos(t) === 0) mensaje = 'Pon una parte en efectivo y otra con tarjeta.';
    else if (diferencia > 0) mensaje = `Faltan ${euros.format(diferencia / 100)}.`;
    else if (diferencia < 0) mensaje = `Sobran ${euros.format(-diferencia / 100)}.`;
    else {
      mensaje = 'Cuadra con el total.';
      ok = true;
    }
    estado.textContent = mensaje;
    estado.classList.toggle('is-ok', ok);
    estado.classList.toggle('is-error', !ok && (efectivo.value !== '' || tarjeta.value !== ''));
    boton.disabled = vacia || !ok;
  };

  const completar = (escrito, otro) => {
    const valor = leerImporte(escrito.value);
    if (escrito.value === '' || Number.isNaN(valor) || Number.isNaN(total())) return comprobar();
    const resto = (centimos(total()) - centimos(valor)) / 100;
    otro.value = resto >= 0 ? importeCampo(resto) : '';
    comprobar();
  };

  const alternar = () => {
    bloqueMixto.hidden = !esMixto();
    if (esMixto() && efectivo.value === '' && tarjeta.value === '') efectivo.focus();
    comprobar();
  };

  efectivo.addEventListener('input', () => completar(efectivo, tarjeta));
  tarjeta.addEventListener('input', () => completar(tarjeta, efectivo));
  form.querySelectorAll('input[name="FormaPago"]').forEach((radio) => radio.addEventListener('change', alternar));
  form.addEventListener('visita:total', comprobar);
  alternar();
})();
