// Pruebas del indicador de carga (cargando.js) sin navegador: documento y temporizadores simulados.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const { runInNewContext } = require('node:vm');
const codigo = readFileSync(resolve(__dirname, '../../../src/PeluqueriaLucia.Web/wwwroot/js/cargando.js'), 'utf8');
const esperar = () => new Promise((resolver) => setImmediate(resolver));

function pagina({ lang = 'es', fetch = async () => ({ ok: true }) } = {}) {
  const elementos = [];
  const crearElemento = () => {
    const e = { hidden: false, dataset: {}, atributos: {}, innerHTML: '', setAttribute(k, v) { this.atributos[k] = v; } };
    elementos.push(e);
    return e;
  };
  const botones = [];
  const cuerpo = { append(e) { cuerpo.hijos.push(e); }, hijos: [] };
  const documento = Object.assign(new EventTarget(), {
    documentElement: { lang }, body: cuerpo, createElement: crearElemento,
    querySelectorAll: () => botones,
  });
  const ventana = Object.assign(new EventTarget(), { location: { href: 'https://localhost/admin/visitas?periodo=Dia', origin: 'https://localhost', pathname: '/admin/visitas', search: '?periodo=Dia' }, fetch });
  const plazos = new Map();
  let siguiente = 0;
  runInNewContext(codigo, {
    document: documento, window: ventana, URL,
    setTimeout: (fn, ms) => { plazos.set(++siguiente, { fn, ms }); return siguiente; },
    clearTimeout: (id) => plazos.delete(id),
  });
  const avanzar = (hastaMs) => {
    for (const [id, { fn, ms }] of [...plazos.entries()].sort((a, b) => a[1].ms - b[1].ms)) {
      if (ms <= hastaMs) { plazos.delete(id); fn(); }
    }
  };
  const indicador = () => elementos.find((e) => e.atributos.role === 'status');
  const enlace = (href, extra = {}) => ({
    getAttribute: (n) => (n === 'href' ? href : null), hasAttribute: (n) => Boolean(extra[n]), target: extra.target ?? '',
  });
  const clic = (destino, opciones = {}) => {
    const evento = new Event('click', { cancelable: true });
    Object.defineProperty(evento, 'target', { value: { closest: () => destino } });
    Object.assign(evento, { button: 0, metaKey: false, ctrlKey: false, shiftKey: false, altKey: false }, opciones);
    documento.dispatchEvent(evento);
  };
  return { documento, ventana, avanzar, indicador, enlace, clic, botones, Cargando: ventana.Cargando };
}

test('una llamada fetch enseña la barra pasados 150 ms y la tarjeta pasados 900, y lo retira al terminar', async () => {
  let resolver;
  const p = pagina({ fetch: () => new Promise((r) => { resolver = r; }) });
  const promesa = p.ventana.fetch('/admin/clientes/buscar?q=ana');
  assert.equal(p.Cargando.activas(), 1);
  assert.equal(p.indicador(), undefined, 'antes del retardo no se crea nada');
  p.avanzar(150);
  assert.equal(p.indicador().hidden, false);
  assert.equal(p.indicador().dataset.nivel, 'barra');
  assert.match(p.indicador().innerHTML, /Cargando…/);
  p.avanzar(900);
  assert.equal(p.indicador().dataset.nivel, 'tarjeta');
  resolver({ ok: true });
  await promesa;
  await esperar();
  assert.equal(p.Cargando.activas(), 0);
  assert.equal(p.indicador().hidden, true);
  assert.equal(p.indicador().dataset.nivel, undefined);
});

test('las llamadas silenciosas (sesión, avisos) no lo activan y la web inglesa dice Loading', async () => {
  const p = pagina({ lang: 'en' });
  await p.ventana.fetch('/admin/acceso/estado', { silencioso: true });
  assert.equal(p.Cargando.activas(), 0);
  p.Cargando.iniciar();
  p.avanzar(150);
  assert.match(p.indicador().innerHTML, /Loading…/);
  p.Cargando.terminar();
  assert.equal(p.indicador().hidden, true);
});

test('los enlaces de esta web lo activan; las anclas, las descargas, otros sitios y las pestañas nuevas no', () => {
  const p = pagina();
  p.clic(p.enlace('#menu-publico'));
  p.clic(p.enlace('/admin/visitas?periodo=Dia#hoy'));
  p.clic(p.enlace('/admin/visitas/exportar', { download: true }));
  p.clic(p.enlace('/admin/visitas/exportar', { 'data-sin-cargando': true }));
  p.clic(p.enlace('https://www.google.com/'));
  p.clic(p.enlace('/admin/clientes', { target: '_blank' }));
  p.clic(p.enlace('/admin/clientes'), { ctrlKey: true });
  assert.equal(p.Cargando.activas(), 0);
  p.clic(p.enlace('/admin/clientes'));
  assert.equal(p.Cargando.activas(), 1);
  p.avanzar(150);
  assert.equal(p.indicador().dataset.nivel, 'barra');
  // Tras un tope de seguridad (descargas sin atributo, diálogos cancelados) se retira solo
  p.avanzar(20000);
  assert.equal(p.Cargando.activas(), 0);
});

test('enviar un formulario lo activa y marca el botón pulsado; volver desde el historial lo retira todo', () => {
  const p = pagina();
  const clases = new Set();
  const boton = { classList: { add: (c) => clases.add(c), remove: (c) => clases.delete(c) }, setAttribute() {}, removeAttribute() {} };
  p.botones.push(boton);
  const evento = new Event('submit', { cancelable: true });
  Object.defineProperty(evento, 'target', { value: { target: '', hasAttribute: () => false } });
  evento.submitter = boton;
  p.documento.dispatchEvent(evento);
  assert.equal(p.Cargando.activas(), 1);
  assert.ok(clases.has('is-cargando'));
  p.ventana.dispatchEvent(new Event('pageshow'));
  assert.equal(p.Cargando.activas(), 0);
  assert.ok(!clases.has('is-cargando'));
});

test('un formulario que ya gestiona un script (preventDefault) no lo activa', () => {
  const p = pagina();
  const evento = new Event('submit', { cancelable: true });
  Object.defineProperty(evento, 'target', { value: { target: '', hasAttribute: () => false } });
  evento.preventDefault();
  p.documento.dispatchEvent(evento);
  assert.equal(p.Cargando.activas(), 0);
});
