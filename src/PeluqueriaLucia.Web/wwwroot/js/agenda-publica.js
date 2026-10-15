// Reserva de la web pública: para quién es la cita y qué cuidado.
// · «¿Para quién es la cita?» (Hombre, Mujer, Otro) es el sexo de la reserva: rellena el campo oculto Formulario.Sexo
//   del formulario de al lado y filtra la lista. Con Hombre o Mujer se ven sus cuidados y los unisex (público «Otro»);
//   con Otro, o sin elegir, todos. Sin elegirlo, la solicitud no se envía y se avisa ahí mismo.
// · La lista se busca escribiendo (select-buscable.js); si el cuidado no está, se puede escribir y preguntar por él.
// · Al elegir un cuidado, la página pide su disponibilidad (el formulario GET de siempre, sin pulsar el botón).
'use strict';

(() => {
  const formulario = document.querySelector('form.agenda-servicio');
  const select = formulario?.querySelector('#servicio-agenda');
  const buscable = select?.buscable;
  const para = formulario?.querySelector('[data-para]');
  if (!buscable || !para) return;

  const reserva = document.querySelector('form.agenda-formulario');
  const sexo = reserva?.querySelector('[data-sexo-cita]');
  const aviso = para.querySelector('[data-para-aviso]');
  const opciones = [...para.querySelectorAll('input[name="para"]')];
  const valor = () => opciones.find((r) => r.checked)?.value ?? '';
  para.hidden = false;

  // Los días y meses del calendario conservan la elección al cambiar de fecha
  const enlaces = (publico) =>
    document.querySelectorAll('.agenda-calendario a[href*="dia="]').forEach((enlace) => {
      const destino = new URL(enlace.href);
      if (publico) destino.searchParams.set('para', publico);
      else destino.searchParams.delete('para');
      enlace.href = destino.pathname + destino.search;
    });

  const aplicar = () => {
    const publico = valor();
    buscable.filtrar(publico === 'Hombre' || publico === 'Mujer' ? (opcion) => opcion.dataset.sexo === publico || opcion.dataset.sexo === 'Otro' : null);
    if (sexo) sexo.value = publico;
    if (publico && aviso) aviso.hidden = true;
  };

  opciones.forEach((radio) =>
    radio.addEventListener('change', () => {
      aplicar();
      enlaces(valor());
    }),
  );

  // Elegir un cuidado (o una consulta escrita) trae su disponibilidad
  select.addEventListener('change', () => formulario.requestSubmit());

  // Sin «¿Para quién es la cita?» no se envía. En fase de captura, antes que el control de doble envío de app.js,
  // para que el siguiente intento no quede bloqueado
  document.addEventListener(
    'submit',
    (evento) => {
      if (evento.target !== reserva || !sexo || sexo.value) return;
      evento.preventDefault();
      if (aviso) aviso.hidden = false;
      para.scrollIntoView({ block: 'center', behavior: 'smooth' });
      opciones[0]?.focus({ preventScroll: true });
    },
    true,
  );

  aplicar();
})();
