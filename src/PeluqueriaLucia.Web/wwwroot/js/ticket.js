// Impresión del ticket en la térmica del salón por WebUSB: el servidor construye el ticket en ESC/POS
// (TicketTermico) y el navegador lo manda directamente a la impresora USB, sin diálogo de impresión.
// Funciona en Chrome y Edge, con la web en HTTPS (o localhost) y la impresora con driver WinUSB (Zadig).
'use strict';

(() => {
  const boton = document.querySelector('[data-imprimir-termica]');
  const estado = document.querySelector('[data-impresion-estado]');
  if (!boton) return;

  const vendorId = Number(boton.dataset.vid);
  const productId = Number(boton.dataset.pid);
  const TROZO = 4096;

  const avisar = (texto, error = false) => {
    if (!estado) return;
    estado.textContent = texto;
    estado.hidden = !texto;
    estado.classList.toggle('es-error', error);
  };

  /** La impresora ya autorizada en este navegador o, si no la hay, la que elija el usuario en el diálogo. */
  async function elegirImpresora() {
    const conocidas = await navigator.usb.getDevices();
    const conocida = conocidas.find((d) => d.vendorId === vendorId && d.productId === productId);
    if (conocida) return conocida;
    return navigator.usb.requestDevice({ filters: [{ vendorId, productId }] });
  }

  /** Interfaz y endpoint de salida (bulk OUT) por donde la impresora recibe los datos. */
  function salida(dispositivo) {
    for (const interfaz of dispositivo.configuration?.interfaces ?? []) {
      for (const alternativa of interfaz.alternates) {
        const endpoint = alternativa.endpoints.find((e) => e.direction === 'out' && e.type === 'bulk');
        if (endpoint) return { interfaz, alternativa, endpoint };
      }
    }
    return null;
  }

  async function enviar(dispositivo, bytes) {
    await dispositivo.open();
    try {
      if (!dispositivo.configuration) await dispositivo.selectConfiguration(1);
      const via = salida(dispositivo);
      if (!via) throw new Error('La impresora no tiene ninguna salida de datos USB utilizable.');
      await dispositivo.claimInterface(via.interfaz.interfaceNumber);
      try {
        if (via.alternativa.alternateSetting !== 0)
          await dispositivo.selectAlternateInterface(via.interfaz.interfaceNumber, via.alternativa.alternateSetting);
        for (let i = 0; i < bytes.length; i += TROZO) {
          const resultado = await dispositivo.transferOut(via.endpoint.endpointNumber, bytes.subarray(i, i + TROZO));
          if (resultado.status !== 'ok') throw new Error(`La impresora no aceptó los datos (${resultado.status}).`);
        }
      } finally {
        await dispositivo.releaseInterface(via.interfaz.interfaceNumber).catch(() => {});
      }
    } finally {
      await dispositivo.close().catch(() => {});
    }
  }

  function explicar(error) {
    switch (error?.name) {
      case 'NotFoundError':
        return 'No se ha elegido ninguna impresora. Comprueba que está encendida y conectada por USB.';
      case 'SecurityError':
        return 'El navegador no deja acceder a la impresora. Revisa que la web esté en HTTPS y que la impresora use el driver WinUSB.';
      case 'NetworkError':
        return 'No se pudo abrir la impresora. Si otro programa la está usando, ciérralo y vuelve a intentarlo.';
      default:
        return error?.message || 'No se pudo imprimir el ticket.';
    }
  }

  boton.addEventListener('click', async () => {
    if (!navigator.usb) {
      avisar('Este navegador no puede enviar el ticket a la impresora térmica: usa Chrome o Edge. Se abre la impresión normal.', true);
      window.print();
      return;
    }

    boton.disabled = true;
    avisar('Imprimiendo…');
    try {
      // Primero la impresora: el permiso del navegador solo se puede pedir justo después del clic
      const dispositivo = await elegirImpresora();
      const respuesta = await fetch(boton.dataset.url, { credentials: 'same-origin', headers: { Accept: 'application/octet-stream' } });
      if (!respuesta.ok) throw new Error('No se pudo preparar el ticket. Vuelve a cargar la página.');
      await enviar(dispositivo, new Uint8Array(await respuesta.arrayBuffer()));
      avisar('Ticket enviado a la impresora.');
    } catch (error) {
      avisar(explicar(error), true);
    } finally {
      boton.disabled = false;
    }
  });
})();
