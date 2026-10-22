// Nueva cita del salón:
// · la ficha del cliente se elige mientras se escribe, sin recargar la página (combo-clientes.js);
// · al elegir el servicio se rellena su duración;
// · la hora solo se elige entre las libres: al cambiar día, servicio o duración se piden de nuevo al servidor.
// Sin JavaScript sigue funcionando el formulario de búsqueda con su botón y el selector de horas del día cargado.
'use strict';

(() => {
  const pagina = document.querySelector('[data-nueva-cita]');
  if (!pagina) return;

  // ── Cliente ──────────────────────────────────────────────────────────────
  const raiz = pagina.querySelector('[data-combo]');
  if (raiz && window.ComboClientes) {
    const clienteId = pagina.querySelector('[data-cliente-id]');
    const elegido = pagina.querySelector('[data-cliente-elegido]');
    const sinFicha = pagina.querySelector('[data-campos-sin-ficha]');
    const resultadosServidor = pagina.querySelector('[data-resultados-servidor]');
    const aviso = pagina.querySelector('[data-aviso-cita]');

    // Aviso al cliente: con canal preferido en la ficha va siempre por ese (no se elige); sin él, se elige y solo entre los
    // canales posibles (correo con email, WhatsApp con teléfono). Sin ficha elegida, todo vuelve a poder elegirse.
    const ajustarAviso = (c) => {
      if (!aviso) return;
      const fijo = aviso.querySelector('[data-aviso-fijo]');
      const elegir = aviso.querySelector('[data-aviso-elegir]');
      const ayuda = aviso.querySelector('[data-aviso-ayuda]');
      const preferido = c && c.canal !== null && c.canal !== undefined ? c.canal : null;
      fijo.hidden = preferido === null;
      elegir.hidden = preferido !== null;
      if (preferido !== null) {
        aviso.querySelector('[data-aviso-fijo-canal]').textContent = preferido === 1 ? 'por WhatsApp' : 'por correo';
        ayuda.textContent = 'El aviso de confirmación queda en Comunicaciones para enviarlo.';
        return;
      }
      const correo = aviso.querySelector('[data-aviso-correo]');
      const whatsApp = aviso.querySelector('[data-aviso-whatsapp]');
      correo.disabled = Boolean(c) && !c.conCorreo;
      whatsApp.disabled = Boolean(c) && !c.conTelefono;
      [correo, whatsApp].forEach((radio) => {
        if (radio.disabled && radio.checked) aviso.querySelector('input[name="Aviso"][value=""]').checked = true;
      });
      ayuda.textContent = c && (!c.conCorreo || !c.conTelefono)
        ? `Sin preferencia en la ficha. ${!c.conCorreo ? 'No tiene email: no se le puede avisar por correo.' : ''} ${!c.conTelefono ? 'No tiene teléfono: no se le puede avisar por WhatsApp.' : ''}`.trim()
        : 'Sin preferencia en la ficha: elige si avisarle y por dónde. El correo necesita su email; WhatsApp, un móvil.';
    };

    const elegir = (c) => {
      clienteId.value = c.id;
      elegido.querySelector('[data-cliente-nombre]').textContent = c.nombre;
      elegido.querySelector('[data-cliente-detalle]').textContent = c.detalle ?? '';
      const avatar = elegido.querySelector('[data-cliente-avatar]');
      avatar.className = `avatar ${window.ComboClientes.tinte(c)}`;
      avatar.textContent = c.iniciales;
      elegido.hidden = false;
      raiz.hidden = true;
      if (resultadosServidor) resultadosServidor.hidden = true;
      ajustarAviso(c);
      // Con ficha elegida, el nombre y el teléfono libres no se usan
      if (sinFicha) {
        sinFicha.hidden = true;
        sinFicha.querySelectorAll('input').forEach((campo) => {
          campo.value = '';
        });
      }
    };

    const combo = window.ComboClientes.montar(raiz, { alElegir: elegir });
    elegido?.querySelector('[data-cliente-cambiar]')?.addEventListener('click', () => {
      clienteId.value = '';
      elegido.hidden = true;
      raiz.hidden = false;
      if (sinFicha) sinFicha.hidden = false;
      ajustarAviso(null);
      combo?.limpiar();
      combo?.enfocar();
    });
  }

  // ── Servicio, duración y horas libres ───────────────────────────────────
  const horas = pagina.querySelector('[data-horas]');
  const dia = pagina.querySelector('#Dia');
  const minutos = pagina.querySelector('#Minutos');
  const servicio = pagina.querySelector('#ServicioId');
  if (!horas || !dia || !minutos) return;

  const grupos = horas.querySelector('[data-horas-grupos]');
  const resumen = horas.querySelector('[data-horas-resumen]');
  const vacio = horas.querySelector('[data-horas-vacio]');
  // «martes 20 de octubre», igual que lo escribe el servidor al cargar la página
  const diaSemana = new Intl.DateTimeFormat('es-ES', { weekday: 'long' });
  const nombreMes = new Intl.DateTimeFormat('es-ES', { month: 'long' });
  const fecha = (f) => `${diaSemana.format(f)} ${f.getDate()} de ${nombreMes.format(f)}`;
  let peticion = null;
  let espera = null;

  const minutosDelServicio = () => Number(servicio?.selectedOptions[0]?.dataset.minutos) || 0;
  const duracion = () => {
    const escrita = Number(minutos.value);
    return minutos.value !== '' && Number.isInteger(escrita) && escrita >= 5 && escrita <= 600 ? escrita : minutosDelServicio() || 30;
  };
  const elegida = () => horas.querySelector('input[name="Hora"]:checked')?.value ?? null;

  function chip(hora, libre, marcada) {
    if (!libre) {
      const ocupada = document.createElement('span');
      ocupada.className = 'hora-chip is-ocupada';
      ocupada.title = 'Ocupada';
      const texto = document.createElement('span');
      texto.textContent = hora;
      const lector = document.createElement('span');
      lector.className = 'sr-only';
      lector.textContent = ', ocupada';
      ocupada.append(texto, lector);
      return ocupada;
    }
    const etiqueta = document.createElement('label');
    etiqueta.className = 'hora-chip';
    const radio = document.createElement('input');
    radio.type = 'radio';
    radio.name = 'Hora';
    radio.value = hora;
    radio.checked = marcada;
    const texto = document.createElement('span');
    texto.textContent = hora;
    etiqueta.append(radio, texto);
    return etiqueta;
  }

  function pintar(datos, anterior) {
    const libres = new Set(datos.horas);
    const todas = [...new Set([...datos.horas, ...datos.ocupadas])].sort();
    grupos.replaceChildren();
    for (const [titulo, lista] of [
      ['Mañana', todas.filter((h) => h < '14:00')],
      ['Tarde', todas.filter((h) => h >= '14:00')],
    ]) {
      if (lista.length === 0) continue;
      const grupo = document.createElement('div');
      grupo.className = 'horas-grupo';
      const cabecera = document.createElement('p');
      cabecera.className = 'horas-titulo';
      cabecera.textContent = titulo;
      const fila = document.createElement('div');
      fila.className = 'horas-lista';
      lista.forEach((h) => fila.append(chip(h, libres.has(h), h === anterior)));
      grupo.append(cabecera, fila);
      grupos.append(grupo);
    }
    const [anio, mes, d] = datos.dia.split('-').map(Number);
    let texto = `Horas libres para ${datos.minutos} min el ${fecha(new Date(anio, mes - 1, d))}.`;
    // La hora que estaba elegida ya no cabe: se avisa en lugar de cambiarla en silencio
    if (anterior && !libres.has(anterior)) texto += ` Las ${anterior} ya no están libres con esta duración: elige otra hora.`;
    resumen.textContent = texto;
    vacio.hidden = datos.horas.length > 0;
  }

  async function refrescar() {
    clearTimeout(espera);
    if (!dia.value) return;
    const anterior = elegida();
    peticion?.abort();
    const propia = new AbortController();
    peticion = propia;
    horas.setAttribute('aria-busy', 'true');
    horas.classList.add('is-cargando');
    try {
      // Con un servicio con dualidad, las citas ya dadas no quitan horas: el servidor necesita saber cuál es
      const delServicio = servicio?.value ? `&servicioId=${encodeURIComponent(servicio.value)}` : '';
      const url = `${horas.dataset.urlHuecos}?dia=${encodeURIComponent(dia.value)}&minutos=${duracion()}${delServicio}`;
      const respuesta = await fetch(url, { signal: propia.signal, headers: { Accept: 'application/json' }, silencioso: true });
      if (respuesta.status === 401 || respuesta.status === 403) {
        window.SesionAdministrador?.expulsar();
        return;
      }
      if (!respuesta.ok) throw new Error(respuesta.statusText);
      pintar(await respuesta.json(), anterior);
    } catch (error) {
      if (error.name === 'AbortError') return;
      resumen.textContent = 'No se han podido cargar las horas libres. Comprueba la conexión y vuelve a elegir el día.';
    } finally {
      // Una petición sustituida por otra no quita el estado de carga de la nueva
      if (peticion === propia) {
        horas.removeAttribute('aria-busy');
        horas.classList.remove('is-cargando');
      }
    }
  }

  // Al elegir el servicio, su duración pasa al campo (si no la tiene, se deja vacío y vale media hora)
  servicio?.addEventListener('change', () => {
    const propios = minutosDelServicio();
    minutos.value = propios > 0 ? String(propios) : '';
    const aviso = pagina.querySelector('[data-dualidad-aviso]');
    if (aviso) aviso.hidden = servicio.selectedOptions[0]?.dataset.dualidad !== 'true';
    refrescar();
  });
  // La duración escrita a mano refresca las horas al dejar de teclear
  minutos.addEventListener('input', () => {
    clearTimeout(espera);
    espera = setTimeout(refrescar, 350);
  });
  dia.addEventListener('change', refrescar);
})();
