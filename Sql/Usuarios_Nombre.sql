-- Agrega la columna Nombre a la tabla Usuarios para guardar el nombre real
-- del empleado (ej. "JUAN PEREZ") y mostrarlo en la app ("Buen turno, <nombre>")
-- en lugar del username (oth53226). Idempotente: solo la crea si no existe.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'Nombre' AND Object_ID = Object_ID(N'dbo.Usuarios')
)
BEGIN
    ALTER TABLE dbo.Usuarios ADD Nombre NVARCHAR(150) NULL;
END
GO
