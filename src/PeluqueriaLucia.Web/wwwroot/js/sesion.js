// El servidor decide si la sesión es válida; esta capa retira también el panel ya abierto.
'use strict';

(() => {
  const cuerpo = document.body;
  const { sesionId, sesionEstado, sesionAcceso } = cuerpo.dataset;
  const claveAviso = 'peluqueria-sesiones-cambiadas';
  let canal;
  try { canal = new BroadcastChannel(claveAviso); } catch { /* El sondeo sigue funcionando. */ }

  let saliendo = false;
  let enCurso = false;
  let pendiente = false;
  let aviso;
  let proximaComprobacion = 0;
  let cargando = false;
  // Mientras se comprueba la sesión se enseña el indicador de carga (cargando.js), no el texto; el texto queda
  // para cuando la comprobación falla de verdad (sin conexión, servidor caído).
  const iniciarCarga = () => {
    if (cargando || !window.Cargando) return;
    cargando = true;
    cuerpo.classList.add('sesion-cargando');
    window.Cargando.iniciar();
  };
  const terminarCarga = () => {
    if (!cargando) return;
    cargando = false;
    cuerpo.classList.remove('sesion-cargando');
    window.Cargando.terminar();
  };
  const bloquear = () => {
    cuerpo.classList.add('sesion-en-comprobacion');
    if (!aviso) {
      aviso = document.createElement('div');
      aviso.className = 'sesion-aviso';
      aviso.setAttribute('role', 'status');
      aviso.textContent = 'Comprobando tu sesión. Si se interrumpe la conexión, el panel permanecerá protegido.';
      cuerpo.append(aviso);
    }
    iniciarCarga();
  };

  const expulsar = () => {
    if (saliendo) return;
    saliendo = true;
    bloquear();
    // replace evita dejar esta pantalla privada como entrada nueva del historial.
    window.location.replace(sesionAcceso);
  };

  const comprobar = async (ocultar = false) => {
    if (!sesionId || saliendo) return;
    if (ocultar) bloquear();
    if (enCurso) { pendiente = true; return; }
    if (Date.now() < proximaComprobacion) return;
    enCurso = true;
    const cancelacion = new AbortController();
    const plazo = setTimeout(() => cancelacion.abort(), 5000);
    try {
      const respuesta = await fetch(sesionEstado, {
        method: 'GET', credentials: 'same-origin', cache: 'no-store', redirect: 'error',
        headers: { Accept: 'application/json' }, signal: cancelacion.signal, silencioso: true,
      });
      if (respuesta.status === 401 || respuesta.status === 403) { expulsar(); return; }
      if (!respuesta.ok) {
        if (respuesta.status === 429) {
          const espera = Number(respuesta.headers.get('Retry-After'));
          proximaComprobacion = Date.now() + (Number.isFinite(espera) && espera > 0 ? espera : 60) * 1000;
        }
        throw new Error('No se pudo validar la sesión');
      }
      const estado = await respuesta.json();
      // Otra entrada en el mismo navegador puede haber sustituido la cookie de esta pestaña.
      if (estado.sesionId !== sesionId) { expulsar(); return; }
      if (!pendiente && !saliendo) { cuerpo.classList.remove('sesion-en-comprobacion'); terminarCarga(); }
    } catch {
      // Sin una respuesta válida no se muestran ni se pueden usar los datos ya cargados: queda el texto, no el indicador.
      bloquear();
      terminarCarga();
    } finally {
      clearTimeout(plazo);
      enCurso = false;
      if (pendiente) { pendiente = false; void comprobar(true); }
    }
  };

  if (sesionId) {
    window.SesionAdministrador = { expulsar };
    canal?.addEventListener('message', () => void comprobar(true));
    window.addEventListener('storage', (e) => {
      if (e.key === claveAviso && e.newValue) void comprobar(true);
    });
    window.addEventListener('focus', () => void comprobar(true));
    window.addEventListener('online', () => void comprobar(true));
    window.addEventListener('offline', bloquear);
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') void comprobar(true);
    });
    // Oculta la copia guardada en memoria antes de restaurarla con Atrás/Adelante.
    window.addEventListener('pagehide', bloquear);
    window.addEventListener('pageshow', (e) => { if (e.persisted) void comprobar(true); });
    setInterval(() => { if (document.visibilityState === 'visible') void comprobar(); }, 15000);
    void comprobar(true);
  }

  if (document.querySelector('[data-sesiones-cambiadas]')) {
    // Se emite solo después de que el servidor confirme el cierre o el cambio de contraseña.
    canal?.postMessage('comprobar');
    try { localStorage.setItem(claveAviso, `${Date.now()}-${Math.random()}`); } catch { /* Sin almacenamiento: sondeo. */ }
  }
})();
