// Reserva pública: si ya eres cliente, tus datos se rellenan solos.
// Al salir del móvil o del correo, en cuanto los dos son válidos, se pide un código: el servidor busca la ficha por el
// móvil y, si no, por el correo, y el código llega al correo de esa ficha (la respuesta es la misma exista o no: nadie
// ve datos ajenos escribiendo un teléfono o un correo). Se pide con los dos a la vez para enviar un único código aunque
// el cliente haya cambiado de móvil. Al teclear las seis cifras se verifica solo y se rellenan nombre, apellidos, móvil,
// correo y para quién es la cita.
(() => {
    const form = document.querySelector('[data-contacto-publico]');
    if (!form || form.dataset.contactoPublico !== 'true') return;
    const campo = nombre => form.elements.namedItem('Formulario.' + nombre);
    const telefono = campo('Telefono'), correo = campo('Correo'), estado = form.querySelector('[data-contacto-estado]');
    const panel = form.querySelector('[data-contacto-desafio]'), codigo = form.querySelector('#codigo-contacto');
    const verificar = form.querySelector('[data-contacto-verificar]'), reenviar = form.querySelector('[data-contacto-solicitar]');
    let desafio, ultimo, revision = 0, peticion, relleno;
    // El sexo de la reserva es «¿Para quién es la cita?» (agenda-publica.js); marcarlo avisa con change para que filtre
    const para = valor => document.querySelector(`input[name="para"][value="${valor}"]`);
    const marcarPara = (valor, marcado) => {
        const radio = para(valor);
        if (!radio) return;
        radio.checked = marcado;
        radio.dispatchEvent(new Event('change', { bubbles: true }));
    };
    const normalizar = valor => {
        let n = valor.replace(/[ ()-]/g, '');
        if (n.startsWith('0034')) n = '+34' + n.slice(4);
        if (/^[67][0-9]{8}$/.test(n)) n = '+34' + n;
        return /^\+34[67][0-9]{8}$/.test(n) ? n : null;
    };
    const correoValido = () => {
        const valor = correo.value.trim().toLowerCase();
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(valor) ? valor : null;
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

    /** Pide el código con el móvil y el correo escritos; sin los dos válidos, todavía no. */
    async function solicitar(forzar = false) {
        const movil = normalizar(telefono.value), email = correoValido();
        if (!movil || !email) return;
        const clave = `${movil}|${email}`;
        if (clave === ultimo && !forzar) return;
        const actual = ++revision; peticion?.abort(); peticion = new AbortController();
        ultimo = clave; reenviar.disabled = true;
        try {
            const datos = await enviar(form.dataset.urlSolicitar, { telefono: movil, correo: email }, peticion.signal);
            if (actual !== revision) return;
            desafio = datos.id; panel.hidden = false;
            avisar(texto('contactoEnviado'));
        } catch (error) { if (error.name !== 'AbortError' && actual === revision) avisar(error.message); }
        finally { reenviar.disabled = false; }
    }
    const cancelarCodigo = () => {
        ++revision; peticion?.abort(); desafio = null; ultimo = null; panel.hidden = true; estado.hidden = true; codigo.value = '';
    };

    async function comprobar() {
        if (!desafio || !/^[0-9]{6}$/.test(codigo.value)) { avisar(texto('contactoCodigo')); return; }
        const actual = ++revision; peticion?.abort(); peticion = new AbortController(); verificar.disabled = true;
        try {
            const datos = await enviar(form.dataset.urlVerificar, { id: desafio, codigo: codigo.value }, peticion.signal);
            if (actual !== revision) return;
            const sexo = ['Hombre', 'Mujer', 'Otro'][datos.sexo];
            relleno = { Nombre: datos.nombre, Apellido: datos.apellido, Correo: datos.correo, Sexo: sexo };
            for (const [nombre, valor] of Object.entries(relleno)) if (nombre !== 'Sexo') campo(nombre).value = valor;
            if (sexo) marcarPara(sexo, true);
            desafio = null; codigo.value = ''; panel.hidden = true;
            avisar(texto('contactoVerificado'));
        } catch (error) { if (error.name !== 'AbortError' && actual === revision) avisar(error.message); }
        finally { verificar.disabled = false; }
    }

    telefono.addEventListener('blur', () => solicitar());
    correo.addEventListener('blur', () => solicitar());
    telefono.addEventListener('input', () => {
        cancelarCodigo();
        // Otro móvil es otra persona: lo rellenado de la ficha anterior se quita
        if (relleno) {
            for (const [nombre, valor] of Object.entries(relleno)) {
                if (nombre === 'Sexo') {
                    if (para(valor)?.checked) marcarPara(valor, false);
                } else if (campo(nombre).value === valor) campo(nombre).value = '';
            }
            relleno = null;
        }
    });
    // Cambiar el correo con el que se pidió el código lo anula: al salir del campo se pide otro
    correo.addEventListener('input', () => { if (desafio || ultimo) cancelarCodigo(); });
    // Al teclear la sexta cifra se comprueba solo
    codigo.addEventListener('input', () => {
        codigo.value = codigo.value.replace(/[^0-9]/g, '').slice(0, 6);
        if (codigo.value.length === 6) comprobar();
    });
    reenviar.addEventListener('click', () => solicitar(true));
    verificar.addEventListener('click', comprobar);
})();
