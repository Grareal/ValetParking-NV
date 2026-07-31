-- Agrega la columna de vigencia a los códigos de liberación.
-- Idempotente: solo la crea si no existe. null = sin caducidad.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.CodigosLiberacion') AND name = 'ExpiraEn'
)
BEGIN
    ALTER TABLE dbo.CodigosLiberacion ADD ExpiraEn datetime2 NULL;
END
GO
