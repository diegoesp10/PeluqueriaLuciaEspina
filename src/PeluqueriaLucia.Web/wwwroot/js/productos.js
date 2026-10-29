// Productos: ficha (galería y movimientos de stock) y formulario (lector de códigos, margen y fotos con vista previa).
'use strict';

(() => {
  const { leerImporte, euros } = window.Peluqueria;

  // ── Galería de la ficha: la miniatura pulsada pasa a la foto grande ──
  const galeria = document.querySelector('[data-galeria]');
  const grande = galeria?.querySelector('[data-galeria-grande]');
  galeria?.querySelectorAll('[data-galeria-miniatura]').forEach((enlace) => {
    enlace.addEventListener('click', (evento) => {
      if (!grande || evento.metaKey || evento.ctrlKey || evento.shiftKey) return;
      evento.preventDefault();
      grande.src = enlace.href;
      galeria.querySelectorAll('[data-galeria-miniatura]').forEach((otro) => otro.removeAttribute('aria-current'));
      enlace.setAttribute('aria-current', 'true');
    });
  });

  // ── Movimiento de stock: la ayuda cambia con el tipo elegido; el coste solo se pide en las entradas ──
  const movimiento = document.querySelector('[data-movimiento-stock]');
  if (movimiento) {
    const ayuda = movimiento.querySelector('[data-movimiento-ayuda]');
    const coste = movimiento.querySelector('[data-solo-entrada]');
    movimiento.querySelectorAll('input[name="Tipo"]').forEach((radio) =>
      radio.addEventListener('change', () => {
        if (!radio.checked) return;
        ayuda.textContent = radio.dataset.ayuda;
        if (coste) coste.hidden = radio.value !== 'Entrada';
      }),
    );
  }

  const form = document.querySelector('[data-producto-formulario]');
  if (!form) return;

  // ── Lector de códigos de barras: escribe el código y pulsa Enter; aquí Enter pasa al siguiente campo, no guarda ──
  const codigo = form.querySelector('[data-codigo-barras]');
  codigo?.addEventListener('keydown', (evento) => {
    if (evento.key !== 'Enter') return;
    evento.preventDefault();
    form.elements.namedItem('Nombre')?.focus();
  });

  // ── Margen en vivo a partir del precio de venta y el de coste ──
  const venta = form.querySelector('[data-precio-venta]');
  const coste = form.querySelector('[data-precio-coste]');
  const margen = form.querySelector('[data-margen]');
  const textoMargen = margen?.textContent;
  const calcularMargen = () => {
    const v = leerImporte(venta.value);
    const c = leerImporte(coste.value);
    if (!coste.value.trim() || Number.isNaN(v) || Number.isNaN(c) || v <= 0) {
      margen.textContent = textoMargen;
      margen.classList.remove('is-negativo');
      return;
    }
    const diferencia = v - c;
    margen.textContent = `Margen: ${euros.format(diferencia)} por unidad (${Math.round((diferencia / v) * 100)} %).`;
    margen.classList.toggle('is-negativo', diferencia < 0);
  };
  if (venta && coste && margen) {
    venta.addEventListener('input', calcularMargen);
    coste.addEventListener('input', calcularMargen);
    calcularMargen();
  }

  // ── Fotos: las guardadas se pueden quitar o hacer principales; las nuevas se ven antes de guardar ──
  const seccion = form.querySelector('[data-fotos-producto]');
  if (!seccion) return;
  const maximo = Number(seccion.dataset.maximo);
  const entrada = seccion.querySelector('[data-fotos-input]');
  const nuevas = seccion.querySelector('[data-fotos-nuevas]');
  const estado = seccion.querySelector('[data-fotos-estado]');
  const textoEstado = estado.textContent;
  const guardadas = [...seccion.querySelectorAll('[data-foto-guardada]')];
  // Las fotos elegidas se acumulan aquí: elegir más no sustituye a las anteriores y cada una se puede quitar
  let elegidas = new DataTransfer();
  const urls = new Map();

  const quedan = () => guardadas.filter((g) => !g.querySelector('[data-foto-quitar]').checked).length;

  const avisar = (texto) => {
    estado.textContent = texto ?? textoEstado;
    estado.classList.toggle('is-error', Boolean(texto));
  };

  // Una foto que se va a quitar no puede ser la principal: pasa a serlo la primera que se queda
  const revisarPrincipal = () => {
    guardadas.forEach((g) => g.classList.toggle('is-quitada', g.querySelector('[data-foto-quitar]').checked));
    const principal = guardadas.find((g) => g.querySelector('input[type=radio]').checked);
    if (principal?.classList.contains('is-quitada')) {
      principal.querySelector('input[type=radio]').checked = false;
      guardadas.find((g) => !g.classList.contains('is-quitada'))?.querySelector('input[type=radio]').click();
    }
  };

  const pintarNuevas = () => {
    nuevas.replaceChildren();
    [...elegidas.files].forEach((archivo, indice) => {
      if (!urls.has(archivo)) urls.set(archivo, URL.createObjectURL(archivo));
      const tarjeta = document.createElement('div');
      tarjeta.className = 'foto-producto is-nueva';
      tarjeta.setAttribute('role', 'listitem');
      const imagen = document.createElement('img');
      imagen.src = urls.get(archivo);
      imagen.alt = '';
      const quitar = document.createElement('button');
      quitar.type = 'button';
      quitar.className = 'foto-producto-quitar';
      quitar.textContent = 'Quitar';
      quitar.setAttribute('aria-label', `Quitar ${archivo.name}`);
      quitar.addEventListener('click', () => {
        const resto = new DataTransfer();
        [...elegidas.files].forEach((otro, i) => { if (i !== indice) resto.items.add(otro); });
        URL.revokeObjectURL(urls.get(archivo));
        urls.delete(archivo);
        elegidas = resto;
        entrada.files = elegidas.files;
        pintarNuevas();
      });
      const etiqueta = document.createElement('span');
      etiqueta.className = 'foto-producto-nueva';
      etiqueta.textContent = 'Nueva';
      tarjeta.append(imagen, etiqueta, quitar);
      nuevas.append(tarjeta);
    });
    nuevas.hidden = elegidas.files.length === 0;
    const total = quedan() + elegidas.files.length;
    avisar(total > maximo ? `Un producto admite hasta ${maximo} fotos; ahora tendría ${total}. Quita alguna.` : null);
  };

  entrada.addEventListener('change', () => {
    const libres = maximo - quedan() - elegidas.files.length;
    const nuevasElegidas = [...entrada.files];
    nuevasElegidas.slice(0, Math.max(0, libres)).forEach((archivo) => elegidas.items.add(archivo));
    entrada.files = elegidas.files;
    pintarNuevas();
    if (nuevasElegidas.length > libres) avisar(`Solo caben ${maximo} fotos por producto: se han añadido ${Math.max(0, libres)}.`);
  });

  guardadas.forEach((g) => g.querySelector('[data-foto-quitar]').addEventListener('change', () => {
    revisarPrincipal();
    pintarNuevas();
  }));
  revisarPrincipal();
})();
