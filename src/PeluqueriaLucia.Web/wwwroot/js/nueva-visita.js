// Nueva visita: buscador de clientes (combo-clientes.js), buscador y filtro de servicios, productos y total en vivo.
'use strict';

(() => {
  const form = document.querySelector('[data-nueva-visita]');
  if (!form) return;

  const { euros, leerImporte, importeCampo } = window.Peluqueria;
  const sexos = ['Hombre', 'Mujer', 'Otro'];

  // ── Cliente ──────────────────────────────────────────────────────────────
  const clienteId = form.querySelector('[data-cliente-id]');
  const elegido = form.querySelector('[data-cliente-elegido]');
  const comboRaiz = form.querySelector('[data-combo]');

  function elegir(c) {
    clienteId.value = c.id;
    elegido.querySelector('[data-cliente-nombre]').textContent = c.nombre;
    elegido.querySelector('[data-cliente-detalle]').textContent = c.detalle ?? '';
    const avatar = elegido.querySelector('[data-cliente-avatar]');
    avatar.className = `avatar ${window.ComboClientes.tinte(c)}`;
    avatar.textContent = c.iniciales;
    elegido.hidden = false;
    comboRaiz.hidden = true;
    // Los servicios se filtran por el sexo del cliente
    const filtro = form.querySelector(`[data-filtro-servicios][value="${sexos[c.sexo]}"]`);
    if (filtro) {
      filtro.checked = true;
      filtrarServicios();
    }
  }

  const combo = window.ComboClientes?.montar(comboRaiz, { alElegir: elegir });

  form.querySelector('[data-cliente-cambiar]').addEventListener('click', () => {
    clienteId.value = '';
    elegido.hidden = true;
    comboRaiz.hidden = false;
    combo?.limpiar();
    combo?.enfocar();
  });

  // ── Servicios ────────────────────────────────────────────────────────────
  const opciones = [...form.querySelectorAll('.servicio-opcion')];
  const sinServicios = form.querySelector('[data-sin-servicios]');
  const textoTotal = form.querySelector('[data-total]');
  const textoNumero = form.querySelector('[data-numero-servicios]');
  const buscadorServicios = form.querySelector('[data-filtro-texto]');

  // «Ampolla» encuentra «AMPOLLA caída / hidratación»: sin mayúsculas ni acentos
  const normalizar = (texto) => texto.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();

  // Un servicio ya marcado sigue a la vista aunque no sea del público filtrado ni coincida con lo escrito
  function filtrarServicios() {
    const filtro = form.querySelector('[data-filtro-servicios]:checked')?.value ?? 'Todos';
    const texto = normalizar(buscadorServicios?.value ?? '');
    let visibles = 0;
    opciones.forEach((opcion) => {
      const marcado = opcion.querySelector('input[type=checkbox]').checked;
      const delPublico = filtro === 'Todos' || opcion.dataset.sexo === filtro;
      const coincide = !texto || normalizar(opcion.querySelector('.row-title').textContent).includes(texto);
      const visible = marcado || (delPublico && coincide);
      opcion.hidden = !visible;
      if (visible) visibles++;
    });
    if (sinServicios) sinServicios.hidden = visibles > 0;
  }

  // Filas de producto (ver más abajo): unidades × precio de cada una
  const lineasProducto = () => [...form.querySelectorAll('[data-producto-linea]')];
  const importe = (campo) => {
    const valor = leerImporte(campo.value);
    return Number.isNaN(valor) ? 0 : Math.round(valor * 100);
  };

  const recalcular = () => {
    const marcados = opciones.map((o) => o.querySelector('input[type=checkbox]')).filter((i) => i.checked);
    const servicios = marcados.reduce((suma, i) => suma + importe(form.querySelector(`[data-importe-servicio="${i.value}"]`)), 0);
    let unidades = 0;
    const productos = lineasProducto().reduce((suma, fila) => {
      const cantidad = Math.max(0, Math.trunc(Number(fila.querySelector('[data-linea-cantidad]').value) || 0));
      unidades += cantidad;
      return suma + cantidad * importe(fila.querySelector('[data-linea-precio]'));
    }, 0);
    textoTotal.textContent = euros.format((servicios + productos) / 100);
    const partes = [marcados.length === 1 ? '1 servicio' : `${marcados.length} servicios`];
    if (unidades > 0) partes.push(unidades === 1 ? '1 producto' : `${unidades} productos`);
    textoNumero.textContent = partes.join(' · ');
  };

  form.querySelectorAll('[data-filtro-servicios]').forEach((r) => r.addEventListener('change', filtrarServicios));
  buscadorServicios?.addEventListener('input', filtrarServicios);
  // Enter en el buscador de servicios filtra, no guarda la visita
  buscadorServicios?.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') e.preventDefault();
  });
  opciones.forEach((o) => {
    const check = o.querySelector('input[type=checkbox]'), precio = o.querySelector('[data-importe-servicio]');
    check.addEventListener('change',() => {
      precio.disabled = !check.checked;
      o.querySelector('.importe-visita').hidden = !check.checked;
      o.querySelector('[data-importe-base]').hidden = check.checked;
      recalcular();
    });
    precio.addEventListener('input',recalcular);
  });
  // ── Productos ────────────────────────────────────────────────────────────
  // El buscador (select-buscable.js, también con el lector de códigos) elige el producto; «Añadir» crea su fila.
  // Elegir uno que ya está suma unidades. Pedir más de lo que hay no se impide: se avisa de que el stock quedará en negativo.
  const elegirProducto = form.querySelector('[data-producto-elegir]');
  const cantidadNueva = form.querySelector('[data-producto-cantidad]');
  const listaProductos = form.querySelector('[data-productos-lista]');
  const plantilla = form.querySelector('[data-producto-plantilla]');
  const aviso = form.querySelector('[data-producto-aviso]');
  const textoAviso = aviso?.textContent;

  const enStock = (n) => (n > 0 ? `${n} en stock` : n === 0 ? 'agotado' : `en negativo (−${-n})`);

  /** Sin texto vuelve la ayuda; «is-error» para errores y «is-aviso» para avisos que no impiden seguir. */
  const avisar = (texto, tipo = 'is-error') => {
    if (!aviso) return;
    aviso.textContent = texto ?? textoAviso;
    aviso.classList.remove('is-error', 'is-aviso');
    if (texto) aviso.classList.add(tipo);
  };

  // Los nombres van seguidos (Productos[0], Productos[1]…): el servidor deja de leer en el primer hueco
  const renumerar = () => {
    lineasProducto().forEach((fila, i) => {
      fila.querySelectorAll('[name^="Productos["]').forEach((campo) => {
        campo.name = campo.name.replace(/^Productos\[\d+\]/, `Productos[${i}]`);
      });
    });
    if (listaProductos) listaProductos.hidden = lineasProducto().length === 0;
  };

  // Si se piden más de las que hay, al cobrar el stock quedará en negativo: se avisa, sin impedirlo
  const revisarStock = () => {
    const porProducto = new Map();
    lineasProducto().forEach((fila) => {
      const actual = porProducto.get(fila.dataset.productoId)
        ?? { nombre: fila.querySelector('[data-linea-nombre]').textContent, stock: Number(fila.dataset.stock), pedidas: 0 };
      actual.pedidas += Math.max(0, Math.trunc(Number(fila.querySelector('[data-linea-cantidad]').value)) || 0);
      porProducto.set(fila.dataset.productoId, actual);
    });
    const negativos = [...porProducto.values()].filter((p) => p.pedidas > p.stock);
    avisar(negativos.length === 0 ? null
      : `Al cobrar quedará en negativo: ${negativos.map((p) => `«${p.nombre}» (−${p.pedidas - p.stock})`).join(', ')}. Recuerda reponerlo.`, 'is-aviso');
  };

  const prepararFila = (fila) => {
    fila.querySelector('[data-linea-cantidad]').addEventListener('input', () => { revisarStock(); recalcular(); });
    fila.querySelector('[data-linea-precio]').addEventListener('input', recalcular);
    fila.querySelector('[data-linea-quitar]').addEventListener('click', () => {
      fila.remove();
      renumerar();
      recalcular();
      revisarStock();
      elegirProducto?.buscable?.enfocar();
    });
  };

  function anadirProducto() {
    const opcion = elegirProducto?.selectedOptions[0];
    if (!opcion || !opcion.value) {
      avisar('Elige primero un producto de la lista.');
      elegirProducto?.buscable?.enfocar();
      return;
    }
    const cantidad = Math.max(1, Math.trunc(Number(cantidadNueva.value) || 1));
    const stock = Number(opcion.dataset.stock);
    const existente = lineasProducto().find((f) => f.dataset.productoId === opcion.value
      && importe(f.querySelector('[data-linea-precio]')) === Math.round(Number(opcion.dataset.precio) * 100));

    if (existente) {
      const campo = existente.querySelector('[data-linea-cantidad]');
      campo.value = String((Math.trunc(Number(campo.value)) || 0) + cantidad);
    } else {
      const fila = plantilla.content.firstElementChild.cloneNode(true);
      fila.dataset.productoId = opcion.value;
      fila.dataset.stock = opcion.dataset.stock;
      fila.querySelector('[data-linea-nombre]').textContent = opcion.dataset.nombre;
      fila.querySelector('[data-linea-referencia]').textContent = opcion.dataset.referencia;
      fila.querySelector('[data-linea-stock]').textContent = enStock(stock);
      fila.querySelector('[data-linea-id]').value = opcion.value;
      fila.querySelector('[data-linea-cantidad]').value = String(cantidad);
      fila.querySelector('[data-linea-cantidad]').setAttribute('aria-label', `Unidades de ${opcion.dataset.nombre}`);
      const precio = fila.querySelector('[data-linea-precio]');
      precio.value = importeCampo(Number(opcion.dataset.precio));
      precio.setAttribute('aria-label', `Precio por unidad de ${opcion.dataset.nombre}`);
      fila.querySelector('[data-linea-quitar]').setAttribute('aria-label', `Quitar ${opcion.dataset.nombre}`);
      if (opcion.dataset.foto) {
        const imagen = document.createElement('img');
        imagen.src = opcion.dataset.foto;
        imagen.alt = '';
        imagen.width = 40;
        imagen.height = 40;
        imagen.addEventListener('error', () => imagen.remove(), { once: true });
        fila.querySelector('.producto-miniatura').append(imagen);
      }
      listaProductos.append(fila);
      prepararFila(fila);
      renumerar();
    }

    revisarStock();
    cantidadNueva.value = '1';
    // Listo para el siguiente producto (o el siguiente código del lector)
    elegirProducto.value = '';
    elegirProducto.dispatchEvent(new Event('change', { bubbles: true }));
    elegirProducto.buscable?.enfocar();
    recalcular();
  }

  if (elegirProducto && plantilla && listaProductos) {
    lineasProducto().forEach(prepararFila);
    revisarStock();
    form.querySelector('[data-producto-anadir]').addEventListener('click', anadirProducto);
    // Con el lector de códigos: el código elige el producto (Enter en el buscador), el foco pasa a las unidades y Enter lo añade
    elegirProducto.addEventListener('change', () => {
      if (elegirProducto.value) cantidadNueva.focus();
    });
    cantidadNueva.addEventListener('keydown', (e) => {
      if (e.key !== 'Enter') return;
      e.preventDefault();
      anadirProducto();
    });
    renumerar();
  }

  filtrarServicios();
  recalcular();
})();
