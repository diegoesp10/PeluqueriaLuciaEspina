// Pruebas de la calculadora del cambio de la pantalla de cobro (cobro.js) sin navegador: formulario simulado y las
// utilidades reales de app.js (leer importes, céntimos y formato en euros).
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const { runInNewContext } = require('node:vm');
const js = (nombre) => readFileSync(resolve(__dirname, `../../../src/PeluqueriaLucia.Web/wwwroot/js/${nombre}`), 'utf8');
const app = js('app.js');
const cobro = js('cobro.js');

function elemento(extra = {}) {
  const clases = new Set();
  return Object.assign(new EventTarget(), {
    value: '', hidden: false, disabled: false, textContent: '', dataset: {},
    classList: {
      add: (...c) => c.forEach((x) => clases.add(x)),
      remove: (...c) => c.forEach((x) => clases.delete(x)),
      toggle: (c, si) => (si ? clases.add(c) : clases.delete(c)),
      contains: (c) => clases.has(c),
    },
    focus() {},
  }, extra);
}

function pantalla({ total = '27.5', forma = 'Efectivo' } = {}) {
  // app.js en una página vacía, solo para tener window.Peluqueria
  const ventana = new EventTarget();
  const vacio = Object.assign(new EventTarget(), {
    documentElement: { lang: 'es', dataset: {} },
    querySelectorAll: () => [], querySelector: () => null, getElementById: () => null,
  });
  runInNewContext(app, { document: vacio, window: ventana, localStorage: { getItem: () => null }, matchMedia: () => ({ matches: false }), Intl });

  const efectivo = elemento();
  const tarjeta = elemento();
  const formaPago = { value: forma };
  const radios = ['Efectivo', 'Tarjeta', 'Mixto'].map((valor) => elemento({ value: valor }));
  const piezas = {
    '[data-mixto]': elemento(), '[data-mixto-estado]': elemento(), '[data-cobro-boton]': elemento({ dataset: { vacia: 'false' } }),
  };
  const sumar = [1, 2, 5, 10, 20, 50, 100].map((v) => elemento({ dataset: { cambioSumar: String(v) } }));
  const dentro = {
    '[data-cambio-entrega]': elemento(), '[data-cambio-cobrar]': elemento(), '[data-cambio-resultado]': elemento(),
    '[data-cambio-desglose]': elemento(), '[data-cambio-justo]': elemento(), '[data-cambio-borrar]': elemento(),
  };
  const cambio = elemento({ hidden: true, querySelector: (s) => dentro[s], querySelectorAll: () => sumar });
  piezas['[data-cambio]'] = cambio;
  const form = elemento({
    dataset: { total },
    elements: { FormaPago: formaPago, Efectivo: efectivo, Tarjeta: tarjeta },
    querySelector: (s) => piezas[s] ?? null,
    querySelectorAll: () => radios,
  });
  const documento = { querySelector: (s) => (s === '[data-cobro]' ? form : null) };
  runInNewContext(cobro, { document: documento, window: ventana, Number, Math });

  const entrega = dentro['[data-cambio-entrega]'];
  const resultado = dentro['[data-cambio-resultado]'];
  const desglose = dentro['[data-cambio-desglose]'];
  return {
    form, cambio, efectivo, entrega, resultado, desglose,
    cobrar: () => dentro['[data-cambio-cobrar]'].textContent,
    escribir(campo, valor) { campo.value = valor; campo.dispatchEvent(new Event('input')); },
    forma(valor) { formaPago.value = valor; radios.find((r) => r.value === valor).dispatchEvent(new Event('change')); },
    sumar(valor) { sumar.find((b) => b.dataset.cambioSumar === String(valor)).dispatchEvent(new Event('click')); },
    justo: () => dentro['[data-cambio-justo]'].dispatchEvent(new Event('click')),
    borrar: () => dentro['[data-cambio-borrar]'].dispatchEvent(new Event('click')),
  };
}

