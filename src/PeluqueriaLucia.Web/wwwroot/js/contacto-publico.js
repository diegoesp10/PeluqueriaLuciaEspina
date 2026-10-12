(() => {
    const form = document.querySelector('[data-contacto-publico]');
    if (!form || form.dataset.contactoPublico !== 'true') return;
    const campo = nombre => form.elements.namedItem('Formulario.' + nombre);
    const telefono = campo('Telefono'), estado = form.querySelector('[data-contacto-estado]');
    const panel = form.querySelector('[data-contacto-desafio]'), codigo = form.querySelector('#codigo-contacto');
    const verificar = form.querySelector('[data-contacto-verificar]'), reenviar = form.querySelector('[data-contacto-solicitar]');
    let desafio, movilSolicitado, revision = 0, peticion, relleno;
    const normalizar = valor => {
        let n = valor.replace(/[ ()-]/g, '');
        if (n.startsWith('0034')) n = '+34' + n.slice(4);
        if (/^[67][0-9]{8}$/.test(n)) n = '+34' + n;
        return /^\+34[67][0-9]{8}$/.test(n) ? n : null;
    };
    const avisar = texto => { estado.hidden = false; estado.textContent = texto; };
    async function enviar(url, valores, signal) {
        const respuesta = await fetch(url, { method: 'POST', credentials: 'same-origin', signal,
            headers: { Accept: 'application/json' }, body: new URLSearchParams({ ...valores,
                __RequestVerificationToken: form.elements.namedItem('__RequestVerificationToken').value }) });
        if (!respuesta.headers.get('content-type')?.includes('application/json')) throw new Error('No se ha podido verificar el contacto. Puedes completar tus datos manualmente.');
        const datos = await respuesta.json();
        if (!respuesta.ok) throw new Error(datos.mensaje || 'No se ha podido verificar el contacto. Puedes completar tus datos manualmente.');
        return datos;
    }
    async function solicitar(forzar = false) {
        const movil = normalizar(telefono.value);
        if (!movil || (movil === movilSolicitado && !forzar)) return;
        const actual = ++revision; peticion?.abort(); peticion = new AbortController();
        movilSolicitado = movil; reenviar.disabled = true;
        try {
            const datos = await enviar('/citas/contacto/solicitar', { telefono: movil }, peticion.signal);
            if (actual !== revision) return;
            desafio = datos.id; panel.hidden = false;
            avisar('Si el teléfono corresponde a una ficha con correo, recibirás un código en ese correo. También puedes continuar rellenando los datos manualmente.');
        } catch (error) { if (error.name !== 'AbortError' && actual === revision) avisar(error.message); }
        finally { reenviar.disabled = false; }
    }
    telefono.addEventListener('blur', () => solicitar());
    telefono.addEventListener('input', () => {
        ++revision; peticion?.abort(); desafio = null; movilSolicitado = null; panel.hidden = true; estado.hidden = true; codigo.value = '';
        if (relleno) {
            for (const [nombre, valor] of Object.entries(relleno)) {
                if (nombre === 'Sexo') {
                    const radio = form.querySelector('input[name="Formulario.Sexo"]:checked');
                    if (radio && radio.value === valor) radio.checked = false;
                } else if (campo(nombre).value === valor) campo(nombre).value = '';
            }
            relleno = null;
        }
    });
    reenviar.addEventListener('click', () => solicitar(true));
    verificar.addEventListener('click', async () => {
        if (!desafio || !/^[0-9]{6}$/.test(codigo.value)) { avisar('Introduce el código de seis dígitos recibido por correo.'); return; }
        const actual = ++revision; peticion?.abort(); peticion = new AbortController(); verificar.disabled = true;
        try {
            const datos = await enviar('/citas/contacto/verificar', { id: desafio, codigo: codigo.value }, peticion.signal);
            if (actual !== revision) return;
            const sexo = ['Hombre', 'Mujer', 'Otro'][datos.sexo];
            relleno = { Nombre: datos.nombre, Apellido: datos.apellido, Correo: datos.correo, Sexo: sexo };
            for (const nombre of ['Nombre', 'Apellido', 'Correo']) campo(nombre).value = relleno[nombre];
            for (const radio of form.querySelectorAll('input[name="Formulario.Sexo"]')) radio.checked = radio.value === sexo;
            desafio = null; codigo.value = ''; panel.hidden = true;
            avisar('Contacto verificado. Tus datos se han completado; revísalos antes de solicitar la cita.');
        } catch (error) { if (error.name !== 'AbortError' && actual === revision) avisar(error.message); }
        finally { verificar.disabled = false; }
    });
})();
