'use strict';
// Calendario propio, progresivo: sin JavaScript sigue disponible el control nativo.
(() => {
  const t = (es) => window.Peluqueria.texto(es, es);
  let numero = 0;
  const iso = (d) => `${String(d.getFullYear()).padStart(4,'0')}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  const fechaEn = (anio, mes, dia = 1) => { const d = new Date(0); d.setFullYear(anio,mes,dia); d.setHours(12,0,0,0); return d; };
  const hoy = () => new Date(new Date().getFullYear(), new Date().getMonth(), new Date().getDate());
  document.querySelectorAll('input[type="date"], input[type="month"], input[type="datetime-local"]').forEach((input) => {
    if (input.disabled || input.readOnly) return;
    const mensual = input.type === 'month', conHora = input.type === 'datetime-local', anual = input.hasAttribute('data-fecha-anual');
    const contenedor = document.createElement('div'); contenedor.className = 'fecha-control';
    input.before(contenedor); contenedor.append(input); input.classList.add('fecha-original');
    input.tabIndex = -1;
    const etiqueta = input.getAttribute('aria-label') || document.querySelector(`label[for="${input.id}"]`)?.textContent || t('Elegir fecha');
    const boton = document.createElement('button'); boton.type = 'button'; boton.className = 'input fecha-boton';
    boton.setAttribute('aria-label', etiqueta); boton.setAttribute('aria-haspopup','dialog'); boton.setAttribute('aria-expanded','false');
    const panel = document.createElement('div'); panel.className = 'fecha-calendario'; panel.id = `calendario-${++numero}`; panel.hidden = true;
    panel.setAttribute('role','dialog'); panel.setAttribute('aria-label', etiqueta); boton.setAttribute('aria-controls',panel.id);
    contenedor.append(boton,panel);
    const seleccion = () => input.value ? new Date(`${input.value.slice(0,10)}${mensual ? '-01' : ''}T12:00:00`) : hoy();
    let vista = seleccion();
    const cerrar = () => { panel.hidden = true; boton.setAttribute('aria-expanded','false'); };
    const colocar = () => {
      const r = boton.getBoundingClientRect(), alto = panel.offsetHeight;
      panel.style.left = `${Math.max(20,Math.min(r.left,innerWidth-panel.offsetWidth-20))}px`;
      panel.style.top = `${r.bottom+10+alto <= innerHeight-20 ? r.bottom+10 : r.top-alto-10 >= 20 ? r.top-alto-10 : 20}px`;
    };
    const rotular = () => { const d = seleccion(); boton.textContent = (input.value ? anual ? `${window.Peluqueria.texto('anio', t('Año'))} ${d.getFullYear()}` : new Intl.DateTimeFormat(window.Peluqueria.cultura, mensual ? { month:'long',year:'numeric' } : { day:'numeric',month:'long',year:'numeric' }).format(d) : t('Elegir fecha')) + (conHora && input.value ? ` · ${input.value.slice(11,16)}` : '') + ''; };
    const aplicar = (valor) => { input.value = valor; input.dispatchEvent(new Event('change',{bubbles:true})); rotular(); };
    const crearBoton = (texto, nombre, accion) => { const b = document.createElement('button'); b.type = 'button'; b.textContent = texto; b.setAttribute('aria-label',nombre); b.addEventListener('click',accion); return b; };
    const pintar = () => {
      panel.replaceChildren();
      const cabecera = document.createElement('div'); cabecera.className = 'fecha-cabecera';
      const controles = document.createElement('div'); controles.className = 'fecha-selectores';
      const etiquetaAnio = document.createElement('label'); etiquetaAnio.textContent = t('Año'); etiquetaAnio.className = 'fecha-selector';
      const anio = document.createElement('input'); anio.type = 'number'; anio.className = 'input'; anio.min = '1'; anio.max = '9999'; anio.step = '1'; anio.value = vista.getFullYear();
      anio.addEventListener('change',() => {
        if (!anio.checkValidity() || !anio.value) return;
        vista = fechaEn(Number(anio.value),vista.getMonth());
        if (anual) { aplicar(`${iso(vista).slice(0,4)}-01-01`); cerrar(); boton.focus({preventScroll:true}); }
        else { pintar(); panel.querySelector('input[type=number]').focus({preventScroll:true}); }
      });
      if (!mensual && !anual) {
        const etiquetaMes = document.createElement('label'); etiquetaMes.textContent = t('Mes'); etiquetaMes.className = 'fecha-selector';
        const mes = document.createElement('select'); mes.className = 'input';
        for(let m=0;m<12;m++) { const opcion = document.createElement('option'); opcion.value = m; opcion.textContent = new Intl.DateTimeFormat(window.Peluqueria.cultura,{month:'long'}).format(fechaEn(2026,m)); mes.append(opcion); }
        mes.value = vista.getMonth();
        mes.addEventListener('change',() => { vista = fechaEn(vista.getFullYear(),Number(mes.value)); pintar(); panel.querySelector('select').focus({preventScroll:true}); });
        etiquetaMes.append(mes); controles.append(etiquetaMes);
      }
      etiquetaAnio.append(anio); controles.append(etiquetaAnio);
      const desplazar = (direccion) => {
        const nueva = fechaEn(vista.getFullYear()+direccion*(anual ? 12 : mensual ? 1 : 0), vista.getMonth()+direccion*(anual || mensual ? 0 : 1));
        vista = nueva.getFullYear() < 1 ? fechaEn(1,0) : nueva.getFullYear() > 9999 ? fechaEn(9999,11) : nueva;
        pintar();
      };
      const anterior = crearBoton('‹',t('Periodo anterior'),() => desplazar(-1)), siguiente = crearBoton('›',t('Periodo siguiente'),() => desplazar(1));
      anterior.disabled = vista.getFullYear() === 1 && (anual || mensual || vista.getMonth() === 0);
      siguiente.disabled = vista.getFullYear() === 9999 && (anual || mensual || vista.getMonth() === 11);
      cabecera.append(anterior,controles,siguiente);
      panel.append(cabecera);
      const rejilla = document.createElement('div'); rejilla.className = mensual || anual ? 'fecha-meses' : 'fecha-dias';
      if (!mensual && !anual) (window.Peluqueria.idioma === 'en' ? ['M','T','W','T','F','S','S'] : ['L','M','X','J','V','S','D']).forEach((n) => { const e = document.createElement('span'); e.textContent = n; e.className = 'fecha-semana'; rejilla.append(e); });
      const inicio = (fechaEn(vista.getFullYear(),vista.getMonth()).getDay()+6)%7;
      if (!mensual && !anual) for(let i=0;i<inicio;i++) rejilla.append(document.createElement('span'));
      const primeroAnio = Math.min(9988,Math.floor((vista.getFullYear()-1)/12)*12+1);
      const total = mensual || anual ? 12 : fechaEn(vista.getFullYear(),vista.getMonth()+1,0).getDate();
      for(let i=0;i<total;i++) {
        const fecha = fechaEn(anual ? primeroAnio+i : vista.getFullYear(),anual ? 0 : mensual ? i : vista.getMonth(),anual || mensual ? 1 : i+1);
        const valor = mensual ? iso(fecha).slice(0,7) : iso(fecha);
        const texto = anual ? String(fecha.getFullYear()) : mensual ? new Intl.DateTimeFormat(window.Peluqueria.cultura,{month:'short'}).format(fecha) : String(i+1);
        const b = crearBoton(texto,anual ? `${window.Peluqueria.texto('anio', t('Año'))} ${texto}` : new Intl.DateTimeFormat(window.Peluqueria.cultura,{day:'numeric',month:'long',year:'numeric'}).format(fecha),() => {
          aplicar(conHora ? `${valor}T${input.value.slice(11,16) || '12:00'}` : valor);
          if (!conHora) { cerrar(); boton.focus({preventScroll:true}); } else pintar();
        });
        if (anual ? input.value.slice(0,4) === valor.slice(0,4) : input.value.startsWith(valor)) { b.classList.add('is-active'); b.setAttribute('aria-pressed','true'); }
        if (anual ? fecha.getFullYear() === hoy().getFullYear() : valor === (mensual ? iso(hoy()).slice(0,7) : iso(hoy()))) b.classList.add('es-hoy');
        b.disabled = (input.min && valor < input.min.slice(0,valor.length)) || (input.max && valor > input.max.slice(0,valor.length));
        rejilla.append(b);
      }
      panel.append(rejilla);
      if (conHora) {
        const label = document.createElement('label'); label.textContent = t('Hora de la visita'); label.className = 'field';
        const hora = document.createElement('input'); hora.type = 'time'; hora.className = 'input'; hora.value = input.value.slice(11,16) || '12:00';
        hora.addEventListener('change',() => { if(hora.value) aplicar(`${iso(seleccion())}T${hora.value}`); }); label.append(hora); panel.append(label);
      }
      const pie = document.createElement('div'); pie.className = 'fecha-pie'; pie.append(crearBoton(anual ? t('Este año') : mensual ? t('Este mes') : t('Hoy'),t('Seleccionar periodo actual'),() => { aplicar(conHora ? `${iso(hoy())}T${input.value.slice(11,16) || '12:00'}` : anual ? `${hoy().getFullYear()}-01-01` : mensual ? iso(hoy()).slice(0,7) : iso(hoy())); cerrar(); boton.focus({preventScroll:true}); }),crearBoton(t('Listo'),t('Cerrar calendario'),() => { cerrar(); boton.focus({preventScroll:true}); })); panel.append(pie);
      if (!panel.hidden) colocar();
    };
    boton.addEventListener('click',() => { if (!panel.hidden) { cerrar(); return; } vista = seleccion(); pintar(); panel.hidden = false; colocar(); boton.setAttribute('aria-expanded','true'); panel.querySelector('.is-active')?.focus({preventScroll:true}); });
    input.addEventListener('invalid',() => { boton.focus({preventScroll:true}); });
    window.addEventListener('resize',cerrar);
    document.addEventListener('scroll',(e) => { if(e.target !== panel) cerrar(); },true);
    input.addEventListener('change',rotular);
    document.addEventListener('pointerdown',(e) => { if(!contenedor.contains(e.target)) cerrar(); });
    contenedor.addEventListener('keydown',(e) => {
      if(e.key === 'Escape') { cerrar(); boton.focus({preventScroll:true}); }
      const dias = [...panel.querySelectorAll('.fecha-dias button:not(:disabled), .fecha-meses button:not(:disabled)')];
      const posicion = dias.indexOf(document.activeElement); const paso = mensual ? 3 : 7;
      const avance = {ArrowRight:1,ArrowLeft:-1,ArrowDown:paso,ArrowUp:-paso}[e.key];
      if(posicion >= 0 && avance) { e.preventDefault(); dias[Math.max(0,Math.min(dias.length-1,posicion+avance))].focus({preventScroll:true}); }
    });
    rotular();
  });
})();
