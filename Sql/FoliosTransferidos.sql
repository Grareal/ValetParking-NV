-- Tabla de historial de transferencias de folio (etiqueta perdida/dañada).
-- Idempotente: sólo crea la tabla si no existe. No modifica tablas existentes.
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FoliosTransferidos')
BEGIN
    CREATE TABLE FoliosTransferidos (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        FolioAnterior VARCHAR(50)  NOT NULL,
        FolioNuevo    VARCHAR(50)  NOT NULL,
        Motivo        VARCHAR(300) NULL,
        Operador      VARCHAR(100) NULL,
        Fecha         DATETIME     NOT NULL DEFAULT (GETDATE())
    );

    CREATE INDEX IX_FoliosTransferidos_Anterior ON FoliosTransferidos(FolioAnterior);
    CREATE INDEX IX_FoliosTransferidos_Nuevo    ON FoliosTransferidos(FolioNuevo);
END
GO
