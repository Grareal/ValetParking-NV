document.addEventListener('DOMContentLoaded', () => {

    const variableFolioLenght = 6;
    const variableReservaLenght = 7;

    const folioInput = document.getElementById('FolioVP');
    const cajonInput = document.getElementById('CajonBuffer');
    const reservaInput = document.getElementById('Reserva');
    const numeroOperadorInput = document.getElementById('NumeroOperadorInput');
    const hotelInput = document.getElementById('Hotel');
    const habitacionInput = document.getElementById('Habitacion');
    const valetNombreInput = document.getElementById('ValetNombreInput');
    const valetNombreHidden = document.getElementById('ValetNombreHidden');
    const estatusField = document.getElementById('Estatus');
    let fetchInProgress = false;
    let ignoreNextEnterInNumero = false;


    //PROBABLEMENTE QUITAR
    reservaInput.addEventListener('keydown', async (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            await actualizarDatosReserva(reservaInput.value);
            ignoreNextEnterInNumero = true;  // Indicamos que vamos a ignorar el siguiente Enter
            numeroOperadorInput.focus();
        }
    });






    async function verificarFolioExiste(folio) {
        try {
            const response = await fetch(`/Botones/VerificarFolio?folio=${encodeURIComponent(folio)}`);
            if (!response.ok) throw new Error("Error al verificar folio");
            const data = await response.json();
            return data.existe;
        } catch (error) {
            console.error("Error al verificar folio:", error);
            return false;
        }
    }

    folioInput.addEventListener('keydown', async (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            const folio = folioInput.value.trim();
            if (!folio) return;

            const existe = await verificarFolioExiste(folio);
            if (existe) {
                alert(` El folio "${folio}" ya existe. Usa uno diferente.`);
                folioInput.classList.add('error');
                setTimeout(() => {
                    folioInput.value = '';
                    folioInput.focus();
                }, 100);
            } else {
                folioInput.classList.remove('error');
                reservaInput.focus();
            }
        }
    });

    function focusOnEnter(source, target) {
        source.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                target.focus();
            }
        });
    }

    // Escáner o escritura rápida - con detección de velocidad
    let folioScanTimer;
    let scanStartTime = null;
    folioInput.addEventListener('input', () => {
        if (!scanStartTime) scanStartTime = Date.now();

        clearTimeout(folioScanTimer);
        folioScanTimer = setTimeout(async () => {
            const folio = folioInput.value.trim();
            const duration = Date.now() - scanStartTime;
            scanStartTime = null;

            //inputfolio caracteres para que de enter automatico [normalmente usan 6 caracteres manuales]
            if (!folio || folio.length < variableFolioLenght) return;

            const existe = await verificarFolioExiste(folio);
            if (existe) {
                alert(` El folio "${folio}" ya existe. Usa uno diferente.`);
                folioInput.value = '';

                folioInput.classList.add('error');
                folioInput.focus();
            } else {
                folioInput.classList.remove('error');
                if (duration <= 500) reservaInput.focus(); // solo avanza si fue escaneo rápido
            }
        }, 300);
    });

    if (folioInput && reservaInput) focusOnEnter(folioInput, reservaInput);
     // if (reservaInput && cajonInput) focusOnEnter(reservaInput, cajonInput);
    if (reservaInput && numeroOperadorInput) focusOnEnter(reservaInput, numeroOperadorInput);
    if (cajonInput && numeroOperadorInput) focusOnEnter(cajonInput, numeroOperadorInput);


    let reservaScanTimer;
    let scanStartTimeReserva = null;

    if (reservaInput && numeroOperadorInput) {

        reservaInput.addEventListener('input', () => {
            if (!scanStartTimeReserva) scanStartTimeReserva = Date.now();

            clearTimeout(reservaScanTimer);
            reservaScanTimer = setTimeout(async () => {
                const reserva = reservaInput.value.trim();
                const duration = Date.now() - scanStartTimeReserva;
                scanStartTimeReserva = null;
                //     //Normalmente la reserva son 7 caracteres // // 
                if (reserva.length >= variableReservaLenght && duration <= 500) {
                    await actualizarDatosReserva(reserva);   
                    numeroOperadorInput.focus();             
                }
            }, 300);
        });


    }


   /* if (reservaInput) {
        reservaInput.addEventListener('change', () => {
            actualizarDatosReserva(reservaInput.value);
        });

        reservaInput.addEventListener('keydown', async (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                await actualizarDatosReserva(reservaInput.value);
                cajonInput.focus(); 
            }
        });

    }*/

    async function actualizarDatosReserva(reserva) {
        try {
            const response = await fetch(`/Botones/ObtenerReserva?confirmacion=${reserva}`);
            if (!response.ok) {
                throw new Error('Error al obtener los datos');
            }

            const data = await response.json();

            if (data && data.hotel && data.habitacion) {
                document.getElementById('Hotel').value = data.hotel;
                document.getElementById('Habitacion').value = data.habitacion;
            } else {
                alert('Reserva no encontrada o datos incompletos');
            }

        } catch (error) {
            console.error('Error en actualizarDatosReserva:', error);
           // alert('Error al obtener la reserva');
        }
    }


    if (numeroOperadorInput) {
        numeroOperadorInput.addEventListener('input', async () => {
            const id = numeroOperadorInput.value.trim();

            if (id.length === 0) {
                valetNombreInput.value = '';
                valetNombreHidden.value = '';
                return;
            }

            fetchInProgress = true;
            try {
                const response = await fetch(`/api/Empleados/${id}`);
                if (!response.ok) throw new Error("No encontrado");

                const data = await response.json();
                const nombre = data.nombre || '';
                valetNombreInput.value = nombre;
                valetNombreHidden.value = nombre;
            } catch (error) {
                valetNombreInput.value = '';
                valetNombreHidden.value = '';
                console.warn("No se encontró el operador:", error);
            }
            fetchInProgress = false;
        });
    }

    const mensaje = document.getElementById('mensajeConfirmacion');
    if (mensaje) {
        setTimeout(() => {
            mensaje.style.transition = 'opacity 0.5s ease';
            mensaje.style.opacity = '0';
            setTimeout(() => {
                mensaje.remove();
            }, 500);
        }, 2000);
    }

    (() => {
        let buffer = '';
        let typingTimeout = null;
        let firstKeyTime = 0;
        let lastKeyTime = 0;
        const typingSpeedThreshold = 50;
        const maxScanDuration = 500;
        const numeroInput = document.getElementById('NumeroOperadorInput');

        document.addEventListener('keydown', async function (event) {
            const active = document.activeElement;
            const isInput = active && (active.tagName === 'INPUT' || active.tagName === 'TEXTAREA');

            if (!isInput || active !== numeroInput) return;

            const now = Date.now();

            if (event.key === 'Enter') {
                event.preventDefault();

                 if (ignoreNextEnterInNumero) {
                    ignoreNextEnterInNumero = false;
                    return;
                }

                if (buffer.length === 0) {
                    if (valetNombreHidden.value.trim() === '') {
                         return;
                    }
                    numeroInput.form.submit();
                    return;
                }

                await processBuffer();
                buffer = '';
                return;
            }



            if (/^[0-9a-zA-Z]$/.test(event.key)) {
                if (buffer.length === 0) {
                    firstKeyTime = now;
                }
                if (now - lastKeyTime < typingSpeedThreshold) {
                    buffer += event.key;
                } else {
                    buffer = event.key;
                    firstKeyTime = now;
                }
                lastKeyTime = now;

                if (typingTimeout) clearTimeout(typingTimeout);
                typingTimeout = setTimeout(async () => {
                    const duration = lastKeyTime - firstKeyTime;
                    console.log('Tiempo buffer:', duration, 'ms, buffer:', buffer);

                    if (duration <= maxScanDuration && buffer.length >= 7) {
                        console.log('Entrada scanner detectada, procesando buffer...');
                        await processBuffer();
                    } else {
                        console.log("Entrada manual detectada o buffer muy corto, NO se envía automáticamente.");
                    }
                    buffer = '';
                }, 7000);

            } else {
                buffer = '';
            }
        });
        
        async function processBuffer() {
            console.log('Procesando buffer:', buffer);
            numeroInput.value = buffer.trim();
            numeroInput.dispatchEvent(new Event('input'));

            while (window.fetchInProgress) {
                await new Promise(resolve => setTimeout(resolve, 50));
            }

            await new Promise(resolve => setTimeout(resolve, 100));

            if (document.getElementById('ValetNombreHidden').value.trim() !== '') {
                console.log('Enviando formulario automáticamente');
                const form = numeroInput.form;
                if (form) form.submit();
            } else {
                console.warn('Nombre de valet no disponible, no se envía formulario');
            }
        }

    })();

});
