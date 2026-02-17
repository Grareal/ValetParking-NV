// ==================================================
// Mostrar/ocultar vistas
// ==================================================
function mostrarVista(idVista) {
    const vistas = ['vistaLlegadasDia', 'vistaReservasMes'];
    vistas.forEach(vista => {
        const elemento = document.getElementById(vista);
        if (elemento) {
            elemento.classList.toggle('oculto', vista !== idVista);
        }
    });
}

function mostrarVistaAlCargar(vistaActual) {
    if (!vistaActual || !['vistaLlegadasDia', 'vistaReservasMes'].includes(vistaActual)) {
        mostrarVista('vistaLlegadasDia'); // vista por defecto
    } else {
        mostrarVista(vistaActual);
    }
}

// ==================================================
// Filtrar tabla por input
// ==================================================
function filtrarTablaPorId(idInput, idTabla) {
    const input = document.getElementById(idInput);
    const filter = input.value.toLowerCase();
    const table = document.getElementById(idTabla);
    const trs = table.tBodies[0].getElementsByTagName("tr");

    for (let i = 0; i < trs.length; i++) {
        const rowText = trs[i].textContent.toLowerCase();
        trs[i].style.display = rowText.includes(filter) ? "" : "none";
    }
}

window.mostrarVista = mostrarVista;
window.mostrarVistaAlCargar = mostrarVistaAlCargar;
window.filtrarTablaPorId = filtrarTablaPorId;

// ==================================================
// DOMContentLoaded
// ==================================================
document.addEventListener("DOMContentLoaded", () => {

    const filtroInput = document.getElementById("filtroReservasMes");
    const fechaInicioInput = document.getElementById("fechaInicio");
    const fechaFinInput = document.getElementById("fechaFin");

    async function cargarReservas(texto = "") {
        const fechaInicio = fechaInicioInput ? fechaInicioInput.value : "";
        const fechaFin = fechaFinInput ? fechaFinInput.value : "";

        const url = `/Reservas/BuscarReservas?texto=${encodeURIComponent(texto)}&fechaInicio=${encodeURIComponent(fechaInicio)}&fechaFin=${encodeURIComponent(fechaFin)}`;

        try {
            const response = await fetch(url);
            const data = await response.json();

            const tbody = document.querySelector("#tablaReservas tbody");
            tbody.innerHTML = "";

            if (!data || data.length === 0) {
                tbody.innerHTML = "<tr><td colspan='8'>No se encontraron reservas.</td></tr>";
                return;
            }

            data.forEach(item => {
                const tr = document.createElement("tr");
                tr.innerHTML = `
                    <td>${item.h_status}</td>
                    <td>${item.h_res_cve}</td>
                    <td>${item.h_cod_reserva}</td>
                    <td>${item.h_nom}</td>
                    <td>${item.h_fec_lld}</td>
                    <td>${item.h_fec_sda}</td>
                    <td>${item.hotel ?? ""}</td>
                <td>${item.acompanantesTexto ?? "Sin acompañantes"}</td>

                `;
                tbody.appendChild(tr);
            });

        } catch (error) {
            console.error("Error cargando reservas:", error);
        }
    }

    // Cargar reservas inicial
    cargarReservas();

    // Eventos de filtro
    if (filtroInput) {
        filtroInput.addEventListener("keyup", () => {
            cargarReservas(filtroInput.value.trim());
        });
    }

    if (fechaInicioInput) {
        fechaInicioInput.addEventListener("change", () => {
            cargarReservas(filtroInput ? filtroInput.value.trim() : "");
        });
    }

    if (fechaFinInput) {
        fechaFinInput.addEventListener("change", () => {
            cargarReservas(filtroInput ? filtroInput.value.trim() : "");
        });
    }
});
