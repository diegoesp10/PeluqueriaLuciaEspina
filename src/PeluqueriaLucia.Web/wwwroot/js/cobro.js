// Pantalla de cobro: en pago mixto, al escribir una parte se completa la otra y se comprueba que cuadre con el total. El
// total cambia si se cambian los precios (visita-editor.js lo avisa con el evento «visita:total»). En efectivo (o en la
// parte en efectivo de un pago mixto), la calculadora del cambio dice cuánto devolver según lo que da el cliente; lo que da
// se envía al cobrar y sale en el ticket con el cambio.
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

  // ── Calculadora del cambio ────────────────────────────────────────────────
  // Lo que te da se envía al cobrar (campo «Entregado») para el ticket; con tarjeta se desactiva y no se envía.
  const cambio = form.querySelector('[data-cambio]');
  if (!cambio) return;

  const entrega = cambio.querySelector('[data-cambio-entrega]');
  const textoCobrar = cambio.querySelector('[data-cambio-cobrar]');
  const resultado = cambio.querySelector('[data-cambio-resultado]');
  const desglose = cambio.querySelector('[data-cambio-desglose]');
  // Billetes y monedas de euro, en céntimos, de mayor a menor
  const valores = [50000, 20000, 10000, 5000, 2000, 1000, 500, 200, 100, 50, 20, 10, 5, 2, 1];

  // Lo que se cobra en efectivo, en céntimos: el total o la parte en efectivo del mixto; null si se paga con tarjeta
  const aCobrar = () => {
    const forma = form.elements.FormaPago.value;
    if (forma === 'Efectivo') return centimos(total());
    if (forma === 'Mixto') {
      const parte = leerImporte(efectivo.value);
      return Number.isNaN(parte) ? NaN : centimos(parte);
    }
    return null;
  };

  const nombre = (c) => (c >= 100 ? `${c / 100} €` : `${c} cts`);

  // «20 € + 2 × 2 € + 50 cts»: la forma de devolverlo con menos piezas
  const desglosar = (c) => {
    const partes = [];
    for (const valor of valores) {
      const n = Math.floor(c / valor);
      if (n > 0) {
        partes.push(n > 1 ? `${n} × ${nombre(valor)}` : nombre(valor));
        c -= n * valor;
      }
    }
    return partes.join(' + ');
  };

  const calcular = () => {
    const cobrar = aCobrar();
    cambio.hidden = cobrar === null;
    entrega.disabled = cobrar === null;
    if (cobrar === null) return;

    const valido = !Number.isNaN(cobrar) && cobrar > 0;
    textoCobrar.textContent = valido ? euros.format(cobrar / 100) : '—';
    const dado = leerImporte(entrega.value);
    let texto;
    let clase = '';
    let detalle = '';
    if (!valido) texto = esMixto() ? 'Escribe primero cuánto paga en efectivo.' : 'Revisa los precios de la visita.';
    else if (entrega.value.trim() === '') texto = 'Escribe lo que te da, o súmalo con los botones, y te digo cuánto devolver.';
    else if (Number.isNaN(dado) || dado < 0) {
      texto = 'Revisa lo que te da.';
      clase = 'is-error';
    } else {
      const diferencia = centimos(dado) - cobrar;
      if (diferencia < 0) {
        texto = `Faltan ${euros.format(-diferencia / 100)}`;
        clase = 'is-error';
      } else if (diferencia === 0) {
        texto = 'Justo: no hay que devolver nada';
        clase = 'is-ok';
      } else {
        texto = `Devuelve ${euros.format(diferencia / 100)}`;
        clase = 'is-devolver';
        detalle = desglosar(diferencia);
      }
    }
    resultado.textContent = texto;
    resultado.classList.remove('is-ok', 'is-error', 'is-devolver');
    if (clase) resultado.classList.add(clase);
    desglose.textContent = detalle;
    desglose.hidden = detalle === '';
  };

  cambio.querySelectorAll('[data-cambio-sumar]').forEach((boton) =>
    boton.addEventListener('click', () => {
      const actual = leerImporte(entrega.value);
      const base = Number.isNaN(actual) ? 0 : centimos(actual);
      entrega.value = importeCampo((base + Number(boton.dataset.cambioSumar) * 100) / 100);
      calcular();
    }),
  );
  cambio.querySelector('[data-cambio-justo]').addEventListener('click', () => {
    const cobrar = aCobrar();
    if (cobrar !== null && !Number.isNaN(cobrar) && cobrar > 0) entrega.value = importeCampo(cobrar / 100);
    calcular();
  });
  cambio.querySelector('[data-cambio-borrar]').addEventListener('click', () => {
    entrega.value = '';
    calcular();
    entrega.focus();
  });
  entrega.addEventListener('input', calcular);
  // Intro en la calculadora no envía la visita (el formulario tiene muchos botones)
  entrega.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') e.preventDefault();
  });
  efectivo.addEventListener('input', calcular);
  tarjeta.addEventListener('input', calcular);
  form.querySelectorAll('input[name="FormaPago"]').forEach((radio) => radio.addEventListener('change', calcular));
  form.addEventListener('visita:total', calcular);
  calcular();
})();
