(() => {
    const form = document.querySelector('form[data-cliente-reconocer]');
    const url = form?.dataset.clienteReconocer;
    if (!url) return;
    const telefono = form.elements.namedItem('Telefono');
    const correo = form.elements.namedItem('Email');
    const estado = form.querySelector('[data-cliente-estado]');
    const guardar = form.querySelector('.acciones-form button');
    const textoGuardar = guardar.textContent;
    let sexoRelleno;
    let peticion, revision = 0, temporizador, relleno;
    async function reconocer() {
        const actual = ++revision;
        peticion?.abort();
        if (!telefono.value.trim() || !correo.value.trim() || !correo.validity.valid) {
            estado.hidden = true;
            return;
        }
        peticion = new AbortController();
        const datos = new URLSearchParams({ telefono: telefono.value, email: correo.value,
            __RequestVerificationToken: form.elements.namedItem('__RequestVerificationToken').value });
        try {
            const respuesta = await fetch(url, { method: 'POST', body: datos, signal: peticion.signal,
                credentials: 'same-origin', headers: { 'Accept': 'application/json' } });
            if (!respuesta.ok || !respuesta.headers.get('content-type')?.includes('application/json')) throw new Error();
            const cliente = await respuesta.json();
            if (actual !== revision) return;
            estado.hidden = false;
            if (!cliente) { guardar.textContent = textoGuardar; estado.textContent = 'No hay una coincidencia exacta. Completa los datos del cliente.'; return; }
            relleno = { Nombre: cliente.nombre, Apellido: cliente.apellido, Apodo: cliente.apodo || '' };
            form.elements.namedItem('Nombre').value = cliente.nombre;
            form.elements.namedItem('Apellido').value = cliente.apellido;
            form.elements.namedItem('Apodo').value = cliente.apodo || '';
            sexoRelleno = ['Hombre', 'Mujer', 'Otro'][cliente.sexo];
            guardar.textContent = textoGuardar.includes('visita') ? 'Guardar ficha y seguir con la visita' : 'Guardar ficha existente';
            for (const radio of form.querySelectorAll('input[name="Sexo"]'))
                radio.checked = radio.value === ['Hombre', 'Mujer', 'Otro'][cliente.sexo];
            estado.textContent = 'Ficha #' + cliente.id + ' reconocida. Puedes revisar sus datos; al guardar se actualizará esta ficha.';
        } catch (error) {
            if (error.name === 'AbortError' || actual !== revision) return;
            estado.hidden = false;
            estado.textContent = 'No se ha podido comprobar el contacto. Puedes completar el formulario y volver a intentarlo.';
        }
    }
    for (const campo of [telefono, correo]) campo.addEventListener('input', () => {
        ++revision; peticion?.abort(); clearTimeout(temporizador); estado.hidden = true;
        guardar.textContent = textoGuardar;
        const radio = form.querySelector('input[name="Sexo"]:checked');
        if (radio && radio.value === sexoRelleno) radio.checked = false;
        sexoRelleno = null;
        if (relleno) {
            for (const [nombre, valor] of Object.entries(relleno))
                if (form.elements.namedItem(nombre).value === valor) form.elements.namedItem(nombre).value = '';
            relleno = null;
        }
        temporizador = setTimeout(reconocer, 400);
    });
})();
