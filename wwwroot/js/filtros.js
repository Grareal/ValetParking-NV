// Función para filtrar tabla por input y tabla (por id)
function filtrarTablaPorId(inputId, tablaId) {
    var input = document.getElementById(inputId);
    var filtro = input.value.toLowerCase();
    var tabla = document.getElementById(tablaId);
    var filas = tabla.getElementsByTagName("tr");

    for (var i = 1; i < filas.length; i++) {
        var celdas = filas[i].getElementsByTagName("td");
        var mostrar = false;

        for (var j = 0; j < celdas.length; j++) {
            if (celdas[j].innerText.toLowerCase().includes(filtro)) {
                mostrar = true;
                break;
            }
        }

        filas[i].style.display = mostrar ? "" : "none";
    }
}

// Función para mostrar la vista correcta al cargar
function mostrarVistaAlCargar(vistaActual) {
    if (vistaActual === "reservas") {
        mostrarVista('vistaReservasMes');
    } else {
        mostrarVista('vistaLlegadasDia');
    }
}

// Función para mostrar una vista y ocultar la otra
function mostrarVista(idVista) {
    document.getElementById('vistaLlegadasDia').classList.add('oculto');
    document.getElementById('vistaReservasMes').classList.add('oculto');

    document.getElementById(idVista).classList.remove('oculto');
}
