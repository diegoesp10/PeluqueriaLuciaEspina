'use strict';
// Calendario propio, progresivo: sin JavaScript sigue disponible el control nativo.
(() => {
  let numero = 0;
  const iso = (d) => `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
  const hoy = () => new Date(new Date().getFullYear(), new Date().getMonth(), new Date().getDate());
  document.querySelectorAll('input[type="date"], input[type="month"], input[type="datetime-local"]').forEach((input) => {
    if (input.disabled || input.readOnly) return;
    const mensual = input.type === 'month', conHora = input.type === 'datetime-local';
    const contenedor = document.createElement('div'); contenedor.className = 'fecha-control';
    input.before(contenedor); contenedor.append(input); input.classList.add('fecha-original');
    input.tabIndex = -1;
    const etiqueta = input.getAttribute('aria-label') || document.querySelector(`label[for="${input.id}"]`)?.textContent || 'Elegir fecha';
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
    const rotular = () => { const d = seleccion(); boton.textContent = (input.value ? new Intl.DateTimeFormat('es-ES', mensual ? { month:'long',year:'numeric' } : { day:'numeric',month:'long',year:'numeric' }).format(d) : 'Elegir fecha') + (conHora && input.value ? ` · ${input.value.slice(11,16)}` : '') + ' ▾'; };
    const aplicar = (valor) => { input.value = valor; input.dispatchEvent(new Event('change',{bubbles:true})); rotular(); };
    const crearBoton = (texto, nombre, accion) => { const b = document.createElement('button'); b.type = 'button'; b.textContent = texto; b.setAttribute('aria-label',nombre); b.addEventListener('click',accion); return b; };
    const pintar = () => {
      panel.replaceChildren();
      const cabecera = document.createElement('div'); cabecera.className = 'fecha-cabecera';
      const titulo = document.createElement('strong'); titulo.textContent = mensual ? String(vista.getFullYear()) : new Intl.DateTimeFormat('es-ES',{month:'long',year:'numeric'}).format(vista);
      titulo.setAttribute('aria-live','polite');
      cabecera.append(crearBoton('‹','Periodo anterior',() => { vista = new Date(vista.getFullYear()-(mensual ? 1 : 0),vista.getMonth()-(mensual ? 0 : 1),1); pintar(); }),titulo,
        crearBoton('›','Periodo siguiente',() => { vista = new Date(vista.getFullYear()+(mensual ? 1 : 0),vista.getMonth()+(mensual ? 0 : 1),1); pintar(); }));
      panel.append(cabecera);
      const rejilla = document.createElement('div'); rejilla.className = mensual ? 'fecha-meses' : 'fecha-dias';
      if (!mensual) ['L','M','X','J','V','S','D'].forEach((n) => { const e = document.createElement('span'); e.textContent = n; e.className = 'fecha-semana'; rejilla.append(e); });
      const inicio = (new Date(vista.getFullYear(),vista.getMonth(),1).getDay()+6)%7;
      if (!mensual) for(let i=0;i<inicio;i++) rejilla.append(document.createElement('span'));
      const total = mensual ? 12 : new Date(vista.getFullYear(),vista.getMonth()+1,0).getDate();
      for(let i=0;i<total;i++) {
        const fecha = new Date(vista.getFullYear(),mensual ? i : vista.getMonth(),mensual ? 1 : i+1);
        const valor = mensual ? iso(fecha).slice(0,7) : iso(fecha);
        const texto = mensual ? new Intl.DateTimeFormat('es-ES',{month:'short'}).format(fecha) : String(i+1);
        const b = crearBoton(texto,new Intl.DateTimeFormat('es-ES',{day:'numeric',month:'long',year:'numeric'}).format(fecha),() => {
          aplicar(conHora ? `${valor}T${input.value.slice(11,16) || '12:00'}` : valor);
          if (!conHora) { cerrar(); boton.focus(); } else pintar();
        });
        if (input.value.startsWith(valor)) { b.classList.add('is-active'); b.setAttribute('aria-pressed','true'); }
        if (valor === (mensual ? iso(hoy()).slice(0,7) : iso(hoy()))) b.classList.add('es-hoy');
        b.disabled = (input.min && valor < input.min.slice(0,valor.length)) || (input.max && valor > input.max.slice(0,valor.length));
        rejilla.append(b);
      }
      panel.append(rejilla);
      if (conHora) {
        const label = document.createElement('label'); label.textContent = 'Hora de la visita'; label.className = 'field';
        const hora = document.createElement('input'); hora.type = 'time'; hora.className = 'input'; hora.value = input.value.slice(11,16) || '12:00';
        hora.addEventListener('change',() => { if(hora.value) aplicar(`${iso(seleccion())}T${hora.value}`); }); label.append(hora); panel.append(label);
      }
      const pie = document.createElement('div'); pie.className = 'fecha-pie'; pie.append(crearBoton('Hoy','Seleccionar hoy',() => { aplicar(conHora ? `${iso(hoy())}T${input.value.slice(11,16) || '12:00'}` : mensual ? iso(hoy()).slice(0,7) : iso(hoy())); cerrar(); boton.focus(); }),crearBoton('Listo','Cerrar calendario',() => { cerrar(); boton.focus(); })); panel.append(pie);
    };
    boton.addEventListener('click',() => { if (!panel.hidden) { cerrar(); return; } vista = seleccion(); pintar(); panel.hidden = false; colocar(); boton.setAttribute('aria-expanded','true'); panel.querySelector('.is-active')?.focus({preventScroll:true}); });
    input.addEventListener('invalid',() => { boton.focus(); });
    window.addEventListener('resize',cerrar);
    document.addEventListener('scroll',(e) => { if(e.target !== panel) cerrar(); },true);
    input.addEventListener('change',rotular);
    document.addEventListener('pointerdown',(e) => { if(!contenedor.contains(e.target)) cerrar(); });
    contenedor.addEventListener('keydown',(e) => {
      if(e.key === 'Escape') { cerrar(); boton.focus(); }
      const dias = [...panel.querySelectorAll('.fecha-dias button:not(:disabled), .fecha-meses button:not(:disabled)')];
      const posicion = dias.indexOf(document.activeElement); const paso = mensual ? 3 : 7;
      const avance = {ArrowRight:1,ArrowLeft:-1,ArrowDown:paso,ArrowUp:-paso}[e.key];
      if(posicion >= 0 && avance) { e.preventDefault(); dias[Math.max(0,Math.min(dias.length-1,posicion+avance))].focus(); }
    });
    rotular();
  });
})();
