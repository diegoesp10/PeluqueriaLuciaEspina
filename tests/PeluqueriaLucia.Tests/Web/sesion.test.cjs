// Pruebas de comportamiento del panel abierto, sin navegador ni dependencias externas.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const { runInNewContext } = require('node:vm');
const codigo = readFileSync(resolve(__dirname, '../../../src/PeluqueriaLucia.Web/wwwroot/js/sesion.js'), 'utf8');
const terminar = () => new Promise((resolver) => setImmediate(resolver));
const respuesta = (status = 200, id = 'sesion-original', espera = null) => ({
  status, ok: status === 200, json: async () => ({ sesionId: id }), headers: { get: () => espera },
});

function panel({ id = 'sesion-original', avisar = false, fetch = async () => respuesta() } = {}) {
  const clases = new Set();
  const cuerpo = { dataset: id ? { sesionId: id, sesionEstado: '/admin/acceso/estado', sesionAcceso: '/admin/acceso' } : {},
    classList: { add: (c) => clases.add(c), remove: (c) => clases.delete(c) }, append() {} };
  const documento = Object.assign(new EventTarget(), { body: cuerpo, visibilityState: 'visible',
    querySelector: () => avisar ? {} : null, createElement: () => ({ setAttribute() {} }) });
  const destinos = [], peticiones = [], mensajes = [], almacen = [], intervalos = [], plazos = new Map();
  const ventana = Object.assign(new EventTarget(), { location: { replace: (ruta) => destinos.push(ruta) } });
  let canal, tiempo = 1000, siguiente = 0;
  class Canal extends EventTarget {
    constructor() { super(); canal = this; }
    postMessage(mensaje) { mensajes.push(mensaje); }
  }
  runInNewContext(codigo, { document: documento, window: ventana, BroadcastChannel: Canal, AbortController,
    Date: { now: () => tiempo }, Math, localStorage: { setItem: (k, v) => almacen.push([k, v]) },
    setInterval: (fn) => intervalos.push(fn), setTimeout: (fn) => { plazos.set(++siguiente, fn); return siguiente; },
    clearTimeout: (id) => plazos.delete(id), fetch: (...args) => { peticiones.push(args); return fetch(...args); } });
  return { destinos, peticiones, mensajes, almacen, documento, ventana, canal, intervalos, plazos,
    bloqueado: () => clases.has('sesion-en-comprobacion'), avanzar: (ms) => { tiempo += ms; } };
}
function evento(destino, nombre, datos = {}) {
  destino.dispatchEvent(Object.assign(new Event(nombre), datos));
}

test('el panel valida contra el servidor usando cookie y sin cachear; no entrega el token a JavaScript', async () => {
  const p = panel();
  await terminar();
  assert.equal(p.bloqueado(), false);
  assert.equal(p.peticiones[0][0], '/admin/acceso/estado');
  const opciones = p.peticiones[0][1];
  assert.equal(opciones.credentials, 'same-origin');
  assert.equal(opciones.cache, 'no-store');
  assert.equal(opciones.headers.Accept, 'application/json');
  assert.equal(p.almacen.length, 0);
});

test('una sesión ausente o revocada expulsa al acceso y oculta todo el panel', async () => {
  for (const codigo of [401, 403]) {
    const p = panel({ fetch: async () => respuesta(codigo) });
    await terminar();
    assert.deepEqual(p.destinos, ['/admin/acceso']);
    assert.equal(p.bloqueado(), true);
  }
});

test('volver a una pestaña comprueba la cookie y expulsa si se cerró en otro lugar', async () => {
  let cerrada = false;
  const p = panel({ fetch: async () => respuesta(cerrada ? 401 : 200) });
  await terminar();
  cerrada = true;
  evento(p.ventana, 'focus');
  await terminar();
  assert.deepEqual(p.destinos, ['/admin/acceso']);
});

test('Atrás no vuelve a mostrar la copia privada antes de revalidar', async () => {
  let cerrada = false;
  const p = panel({ fetch: async () => respuesta(cerrada ? 401 : 200) });
  await terminar();
  evento(p.ventana, 'pagehide');
  assert.equal(p.bloqueado(), true);
  cerrada = true;
  evento(p.ventana, 'pageshow', { persisted: true });
  await terminar();
  assert.equal(p.bloqueado(), true);
  assert.deepEqual(p.destinos, ['/admin/acceso']);
});

test('una cookie sustituida por otro inicio de sesión no conserva la pantalla antigua', async () => {
  const p = panel({ fetch: async () => respuesta(200, 'sesion-distinta') });
  await terminar();
  assert.deepEqual(p.destinos, ['/admin/acceso']);
});

test('el sondeo detecta una revocación remota; no manda peticiones desde pestañas ocultas', async () => {
  let cerrada = false;
  const p = panel({ fetch: async () => respuesta(cerrada ? 401 : 200) });
  await terminar();
  p.documento.visibilityState = 'hidden';
  p.intervalos[0]();
  assert.equal(p.peticiones.length, 1);
  cerrada = true;
  p.documento.visibilityState = 'visible';
  evento(p.documento, 'visibilitychange');
  await terminar();
  assert.deepEqual(p.destinos, ['/admin/acceso']);
});

test('las notificaciones entre pestañas fuerzan una comprobación del servidor', async () => {
  for (const via of ['canal', 'almacen']) {
    let cerrada = false;
    const p = panel({ fetch: async () => respuesta(cerrada ? 401 : 200) });
    await terminar();
    cerrada = true;
    if (via === 'canal') evento(p.canal, 'message');
    else evento(p.ventana, 'storage', { key: 'peluqueria-sesiones-cambiadas', newValue: 'aviso' });
    await terminar();
    assert.deepEqual(p.destinos, ['/admin/acceso']);
  }
});

test('un fallo de conexión oculta los datos; solo una validación válida los recupera', async () => {
  let disponible = true;
  const p = panel({ fetch: async () => { if (!disponible) throw new Error('Sin conexión'); return respuesta(); } });
  await terminar();
  disponible = false;
  p.intervalos[0]();
  await terminar();
  assert.equal(p.bloqueado(), true);
  disponible = true;
  evento(p.ventana, 'online');
  await terminar();
  assert.equal(p.bloqueado(), false);
});

test('un servidor que no responde deja el panel bloqueado al agotar el plazo', async () => {
  const p = panel({ fetch: (_url, opciones) => new Promise((_, rechazar) =>
    opciones.signal.addEventListener('abort', () => rechazar(new Error('Plazo agotado')))) });
  for (const plazo of p.plazos.values()) plazo();
  await terminar();
  assert.equal(p.bloqueado(), true);
  assert.equal(p.destinos.length, 0);
});

test('un 429 protege el panel y respeta Retry-After antes de volver a comprobar', async () => {
  let limitada = true;
  const p = panel({ fetch: async () => limitada ? respuesta(429, null, '60') : respuesta() });
  await terminar();
  assert.equal(p.bloqueado(), true);
  p.intervalos[0]();
  await terminar();
  assert.equal(p.peticiones.length, 1);
  limitada = false;
  p.avanzar(60000);
  p.intervalos[0]();
  await terminar();
  assert.equal(p.bloqueado(), false);
});

test('la pantalla de acceso anuncia cierres confirmados sin guardar identificadores ni tokens', async () => {
  const p = panel({ id: null, avisar: true });
  await terminar();
  assert.equal(p.peticiones.length, 0);
  assert.deepEqual(p.mensajes, ['comprobar']);
  assert.equal(p.almacen.length, 1);
  assert.match(p.almacen[0][1], /^\d+-[\d.]+$/);
});
