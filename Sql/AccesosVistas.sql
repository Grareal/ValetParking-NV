SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.VistasSistema', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VistasSistema (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_VistasSistema PRIMARY KEY,
        Clave nvarchar(80) NOT NULL,
        Titulo nvarchar(120) NOT NULL,
        Icono nvarchar(30) NOT NULL CONSTRAINT DF_VistasSistema_Icono DEFAULT('*'),
        Url nvarchar(250) NOT NULL,
        Orden int NOT NULL CONSTRAINT DF_VistasSistema_Orden DEFAULT(0),
        Activo bit NOT NULL CONSTRAINT DF_VistasSistema_Activo DEFAULT(1),
        MostrarEnMenu bit NOT NULL CONSTRAINT DF_VistasSistema_Menu DEFAULT(1),
        CONSTRAINT UQ_VistasSistema_Clave UNIQUE (Clave)
    );
END;

IF OBJECT_ID(N'dbo.RolVistas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolVistas (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RolVistas PRIMARY KEY,
        Rol nvarchar(40) NOT NULL,
        VistaSistemaId int NOT NULL,
        CONSTRAINT FK_RolVistas_VistasSistema FOREIGN KEY (VistaSistemaId)
            REFERENCES dbo.VistasSistema(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_RolVistas_RolVista UNIQUE (Rol, VistaSistemaId)
    );
END;

DECLARE @Vistas TABLE (Clave nvarchar(80), Titulo nvarchar(120), Icono nvarchar(30), Url nvarchar(250), Orden int, Menu bit);
INSERT INTO @Vistas VALUES
('operadora','Operadora / Valet','OV','/Operadora/Index',10,1),
('reportes','Reportes','R','/Reportes/Index',20,1),
('codigos','Codigos de liberacion','C','/Tickets/Codigos',30,1),
('solicitudes','Control de solicitudes','S','/ControlSolicitudes/Index',40,1),
('reservas','Reservaciones','RS','/Tickets/Reservas',50,1),
('historial','Historial de tickets','H','/Tickets/Historial',60,1),
('movimientos','Movimientos','M','/Botones/EditarRegistro',70,1),
('usuarios','Usuarios','U','/Usuarios/Index',80,1),
('config-tickets','Configuracion de tickets','CT','/Tickets/Config',90,1),
('accesos','Configuracion de accesos','A','/ConfiguracionAccesos/Index',100,1);

MERGE dbo.VistasSistema AS t
USING @Vistas AS s ON t.Clave = s.Clave
WHEN NOT MATCHED THEN INSERT (Clave,Titulo,Icono,Url,Orden,Activo,MostrarEnMenu)
VALUES(s.Clave,s.Titulo,s.Icono,s.Url,s.Orden,1,s.Menu);

INSERT INTO dbo.RolVistas (Rol, VistaSistemaId)
SELECT 'OperadoraValet', v.Id
FROM dbo.VistasSistema v
WHERE v.Clave IN ('operadora','reportes','codigos','solicitudes','reservas','historial','movimientos')
AND NOT EXISTS (SELECT 1 FROM dbo.RolVistas rv WHERE rv.Rol='OperadoraValet' AND rv.VistaSistemaId=v.Id);

-- Migracion idempotente: cualquier rol operativo anterior se consolida en OperadoraValet.
UPDATE dbo.Usuarios SET Funciones = 'TI' WHERE ',' + REPLACE(Funciones,' ', '') + ',' LIKE '%,TI,%';
UPDATE dbo.Usuarios SET Funciones = 'Administracion'
WHERE Funciones <> 'TI' AND ',' + REPLACE(Funciones,' ', '') + ',' LIKE '%,Administracion,%';
UPDATE dbo.Usuarios SET Funciones = 'OperadoraValet'
WHERE Funciones NOT IN ('TI','Administracion') AND (
    ',' + REPLACE(Funciones,' ', '') + ',' LIKE '%,Operadora,%' OR
    ',' + REPLACE(Funciones,' ', '') + ',' LIKE '%,Botones,%' OR
    ',' + REPLACE(Funciones,' ', '') + ',' LIKE '%,Movimientos,%' OR
    ',' + REPLACE(Funciones,' ', '') + ',' LIKE '%,PuertaSol,%' OR
    ',' + REPLACE(Funciones,' ', '') + ',' LIKE '%,Reportes,%' OR
    ',' + REPLACE(Funciones,' ', '') + ',' LIKE '%,Configuracion,%');