test('en efectivo dice cuánto devolver y con qué billetes y monedas', () => {
  const p = pantalla({ total: '27.5' });
  assert.equal(p.cambio.hidden, false);
  assert.match(p.cobrar(), /^27,50\s€$/);
  assert.match(p.resultado.textContent, /Escribe lo que te da/);

  p.escribir(p.entrega, '50');
  assert.match(p.resultado.textContent, /^Devuelve 22,50\s€$/);
  assert.ok(p.resultado.classList.contains('is-devolver'));
  assert.equal(p.desglose.textContent, '20 € + 2 € + 50 cts');
  assert.equal(p.desglose.hidden, false);

  p.escribir(p.entrega, '20');
  assert.match(p.resultado.textContent, /^Faltan 7,50\s€$/);
  assert.ok(p.resultado.classList.contains('is-error'));
  assert.equal(p.desglose.hidden, true);

  p.escribir(p.entrega, 'abc');
  assert.equal(p.resultado.textContent, 'Revisa lo que te da.');
});

test('los botones suman billetes, «Justo» pone el importe exacto y «Borrar» lo vacía', () => {
  const p = pantalla({ total: '12.15' });
  p.sumar(20);
  p.sumar(20);
  p.sumar(10);
  assert.equal(p.entrega.value, '50,00');
  assert.match(p.resultado.textContent, /^Devuelve 37,85\s€$/);
  assert.equal(p.desglose.textContent, '20 € + 10 € + 5 € + 2 € + 50 cts + 20 cts + 10 cts + 5 cts');

  p.justo();
  assert.equal(p.entrega.value, '12,15');
  assert.equal(p.resultado.textContent, 'Justo: no hay que devolver nada');
  assert.ok(p.resultado.classList.contains('is-ok'));

  p.borrar();
  assert.equal(p.entrega.value, '');
  assert.match(p.resultado.textContent, /Escribe lo que te da/);
});

test('repite piezas iguales en el desglose y acepta coma o punto', () => {
  const p = pantalla({ total: '1' });
  p.escribir(p.entrega, '5.00');
  assert.equal(p.desglose.textContent, '2 × 2 €');
  p.escribir(p.entrega, '1,04');
  assert.equal(p.desglose.textContent, '2 × 2 cts');
});

test('con tarjeta no sale; en mixto cuenta solo la parte en efectivo', () => {
  const p = pantalla({ total: '27.5' });
  p.forma('Tarjeta');
  assert.equal(p.cambio.hidden, true);

  p.forma('Mixto');
  assert.equal(p.cambio.hidden, false);
  assert.equal(p.resultado.textContent, 'Escribe primero cuánto paga en efectivo.');
  p.escribir(p.efectivo, '10');
  p.escribir(p.entrega, '20');
  assert.match(p.cobrar(), /^10,00\s€$/);
  assert.match(p.resultado.textContent, /^Devuelve 10,00\s€$/);
  assert.equal(p.desglose.textContent, '10 €');
});

test('lo que te da se envía al cobrar, salvo con tarjeta', () => {
  const p = pantalla({ total: '27.5' });
  assert.equal(p.entrega.disabled, false);
  p.escribir(p.entrega, '50');
  p.forma('Tarjeta');
  assert.equal(p.entrega.disabled, true);
  assert.equal(p.entrega.value, '50');
  p.forma('Mixto');
  assert.equal(p.entrega.disabled, false);
});

test('si cambian los precios de la visita, recalcula con el total nuevo', () => {
  const p = pantalla({ total: '27.5' });
  p.escribir(p.entrega, '50');
  p.form.dataset.total = '30';
  p.form.dispatchEvent(new Event('visita:total'));
  assert.match(p.cobrar(), /^30,00\s€$/);
  assert.match(p.resultado.textContent, /^Devuelve 20,00\s€$/);
});
