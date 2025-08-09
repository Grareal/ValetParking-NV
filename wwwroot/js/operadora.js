
function limpiarCamposOperadora() {
    const inputCodigo = document.getElementById('codigoOperador');
    const spanNombre = document.getElementById('nombreEmpleado');

    if (inputCodigo) inputCodigo.value = '';
    if (spanNombre) spanNombre.textContent = '';

    localStorage.removeItem("codigoOperador");
    localStorage.removeItem("nombreEmpleado");
}

document.addEventListener("DOMContentLoaded", function () {
    // Funcionalidad filtro tabla
    const inputFiltro = document.getElementById("filtroTabla");
    const tabla = document.getElementById("tablaOperadora");
    const filas = tabla.querySelectorAll("tbody tr");

    inputFiltro.addEventListener("input", function () {
        const textoFiltro = this.value.toLowerCase();

        filas.forEach(fila => {
            const textoFila = fila.textContent.toLowerCase();
            fila.style.display = textoFila.includes(textoFiltro) ? "" : "none";
        });
    });

    document.querySelectorAll(".btn-ver-movimientos").forEach(button => {
        button.addEventListener("click", function () {
            const idRegistro = this.getAttribute("data-id");

            fetch(`/Operadora/ObtenerMovimientos?idRegistro=${idRegistro}`)
                .then(response => response.json())

                .then(movimientos => {
                    let contenido = `
        <div style="font-family: Arial; max-height: 400px; overflow-y: auto;">
            <h3 style="margin-bottom: 15px;">Movimientos del registro #${idRegistro}</h3>
            <table style="width: 100%; border-collapse: collapse; font-size: 14px;">
                <thead>
                    <tr style="background-color: #f2f2f2;">
                        <th style="padding: 8px; border: 1px solid #ccc;">Fecha</th>
                        <th style="padding: 8px; border: 1px solid #ccc;">Operador</th>
                        <th style="padding: 8px; border: 1px solid #ccc;">Servicio</th>
                        <th style="padding: 8px; border: 1px solid #ccc;">Movimiento</th>
                    </tr>
                </thead>
                <tbody>`;

                    if (movimientos.length === 0) {
                        contenido += `<tr><td colspan="4" style="text-align:center; padding: 10px;">No hay movimientos registrados.</td></tr>`;
                    } else {
                        movimientos.forEach(m => {
                            const fecha = new Date(m.fechaHora).toLocaleString();
                            contenido += `
                <tr>
                    <td style="padding: 8px; border: 1px solid #ccc;">${fecha}</td>
                    <td style="padding: 8px; border: 1px solid #ccc;">${m.operador}</td>
                    <td style="padding: 8px; border: 1px solid #ccc;">${m.servicio}</td>
                    <td style="padding: 8px; border: 1px solid #ccc;">${m.movimientoTexto}</td>
                </tr>`;
                        });
                    }

                    contenido += `</tbody></table></div>`;

                    const nuevaVentana = window.open("", "_blank", "width=700,height=500");
                    nuevaVentana.document.write(`
        <html>
            <head><title>Movimientos Registro #${idRegistro}</title></head>
            <body style="padding: 20px;">${contenido}</body>
        </html>`);
                    nuevaVentana.document.close();
                })


                .catch(error => {
                    console.error("Error al obtener movimientos:", error);
                    alert("No se pudieron cargar los movimientos.");
                });
        });
    });

    const inputCodigo = document.getElementById("codigoOperador");
    const spanNombre = document.getElementById("nombreEmpleado");

    const codigoGuardado = localStorage.getItem("codigoOperador");
    const nombreGuardado = localStorage.getItem("nombreEmpleado");

    if (codigoGuardado) inputCodigo.value = codigoGuardado;
    if (nombreGuardado) spanNombre.textContent = nombreGuardado;

    inputCodigo.addEventListener("change", function () {
        const codigo = inputCodigo.value.trim();

        if (codigo.length === 0) {
            spanNombre.textContent = "";
            localStorage.removeItem("codigoOperador");
            localStorage.removeItem("nombreEmpleado");
            return;
        }

        fetch(`/api/Empleados/BuscarPorCodigo?codigo=${encodeURIComponent(codigo)}`)
            .then(response => {
                if (!response.ok) throw new Error("Empleado no encontrado");
                return response.json();
            })
            .then(data => {
                const nombre = data.nombreCompleto || "Sin nombre";
                spanNombre.textContent = nombre;
                localStorage.setItem("codigoOperador", codigo);
                localStorage.setItem("nombreEmpleado", nombre);
            })
            .catch(error => {
                console.error(error);
                spanNombre.textContent = "Empleado no encontrado";
                localStorage.setItem("nombreEmpleado", "Empleado no encontrado");
            });
    });

    document.querySelectorAll("form").forEach(form => {
        form.addEventListener("submit", function (event) {
            const nombreEmpleado = document.getElementById("nombreEmpleado").textContent.trim();
            const hiddenInput = form.querySelector(".input-operadora-nombre");

            if (hiddenInput) {
                hiddenInput.value = nombreEmpleado;
                console.log("Valor Operacion seteado en input hidden:", hiddenInput.value);
            } else {
                console.log("No se encontró input oculto en el form");
            }
        });
    });
 



});
