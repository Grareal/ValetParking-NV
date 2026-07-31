# Plan de roles para la app movil Valet Parking

## Contrato de roles

La API de inicio de sesion (`POST /Account/LoginApi`) mantiene el campo `funciones`.
La app debe interpretar un unico rol canonico por usuario:

- `OperadoraValet`: operadora y valet.
- `Administracion`: administradores.
- `TI`: tecnologias de informacion.

Durante una version de transicion, la app puede mapear los tokens antiguos
`Operadora`, `Botones`, `Movimientos`, `PuertaSol`, `Reportes` y `Configuracion`
a `OperadoraValet`. El servidor migra automaticamente los registros existentes,
por lo que este fallback puede retirarse despues de confirmar el despliegue.

## Navegacion para Operadora / Valet

Debe mostrar exclusivamente:

1. Inicio.
2. Movimientos.
3. Registro.
4. Reservacion.
5. Estacionar.
6. Detalles del vehiculo.
7. Vincular.
8. Salida de vehiculo.
9. Configuracion.
10. Cerrar sesion.

Las pantallas de detalle no necesitan aparecer como opcion principal; pueden
abrirse desde el flujo que corresponda, pero deben aplicar la misma validacion
de rol en sus rutas.

## Administradores y TI

`Administracion` y `TI` pueden ver todas las pantallas operativas. Las futuras
pantallas administrativas deben habilitarse mediante una matriz local de
capacidades, no mediante comparaciones dispersas en cada widget.

## Implementacion sugerida en Flutter

1. Normalizar `funciones` una sola vez al deserializar la respuesta de login.
2. Crear un `enum AppRole { operadoraValet, administracion, ti }`.
3. Definir una tabla `AppRole -> Set<AppCapability>`.
4. Construir el menu desde capacidades y proteger tambien el router; ocultar
   una opcion no sustituye el control de navegacion.
5. Persistir solo el rol normalizado y los datos de sesion estrictamente
   necesarios.
6. Agregar pruebas de menu y rutas para cada rol.

## Siguiente evolucion

Cuando se requiera configurar tambien las pantallas moviles desde la web, la
API debera devolver un arreglo de capacidades moviles administrado desde BD.
Hasta entonces, este contrato mantiene la app estable y desacoplada de los
nombres historicos del sistema web.
