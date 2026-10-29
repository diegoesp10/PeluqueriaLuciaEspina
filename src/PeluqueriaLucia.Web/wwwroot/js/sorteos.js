// Sorteo: añadir participantes con el buscador de clientes (combo-clientes.js) y, recién sorteado, descubrir al ganador
// con una ruleta de nombres que se va frenando y un poco de confeti. Sin animaciones si el sistema pide reducir movimiento.
'use strict';

(() => {
  // ── Participantes: elegir una sugerencia la añade al momento ──────────────
  const agregar = document.querySelector('[data-participante-agregar]');
  if (agregar && window.ComboClientes) {
    const id = agregar.querySelector('[data-cliente-id]');
    const combo = window.ComboClientes.montar(agregar.querySelector('[data-combo]'), {
      alElegir: (cliente) => {
        id.value = cliente.id;
        agregar.requestSubmit();
      },
    });
    // Enter sin sugerencia elegida no envía el formulario vacío
    agregar.addEventListener('submit', (e) => {
      if (!id.value) {
        e.preventDefault();
        combo?.enfocar();
      }
    });
  }

  // ── Ganador recién sorteado: ruleta y confeti ─────────────────────────────
  const tarjeta = document.querySelector('[data-revelar="true"]');
  if (!tarjeta) return;

  const contenido = tarjeta.querySelector('[data-ganador-contenido]');
  const ruleta = tarjeta.querySelector('[data-ruleta]');
  const nombre = ruleta.querySelector('[data-ruleta-nombre]');
  const confeti = tarjeta.querySelector('[data-confeti]');
  let nombres = [];
  try {
    nombres = JSON.parse(tarjeta.dataset.nombres || '[]');
  } catch {
    nombres = [];
  }

  const celebrar = () => {
    tarjeta.classList.add('is-revelado');
    if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    const colores = ['var(--accent)', 'var(--pink)', 'var(--orange)', 'var(--green)', 'var(--blue)', 'var(--purple)'];
    for (let i = 0; i < 42; i++) {
      const pieza = document.createElement('i');
      pieza.style.setProperty('--x', `${Math.round(Math.random() * 100)}%`);
      pieza.style.setProperty('--giro', `${Math.round(Math.random() * 720 - 360)}deg`);
      pieza.style.setProperty('--retraso', `${Math.round(Math.random() * 350)}ms`);
      pieza.style.setProperty('--caida', `${Math.round(900 + Math.random() * 900)}ms`);
      pieza.style.background = colores[i % colores.length];
      confeti.append(pieza);
    }
    setTimeout(() => confeti.replaceChildren(), 2600);
  };

  if (nombres.length < 2 || matchMedia('(prefers-reduced-motion: reduce)').matches) {
    celebrar();
    return;
  }

  // La ruleta pasa nombres cada vez más despacio y se para en el del ganador
  contenido.hidden = true;
  ruleta.hidden = false;
  tarjeta.scrollIntoView({ block: 'center', behavior: 'smooth' });
  let paso = 0;
  let espera = 55;
  const girar = () => {
    nombre.textContent = nombres[paso % nombres.length];
    nombre.classList.remove('is-paso');
    void nombre.offsetWidth;
    nombre.classList.add('is-paso');
    paso++;
    espera *= 1.09;
    if (espera < 420) {
      setTimeout(girar, espera);
      return;
    }
    nombre.textContent = tarjeta.dataset.ganador;
    setTimeout(() => {
      ruleta.hidden = true;
      contenido.hidden = false;
      celebrar();
    }, 650);
  };
  girar();
})();
