// Movimiento de la web pública: scroll suave (Lenis) y animaciones al desplazarse (GSAP, ScrollTrigger, SplitText).
// Las librerías están en /lib (sin CDN, compatibles con la CSP). Se marca en el HTML qué se anima:
//   data-anima="subir"     aparece desde abajo al entrar en pantalla
//   data-anima="revelar"   se descubre como una cortina (foto principal)
//   data-anima-grupo       sus hijos aparecen escalonados (tarjetas, listas)
//   data-dividir[="letras"] título que entra por líneas y palabras (o letra a letra)
//   data-parallax="12"     se desplaza ese % más despacio que la página
//   data-flotar, data-giro, data-marquesina, data-magnetico, data-indice-categorias
// Con «reducir movimiento» o si falta una librería no se anima nada y la página se ve completa.
'use strict';

(() => {
  const html = document.documentElement;
  const mostrarTodo = () => {
    window.movimientoListo = true;
    html.classList.remove('anima');
  };
  const { gsap, ScrollTrigger, SplitText, Lenis } = window;
  if (matchMedia('(prefers-reduced-motion: reduce)').matches || !gsap || !ScrollTrigger) {
    mostrarTodo();
    return;
  }
  gsap.registerPlugin(ScrollTrigger, ...(SplitText ? [SplitText] : []));
  const todos = (selector) => gsap.utils.toArray(selector);

  // ── Scroll suave, sincronizado con ScrollTrigger ──
  let lenis = null;
  if (Lenis) {
    lenis = new Lenis({ autoRaf: false, lerp: 0.1, anchors: { offset: -24 } });
    lenis.on('scroll', ScrollTrigger.update);
    gsap.ticker.add((tiempo) => lenis.raf(tiempo * 1000));
    gsap.ticker.lagSmoothing(0);
    // Con el menú hamburguesa abierto la página de detrás no se desplaza (ver app.js)
    document.addEventListener('menu-cambio', ({ detail }) => (detail.abierto ? lenis.stop() : lenis.start()));
  }

  // ── Estado inicial: GSAP toma el control de lo que estaba oculto por la clase .anima ──
  const subir = todos('[data-anima="subir"]');
  const grupos = todos('[data-anima-grupo]');
  const revelar = todos('[data-anima="revelar"]');
  const titulos = todos('[data-dividir]');
  gsap.set(subir, { autoAlpha: 0, y: 36 });
  grupos.forEach((grupo) => gsap.set(grupo.children, { autoAlpha: 0, y: 48 }));
  gsap.set(revelar, { clipPath: 'inset(100% 0% 0% 0%)' });
  gsap.set(titulos, { autoAlpha: 0 });
  mostrarTodo();

  // ── Apariciones al entrar en pantalla ──
  ScrollTrigger.batch(subir, {
    start: 'top 90%',
    once: true,
    onEnter: (elementos) =>
      gsap.to(elementos, { autoAlpha: 1, y: 0, duration: 0.9, ease: 'power3.out', stagger: 0.08, overwrite: true }),
  });
  grupos.forEach((grupo) =>
    ScrollTrigger.create({
      trigger: grupo,
      start: 'top 88%',
      once: true,
      onEnter: () =>
        gsap.to(grupo.children, { autoAlpha: 1, y: 0, duration: 0.9, ease: 'power3.out', stagger: 0.09, overwrite: true }),
    }),
  );
  revelar.forEach((elemento) =>
    gsap.to(elemento, { clipPath: 'inset(0% 0% 0% 0%)', duration: 1.5, ease: 'expo.inOut', delay: 0.1 }),
  );

  // ── Títulos por líneas (con máscara) o letra a letra ──
  const dividir = (titulo) => {
    const letras = titulo.dataset.dividir === 'letras';
    if (!SplitText) {
      gsap.to(titulo, { autoAlpha: 1, duration: 0.6 });
      return;
    }
    SplitText.create(titulo, {
      type: letras ? 'chars' : 'lines,words',
      mask: letras ? undefined : 'lines',
      autoSplit: !letras,
      onSplit(partes) {
        gsap.set(titulo, { autoAlpha: 1 });
        return gsap.from(letras ? partes.chars : partes.words, {
          yPercent: letras ? 60 : 110,
          autoAlpha: letras ? 0 : 1,
          duration: letras ? 0.7 : 1.15,
          ease: 'expo.out',
          stagger: letras ? 0.035 : 0.045,
          scrollTrigger: { trigger: titulo, start: 'top 90%', once: true },
        });
      },
    });
  };
  // Esperar a las fuentes evita cortar las líneas en un sitio y pintarlas en otro
  (document.fonts?.ready ?? Promise.resolve()).then(() => titulos.forEach(dividir));

  // ── Parallax: fotos y elementos que se desplazan más despacio que la página ──
  todos('[data-parallax]').forEach((elemento) =>
    gsap.to(elemento, {
      yPercent: Number(elemento.dataset.parallax) || 10,
      ease: 'none',
      scrollTrigger: { trigger: elemento.parentElement, start: 'top top', end: 'bottom top', scrub: true },
    }),
  );

  // ── Fondo vivo: las hojas giran y derivan con el scroll; las manchas respiran solas ──
  const recorrido = { trigger: document.body, start: 'top top', end: 'bottom bottom', scrub: 1.5 };
  gsap.to('.fondo-hoja-1', { y: '-55vh', rotation: 40, ease: 'none', scrollTrigger: recorrido });
  gsap.to('.fondo-hoja-2', { y: '70vh', x: '-18vw', rotation: -70, ease: 'none', scrollTrigger: { ...recorrido } });
  gsap.to('.fondo-hoja-3', { y: '-40vh', rotation: 110, ease: 'none', scrollTrigger: { ...recorrido } });
  todos('.fondo-mancha').forEach((mancha, i) =>
    gsap.to(mancha, {
      x: gsap.utils.random(-80, 80),
      y: gsap.utils.random(-60, 60),
      scale: gsap.utils.random(0.85, 1.2),
      duration: 9 + i * 3,
      ease: 'sine.inOut',
      yoyo: true,
      repeat: -1,
    }),
  );

  // ── Detalles ──
  gsap.to('[data-flotar]', { y: -10, rotation: 2.5, duration: 3.2, ease: 'sine.inOut', yoyo: true, repeat: -1 });
  todos('[data-giro]').forEach((estrella) =>
    gsap.to(estrella, {
      rotation: 140 * (Number(estrella.dataset.giro) || 1),
      ease: 'none',
      scrollTrigger: { trigger: estrella.parentElement, start: 'top bottom', end: 'bottom top', scrub: true },
    }),
  );

  // Franja en bucle infinito: acelera un poco cuando se hace scroll deprisa
  todos('[data-marquesina]').forEach((franja) => {
    const pista = franja.querySelector('.franja-pista');
    if (!pista) return;
    const copia = pista.cloneNode(true);
    copia.setAttribute('aria-hidden', 'true');
    pista.after(copia);
    [pista, copia].forEach((p) => p.classList.add('en-bucle'));
    const bucle = gsap.to([pista, copia], { xPercent: -100, ease: 'none', duration: 32, repeat: -1 });
    lenis?.on('scroll', ({ velocity }) => {
      gsap.to(bucle, { timeScale: 1 + Math.min(Math.abs(velocity) / 6, 4), duration: 0.2, overwrite: true });
      gsap.to(bucle, { timeScale: 1, duration: 1.2, delay: 0.25, overwrite: false });
    });
  });

  // Botones que siguen ligeramente al ratón (solo con ratón, no en pantallas táctiles)
  if (matchMedia('(hover: hover) and (pointer: fine)').matches) {
    todos('[data-magnetico]').forEach((boton) => {
      const x = gsap.quickTo(boton, 'x', { duration: 0.5, ease: 'power3' });
      const y = gsap.quickTo(boton, 'y', { duration: 0.5, ease: 'power3' });
      boton.addEventListener('pointermove', (evento) => {
        const caja = boton.getBoundingClientRect();
        x((evento.clientX - caja.left - caja.width / 2) * 0.25);
        y((evento.clientY - caja.top - caja.height / 2) * 0.35);
      });
      boton.addEventListener('pointerleave', () => {
        x(0);
        y(0);
      });
    });
  }

  // Servicios: el índice lateral marca la categoría que se está leyendo
  document.querySelectorAll('[data-indice-categorias] a').forEach((enlace) => {
    const seccion = document.getElementById(decodeURIComponent(enlace.hash.slice(1)));
    if (!seccion) return;
    ScrollTrigger.create({
      trigger: seccion,
      start: 'top 45%',
      end: 'bottom 45%',
      onToggle: ({ isActive }) =>
        isActive ? enlace.setAttribute('aria-current', 'true') : enlace.removeAttribute('aria-current'),
    });
  });

  // Página de error: las cifras caen y la hoja se mece
  const cifras = todos('[data-error-numero] span');
  if (cifras.length) {
    gsap.from(cifras, { yPercent: -130, rotation: () => gsap.utils.random(-30, 30), autoAlpha: 0, duration: 1.3, ease: 'bounce.out', stagger: 0.12 });
    gsap.to('.error-hoja', { rotation: 14, y: -12, duration: 2.6, ease: 'sine.inOut', yoyo: true, repeat: -1 });
  }

  // Las fotos cambian la altura de la página al cargar: recalcular posiciones
  window.addEventListener('load', () => ScrollTrigger.refresh());
})();
