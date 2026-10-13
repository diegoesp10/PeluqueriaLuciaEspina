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
    const { texto } = window.Peluqueria;
    const fallo = () => texto('contactoError', 'No se ha podido verificar el contacto. Puedes completar tus datos manualmente.');
    // Contrato: POST con language y antifalsificación → 200 JSON; 400 { mensaje }; 429 texto plano con Retry-After.
    async function enviar(url, valores, signal) {
        const respuesta = await window.Peluqueria.enviar(url, { ...valores,
            __RequestVerificationToken: form.elements.namedItem('__RequestVerificationToken').value }, { signal });
        if (respuesta.status === 429) throw new Error(await respuesta.text() || fallo());
        if (!respuesta.headers.get('content-type')?.includes('application/json')) throw new Error(fallo());
        const datos = await respuesta.json();
        if (!respuesta.ok) throw new Error(datos.mensaje || fallo());
        return datos;
    }
    async function solicitar(forzar = false) {
        const movil = normalizar(telefono.value);
        if (!movil || (movil === movilSolicitado && !forzar)) return;
        const actual = ++revision; peticion?.abort(); peticion = new AbortController();
        movilSolicitado = movil; reenviar.disabled = true;
        try {
            const datos = await enviar(form.dataset.urlSolicitar, { telefono: movil }, peticion.signal);
            if (actual !== revision) return;
            desafio = datos.id; panel.hidden = false;
            avisar(texto('contactoEnviado'));
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
        if (!desafio || !/^[0-9]{6}$/.test(codigo.value)) { avisar(texto('contactoCodigo')); return; }
        const actual = ++revision; peticion?.abort(); peticion = new AbortController(); verificar.disabled = true;
        try {
            const datos = await enviar(form.dataset.urlVerificar, { id: desafio, codigo: codigo.value }, peticion.signal);
            if (actual !== revision) return;
            const sexo = ['Hombre', 'Mujer', 'Otro'][datos.sexo];
            relleno = { Nombre: datos.nombre, Apellido: datos.apellido, Correo: datos.correo, Sexo: sexo };
            for (const nombre of ['Nombre', 'Apellido', 'Correo']) campo(nombre).value = relleno[nombre];
            for (const radio of form.querySelectorAll('input[name="Formulario.Sexo"]')) radio.checked = radio.value === sexo;
            desafio = null; codigo.value = ''; panel.hidden = true;
            avisar(texto('contactoVerificado'));
        } catch (error) { if (error.name !== 'AbortError' && actual === revision) avisar(error.message); }
        finally { verificar.disabled = false; }
    });
})();
