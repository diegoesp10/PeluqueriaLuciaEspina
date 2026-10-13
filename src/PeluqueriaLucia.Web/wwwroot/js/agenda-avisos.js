(() => {
  const contador = document.querySelector('[data-agenda-contador]');
  if (!contador) return;
  let ultimo = sessionStorage.getItem('lucia-agenda-ultimo'), aviso, inicializado = false;
  const revisar = async () => {
    if (document.hidden) return;
    try {
      const r = await fetch('/admin/agenda/avisos', { credentials:'same-origin', cache:'no-store', redirect:'error' });
      if (!r.ok) return;
      const datos = await r.json();
      contador.textContent = datos.pendientes ? String(datos.pendientes) : '';
      const fecha = datos.ultimaUtc;
      if (fecha && ((ultimo && fecha > ultimo) || (inicializado && !ultimo))) {
        aviso?.remove();
        aviso = document.createElement('div'); aviso.className = 'agenda-notificacion'; aviso.setAttribute('role','status');
        const cerrar = document.createElement('button'); cerrar.type='button'; cerrar.textContent='×'; cerrar.setAttribute('aria-label','Cerrar aviso'); cerrar.addEventListener('click',()=>aviso.remove());
        const texto = document.createElement('strong'); texto.textContent='Ha llegado una nueva solicitud de cita';
        const enlace = document.createElement('a'); enlace.href='/admin/agenda'; enlace.textContent='Revisar la agenda →';
        aviso.append(cerrar,texto,enlace); document.body.append(aviso);
        document.title = 'Nueva cita · ' + document.title.replace(/^Nueva cita · /,'');
      }
      ultimo=fecha;
      if(fecha) sessionStorage.setItem('lucia-agenda-ultimo',fecha);
      else sessionStorage.removeItem('lucia-agenda-ultimo');
      inicializado=true;
    } catch { /* La comprobación de sesión compartida gestiona caducidades y redirecciones. */ }
  };
  revisar(); setInterval(revisar,5000); document.addEventListener('visibilitychange',revisar);
})();
