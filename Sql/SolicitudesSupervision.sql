SET NOCOUNT ON;

IF COL_LENGTH('dbo.ValetSolicitudes', 'EstadoPaso') IS NULL
    ALTER TABLE dbo.ValetSolicitudes ADD EstadoPaso nvarchar(40) NULL;

IF COL_LENGTH('dbo.ValetSolicitudes', 'FechaPendienteQr') IS NULL
    ALTER TABLE dbo.ValetSolicitudes ADD FechaPendienteQr datetime2 NULL;

IF OBJECT_ID(N'dbo.SolicitudesAuditoria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SolicitudesAuditoria (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SolicitudesAuditoria PRIMARY KEY,
        SolicitudId int NOT NULL,
        Fecha datetime2 NOT NULL CONSTRAINT DF_SolicitudesAuditoria_Fecha DEFAULT(GETDATE()),
        Accion nvarchar(40) NOT NULL,
        ActorGafete nvarchar(80) NULL,
        ActorNombre nvarchar(180) NULL,
        Motivo nvarchar(500) NULL,
        ValorAnterior nvarchar(500) NULL,
        ValorNuevo nvarchar(500) NULL
    );
    CREATE INDEX IX_SolicitudesAuditoria_SolicitudId_Fecha
        ON dbo.SolicitudesAuditoria(SolicitudId, Fecha DESC);
END;
