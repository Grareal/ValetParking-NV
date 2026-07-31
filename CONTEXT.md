# CONTEXT.md — AppValetParking Backend

> Memoria técnica y funcional del backend ASP.NET Core. Complementa `C:\ValetParking\AI_CONTEXT.md`. Actualizado el 2026-07-17.

## Repositorios y roles

- Backend web/API: `C:\ValetParking\AppValetParking`.
- App Flutter: `C:\Users\adminemeza\Desktop\Movil App valet\valet_parking_app_nuevo`.
- Todo cambio al contrato `api/*` debe coordinarse con Flutter.
- Roles canónicos: `OperadoraValet`, `Administracion` y `TI`. Los nombres históricos sólo son compatibilidad temporal.

## Solicitudes y bloqueo por QR

- Tomar una solicitud asigna el valet y deja `EstadoPaso = EN_PROCESO`, pero todavía no lo bloquea.
- Registrar vehículos continúa permitido; este control sólo limita trabajos de estacionamiento.
- El bloqueo global comienza después de asignar cajón, cuando Flutter llama `POST api/solicitudes/pendienteQr/{id}` y queda `EstadoPaso = PENDIENTE_QR`.
- Un valet con un `PENDIENTE_QR` no puede tomar otro estacionamiento hasta validar el QR o hasta que intervenga un supervisor.
- Completar/cerrar limpia `EstadoPaso` y `FechaPendienteQr`.
- La validación definitiva está en el servidor; ocultar acciones en Flutter no la sustituye.

## Supervisión y auditoría

Sólo `TI` y `Administracion` pueden usar:

- `GET api/supervision/solicitudes-pendientes`: lista pendientes y filtra por folio.
- `POST api/supervision/solicitudes/{id}/liberar`: devuelve a la fila y quita la asignación.
- `POST api/supervision/solicitudes/{id}/reasignar`: conserva el QR pendiente y cambia el responsable. El destino no puede tener otro QR pendiente.
- `POST api/supervision/solicitudes/{id}/cancelar`: cancela sin liberar automáticamente el cajón físico.
- `POST api/supervision/solicitudes/{id}/discrepancia`: registra una observación sin alterar estado, dueño ni bloqueo.

Todas las acciones requieren `SupervisorGafete` y `Motivo`. Se registra actor, fecha, motivo y valores anterior/nuevo en `SolicitudesAuditoria`.

## Alerta de checkout

- `GET api/solicitudes` cruza la reserva más reciente del folio con TCADBOPE mediante `TcabdopeNewDbContext`.
- La fuente es `Reserva.h_fec_sda`, formato `yyyyMMdd`.
- `CheckoutHoy` y `CheckoutFecha` sólo se devuelven cuando la fecha coincide exactamente con el día actual del servidor.
- Sólo aplica a `PASEO`/`PARCIAL`; no alerta fechas pasadas o futuras.
- TCADBOPE es de sólo lectura.

## Flujo ligero VISITA

- Flutter crea `TipoSalida = VISITA` y folio prefijado `VIS-`.
- No exige ni crea registro de vehículo.
- Aparece en Movimientos, se toma y termina con “Confirmar atención”; después desaparece de activas.

## Persistencia y archivos clave

- `ValetSolicitud`: `EstadoPaso` y `FechaPendienteQr`.
- `SolicitudAuditoria`: evidencia de intervenciones.
- `Sql/SolicitudesSupervision.sql`: cambio idempotente de esquema.
- `Services/DatabaseInitializer.cs`: ejecuta el script al arrancar. Verificar respaldo y permisos DDL antes del primer despliegue.
- Lógica: `Controllers/ControlSolicitudesController .cs` (el nombre contiene un espacio antes de `.cs`).
- Modelos: `Models/ValetSolicitud.cs`, `Models/SolicitudAuditoria.cs`.
- Mapeo: `Data/ApplicationDbContext.cs`.

## Pruebas de aceptación

1. Registrar vehículos no bloquea al valet.
2. Tras asignar cajón y quedar pendiente de QR, no puede iniciar otro estacionamiento.
3. Tras validar el QR, puede tomar otro trabajo.
4. TI/Administración puede buscar el pendiente por folio.
5. Liberar devuelve a la fila; reasignar conserva el pendiente; cancelar lo retira; discrepancia sólo documenta.
6. Toda intervención exige motivo y queda auditada.
7. Paseo/parcial con checkout hoy muestra fecha; ayer/mañana no.
8. VISITA no exige vehículo y termina al confirmar atención.

## Riesgos conocidos

- La API móvil aún no usa JWT; la autorización por gafete es una limitación pendiente.
- Cancelar no libera cajones automáticamente porque el vehículo puede seguir físicamente estacionado.
- “Hoy” usa la zona horaria del servidor; producción debe operar con la fecha de `America/Mexico_City`.
- Hay cambios no relacionados en ambos repositorios: no limpiar ni revertir el árbol de trabajo masivamente.
