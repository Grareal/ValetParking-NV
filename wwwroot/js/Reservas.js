function filtrarTabla() {
    const input = document.getElementById("filtroTabla");
    const filter = input.value.toLowerCase();
    const table = document.getElementById("tablaReservas");
    const trs = table.getElementsByTagName("tr");

    for (let i = 1; i < trs.length; i++) {  
        const tds = trs[i].getElementsByTagName("td");
        let visible = false;

        for (let j = 0; j < tds.length; j++) {
            if (tds[j].textContent.toLowerCase().indexOf(filter) > -1) {
                visible = true;
                break;
            }
        }

        trs[i].style.display = visible ? "" : "none";
    }
}
