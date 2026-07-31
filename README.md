# AppValetParking

Sistema web y API para la operación de valet parking. Backend en **ASP.NET Core 9** (MVC + API), con base de datos **SQL Server**, control de accesos por roles, módulos de supervisión, generación de tickets y QR, e integración con bases existentes (Pegasys y TCADBOPE).

Este repositorio alimenta a la app móvil **Flutter** (`valet_parking_app_nuevo`). Todo cambio al contrato `api/*` debe coordinarse con Flutter.

---

## Características

- **Módulo operadora / valet**: registro de vehículos, solicitudes, asignación de cajones, tickets y salidas.
- **Módulo reservas**: cruce con TCADBOPE para detectar checkout del día (PASEO / PARCIAL).
- **Flujo ligero VISITA**: folios `VIS-` sin exigir registro de vehículo.
- **Solicitudes y bloqueo por QR**: un valet con un pendiente de QR no puede tomar otro estacionamiento hasta validarlo o que un supervisor intervenga.
- **Supervisión y auditoría**: liberar, reasignar, cancelar y registrar discrepancias, con motivo obligatorio y trazabilidad completa en `SolicitudesAuditoria`.
- **Generación de tickets e impresión**: plantillas, configuraciones de impresora y envío de correo.
- **Reportes con exportación a Excel** (ClosedXML / EPPlus).
- **Control de accesos por roles**: `OperadoraValet`, `Administracion` y `TI`, con matriz de vistas y capacidades.
- **CORS abierto** para la app Flutter (web y móvil) y **autenticación por cookies**.

---

## Tecnologías

| Tecnología | Uso |
|---|---|
| .NET 9 (ASP.NET Core MVC) | Backend web y API |
| SQL Server / Entity Framework Core 9 | Persistencia |
| ClosedXML / EPPlus | Exportación a Excel |
| QRCoder | Generación de códigos QR |
| System.Drawing.Common | Imágenes |
| Bootstrap + jQuery | Frontend (Razor) |
| Flutter (repositorio aparte) | App móvil consumidora de la API |

---

## Estructura del proyecto

```
AppValetParking/
├── Controllers/          # Controladores MVC y API (valet, solicitudes, supervisión, reportes, etc.)
├── Data/                 # DbContexts y configuración de bases de datos
├── Models/               # Entidades y view models
├── Services/             # Lógica de negocio, inicializador de BD, accesos, impresoras
├── Filters/              # Filtro global de acceso por vista
├── Sql/                  # Scripts SQL idempotentes aplicados al arrancar
├── Views/                # Vistas Razor
├── wwwroot/              # CSS, JS, librerías y configuración del valet (Config/)
├── Docs/                 # Documentación técnica y planes
├── Program.cs            # Punto de entrada y registro de servicios
└── appsettings.json      # Cadenas de conexión
```

### Archivos clave

| Archivo | Descripción |
|---|---|
| `Controllers/ControlSolicitudesController .cs` | Lógica de solicitudes, QR y supervisión |
| `Data/ApplicationDbContext.cs` | Mapeo de entidades |
| `Services/DatabaseInitializer.cs` | Ejecuta los scripts SQL al arrancar |
| `Sql/SolicitudesSupervision.sql` | Cambio idempotente de esquema (EstadoPaso, FechaPendienteQr, auditoría) |
| `Models/ValetSolicitud.cs`, `Models/SolicitudAuditoria.cs` | Modelos de solicitud y auditoría |

---

## Configuración

### Requisitos

- .NET SDK 9
- SQL Server (acceso a las bases de `appsettings.json`)

### Cadenas de conexión (`appsettings.json`)

Configura las siguientes bases antes de ejecutar:

| Clave | Base |
|---|---|
| `DefaultConnection` | Base principal del sistema (`ValetParkingDB`) |
| `PegasysConnection` | Base existente de Pegasys (solo lectura) |
| `TCABDOPEConnection` | Base TCADBOPE para alerta de checkout (solo lectura) |

### Configuración del valet

- `wwwroot/Config/configuracionValet.json`: parámetros generales.
- `wwwroot/Config/servicios.json`: catálogo de servicios de estacionamiento.
- `wwwroot/Config/printers.json`: configuración de impresoras.
- `Data/ticket-rules.json`, `Data/hotel-printer-config.json`: reglas de tickets e impresoras.

> **Importante**: `DatabaseInitializer` aplica los scripts de `Sql/` al arrancar. Verifica permisos DDL y toma respaldo antes del primer despliegue.

---

## Ejecución

```bash
dotnet restore
dotnet build
dotnet run
```

La aplicación inicia en el login (`/Account/Login`) como ruta por defecto.

---

## Roles

| Rol | Alcance |
|---|---|
| `OperadoraValet` | Operación diaria: movimientos, registro, reservas, estacionar, salidas |
| `Administracion` | Todo lo operativo + supervisión y reportes |
| `TI` | Todo el sistema + configuración y supervisión |

Los nombres históricos (`Operadora`, `Botones`, `Movimientos`, `PuertaSol`, `Reportes`, `Configuracion`) se migran automáticamente al rol canónico `OperadoraValet`.

---

## API (resumen)

- `POST /Account/LoginApi` — inicio de sesión para la app móvil.
- `POST api/solicitudes/pendienteQr/{id}` — marca una solicitud como pendiente de QR (bloquea al valet).
- `GET api/solicitudes` — lista solicitudes y cruza checkout con TCADBOPE.
- `GET api/supervision/solicitudes-pendientes` — pendientes y búsqueda por folio.
- `POST api/supervision/solicitudes/{id}/liberar` — devuelve a la fila y quita asignación.
- `POST api/supervision/solicitudes/{id}/reasignar` — cambia responsable conservando el QR pendiente.
- `POST api/supervision/solicitudes/{id}/cancelar` — cancela sin liberar cajón físico.
- `POST api/supervision/solicitudes/{id}/discrepancia` — registra observación sin alterar estado.

Todas las acciones de supervisión requieren `SupervisorGafete` y `Motivo`, y quedan registradas en `SolicitudesAuditoria`.

---

## Bases de datos

- `ValetParkingDB` — base principal del sistema.
- `pegasys` — integración existente (solo lectura).
- `TCADBOPE` — alerta de checkout del día (solo lectura). La fuente es `Reserva.h_fec_sda` (`yyyyMMdd`); `CheckoutHoy`/`CheckoutFecha` solo se devuelven cuando coinciden con el día actual del servidor.

---

## Scripts SQL

Los scripts en `Sql/` son idempotentes y se aplican automáticamente al arrancar:

- `SolicitudesSupervision.sql` — columnas `EstadoPaso`, `FechaPendienteQr` y tabla `SolicitudesAuditoria`.
- `AccesosVistas.sql` — matriz de accesos por rol y vista.
- `CodigosLiberacion_ExpiraEn.sql` — vencimiento de códigos de liberación.
- `FoliosTransferidos.sql` — folios transferidos.
- `Usuarios_Nombre.sql` — normalización de nombres de usuarios.

---

## Notas

- La API móvil aún no usa JWT; la autorización por gafete es una limitación pendiente.
- Cancelar no libera cajones automáticamente porque el vehículo puede seguir físicamente estacionado.
- "Hoy" usa la zona horaria del servidor; producción debe operar con `America/Mexico_City`.

---

## Documentación

- `CONTEXT.md` — memoria técnica y funcional del backend.
- `Docs/PLAN_ROLES_APP_MOVIL.md` — contrato de roles para la app Flutter.
