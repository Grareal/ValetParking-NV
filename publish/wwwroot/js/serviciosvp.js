function cargarOpcionesServicio(selectId, jsonPath, valorPreseleccionado = null) {
    fetch(jsonPath)
        .then(response => response.json())
        .then(data => {
            const select = document.getElementById(selectId);
            if (!select) return;

            // Limpiar por si acaso
            select.innerHTML = '';

            data.forEach(opcion => {
                const option = document.createElement('option');
                option.value = opcion.value;
                option.text = opcion.text;
                select.appendChild(option);
            });

            if (valorPreseleccionado) {
                select.value = valorPreseleccionado;
            }
        })
        .catch(error => console.error('Error cargando opciones:', error));
}
