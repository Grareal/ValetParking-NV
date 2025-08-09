// Funciones para mostrar/ocultar las vistas
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


// Código que depende de DOMContentLoaded (no necesita ser global)
document.addEventListener("DOMContentLoaded", () => {
    // Cargar reservas inicial sin filtro ni fechas (o con fechas por defecto)
    cargarReservas();

    const filtroInput = document.getElementById("filtroReservas");
    const fechaInicioInput = document.getElementById("fechaInicio");
    const fechaFinInput = document.getElementById("fechaFin");

    // Función para cargar reservas con filtro y fechas
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
                    <td>${item.Hotel || ""}</td>
                    <td>${item.Acompanantes && item.Acompanantes.length > 0 ? item.Acompanantes.join(", ") : "Sin acompañantes"}</td>
                `;
                tbody.appendChild(tr);
            });
        } catch (error) {
            console.error("Error cargando reservas:", error);
        }
    }

    // Evento para filtro por texto en reservas del mes
    if (filtroInput) {
        filtroInput.addEventListener("keyup", () => {
            const texto = filtroInput.value.trim();
            cargarReservas(texto);
        });
    }

    // También carga reservas cuando cambian las fechas
    if (fechaInicioInput) {
        fechaInicioInput.addEventListener("change", () => {
            cargarReservas(filtroInput.value.trim());
        });
    }
    if (fechaFinInput) {
        fechaFinInput.addEventListener("change", () => {
            cargarReservas(filtroInput.value.trim());
        });
    }
});


function toggleAcompanantes(reservaId) {
    const div = document.getElementById('acompanantes-' + reservaId);
    if (!div) return;

    if (div.style.display === 'none' || div.style.display === '') {
        div.style.display = 'block';
    } else {
        div.style.display = 'none';
    }
}
