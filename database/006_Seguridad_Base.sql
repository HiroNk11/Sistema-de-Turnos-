USE SistemaTurnos;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.Usuario','TokenSesion') IS NULL
    ALTER TABLE dbo.Usuario ADD TokenSesion UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Usuario_Token DEFAULT NEWID();
IF COL_LENGTH('dbo.CorreoSalida','TokenReclamo') IS NULL
    ALTER TABLE dbo.CorreoSalida ADD TokenReclamo UNIQUEIDENTIFIER NULL;
COMMIT;
GO
CREATE OR ALTER TRIGGER dbo.TR_Usuario_RevocarSesion ON dbo.Usuario AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT(UPDATE(Nombre) OR UPDATE(PasswordHash) OR UPDATE(Rol) OR UPDATE(Activo) OR UPDATE(ProfesionalId)) RETURN;
    UPDATE u SET TokenSesion=NEWID()
    FROM dbo.Usuario u JOIN inserted i ON i.UsuarioId=u.UsuarioId JOIN deleted d ON d.UsuarioId=i.UsuarioId
    WHERE EXISTS(SELECT i.Nombre,i.PasswordHash,i.Rol,i.Activo,i.ProfesionalId
                 EXCEPT SELECT d.Nombre,d.PasswordHash,d.Rol,d.Activo,d.ProfesionalId);
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Cobro_ProtegerDatos ON dbo.Cobro AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT i.CobroId,i.TurnoId,i.SolicitudId,i.Importe,i.Moneda,i.Medio,i.FechaCobroUtc,i.UsuarioId
              FROM inserted i EXCEPT
              SELECT d.CobroId,d.TurnoId,d.SolicitudId,d.Importe,d.Moneda,d.Medio,d.FechaCobroUtc,d.UsuarioId FROM deleted d)
        THROW 53201,N'Los datos originales del cobro no se editan. Anulá el movimiento y registrá uno nuevo.',1;
    IF EXISTS(SELECT i.CobroId,i.AnuladoEnUtc,i.AnuladoPorId,i.MotivoAnulacion FROM inserted i
              JOIN deleted d ON d.CobroId=i.CobroId WHERE d.AnuladoEnUtc IS NOT NULL
              EXCEPT SELECT CobroId,AnuladoEnUtc,AnuladoPorId,MotivoAnulacion FROM deleted WHERE AnuladoEnUtc IS NOT NULL)
        THROW 53202,N'No se puede modificar una anulación existente.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Turno_ProtegerCobros ON dbo.Turno AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT(UPDATE(PrecioAcordado) OR UPDATE(Moneda)) RETURN;
    IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.TurnoId=i.TurnoId
        WHERE i.Moneda<>d.Moneda AND EXISTS(SELECT 1 FROM dbo.Cobro c WHERE c.TurnoId=i.TurnoId))
        THROW 53203,N'No se puede cambiar la moneda de una reserva con movimientos de caja.',1;
    IF EXISTS(SELECT 1 FROM inserted i WHERE i.PrecioAcordado<
        (SELECT COALESCE(SUM(c.Importe),0) FROM dbo.Cobro c WHERE c.TurnoId=i.TurnoId AND c.AnuladoEnUtc IS NULL))
        THROW 53204,N'El precio de la reserva no puede ser menor a lo ya cobrado.',1;
END;
GO
