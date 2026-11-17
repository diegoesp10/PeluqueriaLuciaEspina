// Cita desde la web pública (/citas) sin recargas que borren lo escrito:
// · Cambiar de día o de mes, de cuidado o de fecha trae la página nueva por detrás (fetch) y cambia solo el calendario
//   ([data-reserva-calendario]), la fecha y las horas ([data-reserva-huecos]); el formulario se queda como está. La
//   dirección se actualiza, así que recargar enseña lo mismo. Sin JavaScript, o si la petición falla, se navega como siempre.
// · Las horas se ponen al día solas cada minuto y al volver a la pestaña; si la hora elegida se ocupa, se avisa.
// · Lo escrito (móvil, correo, nombre, apellidos, canal, para quién, el día, el cuidado y la hora elegida) se guarda en
//   sessionStorage: es solo de esta pestaña y desaparece al cerrarla. Si la página se recarga, o se vuelve a /citas desde
//   el menú, vuelve. Se borra al enviar la solicitud (/citas/recibida, [data-cita-recibida]). El consentimiento no se
//   guarda: se marca cada vez.
// · Simulador ([data-simulador]): con un cuidado elegido, a qué hora acabaría la cita y, si pasa del cierre del tramo,
//   cuánto (las horas «alargadas», que el salón acepta o no).
'use strict';

(() => {
  const CLAVE = 'lucia-reserva-cita';
  const almacen = {
    leer() {
      try {
        return JSON.parse(sessionStorage.getItem(CLAVE) || 'null') ?? {};
      } catch {
        return {};
      }
    },
    escribir(datos) {
      try {
        sessionStorage.setItem(CLAVE, JSON.stringify(datos));
      } catch {
        // Sin almacenamiento (modo privado estricto…) todo funciona igual; solo no se recuerda al recargar
      }
    },
    borrar() {
      try {
        sessionStorage.removeItem(CLAVE);
      } catch {
        // Nada que borrar
      }
    },
  };

  if (document.querySelector('[data-cita-recibida]')) {
    almacen.borrar();
    return;
  }

  const reserva = document.querySelector('form.agenda-formulario');
  const busqueda = document.querySelector('form.agenda-servicio');
  if (!reserva || !busqueda) return;

  const CAMPOS = ['Formulario.Telefono', 'Formulario.Correo', 'Formulario.Nombre', 'Formulario.Apellido'];
  const campo = (nombre) => reserva.elements.namedItem(nombre);
  const valor = (nombre) => campo(nombre)?.value ?? '';
  const huecos = () => reserva.querySelector('[data-reserva-huecos]');
  const calendario = () => document.querySelector('[data-reserva-calendario]');
  const horas = () => [...(huecos()?.querySelectorAll('input[name="Formulario.Hora"]') ?? [])];
  const horaElegida = () => horas().find((radio) => radio.checked) ?? null;
  const para = () => busqueda.querySelector('input[name="para"]:checked')?.value ?? '';

  const elegirHora = (hora) => {
    const radio = horas().find((r) => r.value === hora);
    if (radio) radio.checked = true;
    return Boolean(radio);
  };

  // ── Lo escrito se guarda en esta pestaña ──
  const guardar = () => {
    const datos = { consulta: location.search, dia: valor('Formulario.Dia'), hora: horaElegida()?.value ?? '', canal: valor('Formulario.Canal'), para: para() };
    CAMPOS.forEach((nombre) => (datos[nombre] = valor(nombre)));
    almacen.escribir(datos);
  };

  const restaurar = () => {
    const datos = almacen.leer();
    CAMPOS.forEach((nombre) => {
      const control = campo(nombre);
      // Lo que trae la página (al volver con algún error) manda sobre lo guardado
      if (control && !control.value && typeof datos[nombre] === 'string') control.value = datos[nombre];
    });
    const canal = campo('Formulario.Canal');
    if (canal instanceof RadioNodeList && !canal.value && datos.canal) {
      const radio = [...canal].find((r) => r.value === datos.canal && !r.disabled);
      if (radio) radio.checked = true;
    }
    if (!para() && datos.para) {
      // Con change, agenda-publica.js filtra la lista y rellena el sexo de la reserva como si se hubiera pulsado
      const radio = busqueda.querySelector(`input[name="para"][value="${CSS.escape(datos.para)}"]`);
      if (radio) {
        radio.checked = true;
        radio.dispatchEvent(new Event('change', { bubbles: true }));
      }
    }
    if (!horaElegida() && datos.hora && datos.dia === valor('Formulario.Dia')) elegirHora(datos.hora);
    // Volver a /citas sin nada elegido (desde el menú) trae el día y el cuidado de antes. No al volver con errores del envío:
    // entonces manda lo que trae la página
    const consulta = typeof datos.consulta === 'string' && datos.consulta.startsWith('?') ? datos.consulta : '';
    if (consulta && !location.search && !reserva.querySelector('.validation-summary-errors')) {
      actualizar(location.pathname + consulta, { busqueda: true, recuperar: { dia: datos.dia, hora: datos.hora } });
    }
  };

  // Lo elegido arriba (cuidado, texto libre y para quién) como lo trae la página nueva, sin volver a pedir la disponibilidad
  let sincronizando = false;
  const sincronizarBusqueda = (pagina) => {
    const select = busqueda.querySelector('#servicio-agenda');
    const selectNuevo = pagina.querySelector('#servicio-agenda');
    const otro = busqueda.querySelector('#otro-agenda');
    const otroNuevo = pagina.querySelector('#otro-agenda');
    if (otro && otroNuevo) otro.value = otroNuevo.value;
    sincronizando = true;
    try {
      if (select && selectNuevo && select.value !== selectNuevo.value) {
        select.value = selectNuevo.value;
        // select-buscable.js enseña la elección nueva; el envío que pide agenda-publica.js se descarta (sincronizando)
        select.dispatchEvent(new Event('change', { bubbles: true }));
      }
      const paraNuevo = pagina.querySelector('form.agenda-servicio input[name="para"]:checked')?.value;
      const radio = paraNuevo && busqueda.querySelector(`input[name="para"][value="${CSS.escape(paraNuevo)}"]`);
      if (radio && !radio.checked) {
        radio.checked = true;
        radio.dispatchEvent(new Event('change', { bubbles: true }));
      }
    } finally {
      sincronizando = false;
    }
  };

  // ── Simulador: a qué hora acabaría ──
  const minutos = (hora) => {
    const [h, m] = hora.split(':').map(Number);
    return h * 60 + m;
  };
  const simular = () => {
    const bloque = huecos();
    const salida = bloque?.querySelector('[data-simulador]');
    if (!salida) return;
    const radio = horaElegida();
    if (!radio || bloque.dataset.conServicio !== 'true' || !radio.dataset.fin) {
      salida.hidden = true;
      salida.textContent = '';
      return;
    }
    const { fin, cierre } = radio.dataset;
    const plantilla = (cierre ? bloque.dataset.textoAlargue : bloque.dataset.texto) ?? '';
    const valores = [radio.value, fin, cierre ? String(minutos(fin) - minutos(cierre)) : '', cierre ?? ''];
    salida.textContent = plantilla.replace(/\{(\d)\}/g, (_, i) => valores[Number(i)] ?? '');
    salida.hidden = false;
  };

  const avisarHoraPerdida = () => {
    const bloque = huecos();
    if (!bloque?.dataset.textoPerdida) return;
    const nota = document.createElement('p');
    nota.className = 'agenda-hora-perdida';
    nota.setAttribute('role', 'alert');
    nota.textContent = bloque.dataset.textoPerdida;
    bloque.prepend(nota);
  };

  // ── Cambiar de día, mes o cuidado sin recargar ──
  let peticion = null;

  // silencioso: la puesta al día de cada minuto (sin atenuar nada y, si falla, sin navegar). busqueda: traer también lo
  // elegido arriba (al volver a /citas). recuperar: { dia, hora } guardados, la hora se vuelve a elegir si sigue libre.
  async function actualizar(url, { silencioso = false, busqueda: conBusqueda = false, recuperar = null } = {}) {
    peticion?.abort();
    const control = new AbortController();
    peticion = control;
    const destino = new URL(url, location.href);
    const diaAntes = valor('Formulario.Dia');
    const horaAntes = horaElegida()?.value ?? '';
    const ocupados = [calendario(), huecos()].filter(Boolean);
    if (!silencioso) ocupados.forEach((bloque) => bloque.setAttribute('aria-busy', 'true'));
    try {
      // Sin la barra de carga de página (cargando.js): lo único que cambia se atenúa mientras llega (aria-busy)
      const respuesta = await fetch(destino, { signal: control.signal, headers: { Accept: 'text/html' }, credentials: 'same-origin', silencioso: true });
      if (!respuesta.ok) throw new Error(`HTTP ${respuesta.status}`);
      const pagina = new DOMParser().parseFromString(await respuesta.text(), 'text/html');
      const nuevoCalendario = pagina.querySelector('[data-reserva-calendario]');
      const nuevasHoras = pagina.querySelector('[data-reserva-huecos]');
      if (!nuevoCalendario || !nuevasHoras || !calendario() || !huecos()) throw new Error('La página no trae el calendario');
      if (control.signal.aborted) return;

      // El foco sigue en el mismo día o la misma flecha del mes
      const enfocado = calendario().contains(document.activeElement) ? document.activeElement.getAttribute('aria-label') : null;
      calendario().replaceWith(nuevoCalendario);
      huecos().replaceWith(nuevasHoras);
      const fecha = pagina.querySelector('[data-reserva-fecha]');
      const fechaActual = reserva.querySelector('[data-reserva-fecha]');
      if (fecha && fechaActual) fechaActual.textContent = fecha.textContent;
      ['Formulario.Dia', 'Formulario.ServicioId', 'Formulario.ServicioOtro'].forEach((nombre) => {
        const nuevo = pagina.querySelector(`form.agenda-formulario [name="${nombre}"]`);
        const actual = campo(nombre);
        if (nuevo && actual) actual.value = nuevo.value;
      });
      const diaNuevo = pagina.querySelector('#dia-agenda');
      const diaActual = busqueda.querySelector('#dia-agenda');
      if (diaNuevo && diaActual && diaActual.value !== diaNuevo.value) {
        diaActual.value = diaNuevo.value;
        // fechas.js rotula su botón con change; sin volver a pedir la disponibilidad
        sincronizando = true;
        try {
          diaActual.dispatchEvent(new Event('change', { bubbles: true }));
        } finally {
          sincronizando = false;
        }
      }
      const enviar = reserva.querySelector('[data-reserva-enviar]');
      const enviarNuevo = pagina.querySelector('[data-reserva-enviar]');
      if (enviar && enviarNuevo) enviar.disabled = enviarNuevo.disabled;
      if (conBusqueda) sincronizarBusqueda(pagina);

      // El mismo día conserva la hora elegida si sigue libre; si se ha ocupado, se dice
      if (horaAntes && valor('Formulario.Dia') === diaAntes && !elegirHora(horaAntes)) avisarHoraPerdida();
      else if (recuperar?.hora && recuperar.dia === valor('Formulario.Dia') && !horaElegida()) elegirHora(recuperar.hora);
      history.replaceState(history.state, '', destino.pathname + destino.search);
      simular();
      guardar();
      document.dispatchEvent(new CustomEvent('agenda-actualizada'));
      if (enfocado) {
        const nuevo = [...nuevoCalendario.querySelectorAll('a[aria-label]')].find((a) => a.getAttribute('aria-label') === enfocado)
          ?? nuevoCalendario.querySelector('[aria-current="date"]');
        nuevo?.focus({ preventScroll: true });
      }
    } catch (error) {
      if (error?.name === 'AbortError' || control.signal.aborted) return;
      // Si algo falla se navega como siempre: lo escrito vuelve desde sessionStorage
      if (!silencioso) {
        guardar();
        location.assign(destino);
      }
    } finally {
      if (peticion === control) peticion = null;
      ocupados.forEach((bloque) => bloque.removeAttribute('aria-busy'));
    }
  }

  // Días y flechas del mes. En captura: antes que agenda-navegacion.js y que el indicador de carga de página
  document.addEventListener(
    'click',
    (evento) => {
      const enlace = evento.target.closest?.('[data-reserva-calendario] a[href]');
      if (!enlace || evento.defaultPrevented || evento.button !== 0 || evento.ctrlKey || evento.metaKey || evento.shiftKey || evento.altKey) return;
      evento.preventDefault();
      actualizar(enlace.href);
    },
    true,
  );

  // Cuidado y fecha: el formulario GET de siempre (agenda-publica.js lo envía al elegir un cuidado), sin recargar
  document.addEventListener(
    'submit',
    (evento) => {
      if (evento.target !== busqueda || evento.defaultPrevented) return;
      evento.preventDefault();
      if (sincronizando) return;
      const destino = new URL(busqueda.action, location.href);
      destino.search = new URLSearchParams(new FormData(busqueda)).toString();
      actualizar(destino);
    },
    true,
  );
  busqueda.querySelector('#dia-agenda')?.addEventListener('change', () => {
    if (!sincronizando) busqueda.requestSubmit();
  });
  // Todo se actualiza solo: el botón «Ver disponibilidad» queda para quien navega sin JavaScript
  busqueda.querySelector('[data-reserva-ver]')?.setAttribute('hidden', '');

  // Las horas, al día: cada minuto con la pestaña a la vista y al volver a ella. No mientras se está eligiendo una
  const refrescar = () => {
    if (document.hidden || peticion) return;
    if (huecos()?.contains(document.activeElement) || calendario()?.contains(document.activeElement)) return;
    actualizar(location.href, { silencioso: true });
  };
  setInterval(refrescar, 60000);
  document.addEventListener('visibilitychange', () => {
    if (!document.hidden) refrescar();
  });

  reserva.addEventListener('input', guardar);
  reserva.addEventListener('change', (evento) => {
    if (evento.target.name === 'Formulario.Hora') huecos()?.querySelector('.agenda-hora-perdida')?.remove();
    simular();
    guardar();
  });
  busqueda.addEventListener('change', guardar);
  // Lo que rellena el autocompletado del contacto (sin eventos de teclado) se guarda al salir de la página
  window.addEventListener('pagehide', guardar);

  restaurar();
  simular();
})();
