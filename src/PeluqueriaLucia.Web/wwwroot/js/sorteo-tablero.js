// Tablero del sorteo: buscar un número o un nombre, enseñar solo los libres o los ocupados, ver quién tiene cada número
// (y apuntar uno libre) y ponerlo a pantalla completa para enseñarlo en el salón. Sin JavaScript el tablero se ve igual,
// con el número y el nombre corto en cada casilla.
(() => {
  const tablero = document.querySelector('[data-tablero-celdas]');
  const pantalla = document.querySelector('[data-tablero]');
  const celdas = tablero ? [...tablero.querySelectorAll('.tablero-celda')] : [];

  /** Sin tildes ni mayúsculas, para buscar «maria» y encontrar «María». */
  const normalizar = (texto) => (texto ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim();

  // ── Filtro (todos, libres, ocupados) y búsqueda ──
  const buscar = document.querySelector('[data-tablero-buscar]');
  const resultado = document.querySelector('[data-tablero-resultado]');
  const filtrar = () => {
    if (!tablero) return;
    const filtro = document.querySelector('[data-tablero-filtro]:checked')?.value ?? 'todos';
    tablero.dataset.filtro = filtro;
    const consulta = normalizar(buscar?.value);
    const esNumero = /^\d+$/.test(consulta);
    let coincidencias = 0;
    let primera = null;
    for (const celda of celdas) {
      const coincide =
        consulta.length > 0 &&
        (esNumero ? celda.dataset.numero === consulta : normalizar(celda.dataset.nombre).includes(consulta));
      celda.classList.toggle('is-coincide', coincide);
      if (coincide) {
        coincidencias += 1;
        primera ??= celda;
      }
    }
    tablero.classList.toggle('is-buscando', consulta.length > 0);
    if (resultado) {
      resultado.textContent =
        consulta.length === 0
          ? ''
          : coincidencias === 0
            ? 'Ningún número coincide.'
            : coincidencias === 1
              ? '1 número coincide.'
              : `${coincidencias} números coinciden.`;
    }
    primera?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
  };
  buscar?.addEventListener('input', filtrar);
  document.querySelectorAll('[data-tablero-filtro]').forEach((radio) => radio.addEventListener('change', filtrar));

  // ── Quién tiene cada número ──
  const dialogo = document.querySelector('[data-tablero-dialogo]');
  const textos = { 'is-libre': 'Libre', 'is-ocupado': 'Ocupado', 'is-ganador': '¡Ganador!', 'is-gano-antes': 'Salió en un sorteo anterior' };
  const abrir = (celda) => {
    if (!dialogo?.showModal) return;
    const libre = celda.dataset.estado === 'is-libre';
    const numero = celda.dataset.numero;
    dialogo.querySelector('[data-dialogo-numero]').textContent = numero;
    dialogo.querySelector('[data-dialogo-numero]').className = `tablero-bola ${celda.dataset.estado}`;
    dialogo.querySelector('[data-dialogo-titulo]').textContent = libre
      ? `El número ${numero} está libre`
      : celda.dataset.nombre || `Número ${numero} (sin nombre)`;
    dialogo.querySelector('[data-dialogo-estado]').textContent = libre ? 'Nadie lo tiene todavía.' : `Número ${numero} · ${textos[celda.dataset.estado] ?? ''}`;
    const contacto = dialogo.querySelector('[data-dialogo-contacto]');
    contacto.textContent = celda.dataset.contacto ?? '';
    contacto.hidden = libre || !celda.dataset.contacto;
    const ficha = dialogo.querySelector('[data-dialogo-ficha]');
    if (celda.dataset.ficha) ficha.href = celda.dataset.ficha;
    ficha.hidden = !celda.dataset.ficha;
    dialogo.querySelector('[data-dialogo-ocupado]').hidden = libre;
    const apuntar = dialogo.querySelector('[data-dialogo-libre]');
    if (apuntar) apuntar.hidden = !libre;
    const campo = dialogo.querySelector('[data-dialogo-campo-numero]');
    if (campo) campo.value = numero;
    dialogo.showModal();
    if (libre) dialogo.querySelector('#nombre-apuntar')?.focus();
  };
  celdas.forEach((celda) => celda.addEventListener('click', () => abrir(celda)));
  // Un clic fuera de la hoja la cierra
  dialogo?.addEventListener('click', (evento) => {
    if (evento.target === dialogo) dialogo.close();
  });

  // Al volver de apuntar un número (#n-12), se ve resaltado
  const destino = location.hash.startsWith('#n-') ? document.getElementById(location.hash.slice(1)) : null;
  if (destino) {
    destino.classList.add('is-recien');
    destino.scrollIntoView({ block: 'center' });
  }

  // ── Pantalla completa, para enseñarlo en el salón ──
  const boton = document.querySelector('[data-tablero-pantalla]');
  if (boton && pantalla?.requestFullscreen && document.fullscreenEnabled) {
    boton.hidden = false;
    const texto = boton.querySelector('span');
    boton.addEventListener('click', () => {
      if (document.fullscreenElement) document.exitFullscreen();
      else pantalla.requestFullscreen().catch(() => {});
    });
    document.querySelector('[data-tablero-salir]')?.addEventListener('click', () => document.exitFullscreen?.());
    document.addEventListener('fullscreenchange', () => {
      const completa = document.fullscreenElement === pantalla;
      pantalla.classList.toggle('is-completa', completa);
      if (texto) texto.textContent = completa ? 'Salir de pantalla completa' : 'Pantalla completa';
    });
  }
})();
