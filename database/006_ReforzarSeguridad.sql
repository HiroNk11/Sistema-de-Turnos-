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

CREATE OR ALTER PROCEDURE dbo.usp_Cobro_Registrar
    @TurnoId BIGINT,@Importe DECIMAL(18,2),@Medio VARCHAR(20),@Solicitud UNIQUEIDENTIFIER,@UsuarioId INT,@TokenSesion UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Usuario WITH(UPDLOCK,HOLDLOCK) WHERE UsuarioId=@UsuarioId AND Activo=1 AND TokenSesion=@TokenSesion AND Rol IN ('Administrador','Recepcion','Caja'))
            THROW 53001,N'No tenés permiso para registrar cobros.',1;
        DECLARE @Lock INT,@Recurso NVARCHAR(255)=CONCAT(N'Lock_Cobro_Turno_',@TurnoId);
        EXEC @Lock=sys.sp_getapplock @Resource=@Recurso,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
        IF @Lock<0 THROW 53002,N'La cuenta está siendo modificada. Intentá nuevamente.',1;
        DECLARE @Existente BIGINT=(SELECT CobroId FROM dbo.Cobro WHERE SolicitudId=@Solicitud);
        IF @Existente IS NOT NULL
        BEGIN
            IF NOT EXISTS(SELECT 1 FROM dbo.Cobro WHERE CobroId=@Existente AND TurnoId=@TurnoId
                AND Importe=@Importe AND Medio=@Medio AND UsuarioId=@UsuarioId AND AnuladoEnUtc IS NULL)
                THROW 53003,N'La solicitud ya existe con otros datos o fue anulada. Actualizá la cuenta antes de iniciar otra operación.',1;
            COMMIT; SELECT @Existente; RETURN;
        END;
        DECLARE @Precio DECIMAL(18,2),@Moneda CHAR(3),@Estado VARCHAR(25),@Pagado DECIMAL(18,2);
        SELECT @Precio=t.PrecioAcordado,@Moneda=t.Moneda,@Estado=e.Codigo
        FROM dbo.Turno t WITH(UPDLOCK,HOLDLOCK) JOIN dbo.EstadoTurno e ON e.EstadoTurnoId=t.EstadoTurnoId WHERE t.TurnoId=@TurnoId;
        IF @Precio IS NULL OR @Estado='CANCELADO' THROW 53004,N'El turno no existe o está cancelado.',1;
        SELECT @Pagado=COALESCE(SUM(Importe),0) FROM dbo.Cobro WITH(UPDLOCK,HOLDLOCK) WHERE TurnoId=@TurnoId AND AnuladoEnUtc IS NULL;
        IF @Importe IS NULL OR @Importe<=0 OR @Importe>@Precio-@Pagado
            THROW 53005,N'El importe supera el saldo pendiente o no es válido. Actualizá la cuenta.',1;
        INSERT dbo.Cobro(TurnoId,SolicitudId,Importe,Moneda,Medio,UsuarioId) VALUES(@TurnoId,@Solicitud,@Importe,@Moneda,@Medio,@UsuarioId);
        DECLARE @Id BIGINT=CONVERT(BIGINT,SCOPE_IDENTITY());
        COMMIT; SELECT @Id;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Cobro_Anular @CobroId BIGINT,@Motivo NVARCHAR(500),@UsuarioId INT,@TokenSesion UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Usuario WITH(UPDLOCK,HOLDLOCK) WHERE UsuarioId=@UsuarioId AND Activo=1 AND TokenSesion=@TokenSesion AND Rol IN ('Administrador','Caja'))
            THROW 53006,N'No tenés permiso para anular cobros.',1;
        IF NULLIF(LTRIM(RTRIM(@Motivo)),N'') IS NULL THROW 53007,N'Ingresá el motivo de anulación.',1;
        DECLARE @TurnoId BIGINT=(SELECT TurnoId FROM dbo.Cobro WHERE CobroId=@CobroId),@Lock INT;
        IF @TurnoId IS NULL THROW 53008,N'El cobro no existe.',1;
        DECLARE @Recurso NVARCHAR(255)=CONCAT(N'Lock_Cobro_Turno_',@TurnoId);
        EXEC @Lock=sys.sp_getapplock @Resource=@Recurso,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
        IF @Lock<0 THROW 53002,N'La cuenta está siendo modificada. Intentá nuevamente.',1;
        UPDATE dbo.Cobro SET AnuladoEnUtc=SYSUTCDATETIME(),AnuladoPorId=@UsuarioId,MotivoAnulacion=@Motivo
        WHERE CobroId=@CobroId AND AnuladoEnUtc IS NULL;
        IF @@ROWCOUNT=0 THROW 53009,N'El cobro ya estaba anulado. Actualizá la cuenta.',1;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO

