USE SistemaTurnos;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.Usuario','Rol') IS NULL
BEGIN
    ALTER TABLE dbo.Usuario ADD Rol VARCHAR(20) NOT NULL CONSTRAINT DF_Usuario_Rol DEFAULT 'Recepcion';
    EXEC(N'UPDATE dbo.Usuario SET Rol=''Administrador'' WHERE UsuarioId=(SELECT MIN(UsuarioId) FROM dbo.Usuario WHERE Activo=1);');
END;
IF COL_LENGTH('dbo.Usuario','ProfesionalId') IS NULL
    ALTER TABLE dbo.Usuario ADD ProfesionalId INT NULL REFERENCES dbo.Profesional(ProfesionalId);
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Usuario_Rol')
    EXEC(N'ALTER TABLE dbo.Usuario ADD CONSTRAINT CK_Usuario_Rol CHECK
      (Rol IN (''Administrador'',''Recepcion'',''Caja'',''Profesional'') AND (Rol<>''Profesional'' OR ProfesionalId IS NOT NULL));');
IF OBJECT_ID('dbo.Cobro','U') IS NULL
BEGIN
    CREATE TABLE dbo.Cobro (
        CobroId BIGINT IDENTITY PRIMARY KEY,
        TurnoId BIGINT NOT NULL REFERENCES dbo.Turno(TurnoId),
        SolicitudId UNIQUEIDENTIFIER NOT NULL UNIQUE,
        Importe DECIMAL(18,2) NOT NULL CHECK(Importe>0), Moneda CHAR(3) NOT NULL,
        Medio VARCHAR(20) NOT NULL CHECK(Medio IN ('Efectivo','Transferencia','Tarjeta','Otro')),
        FechaCobroUtc DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
        UsuarioId INT NOT NULL REFERENCES dbo.Usuario(UsuarioId),
        AnuladoEnUtc DATETIME2(0) NULL, AnuladoPorId INT NULL REFERENCES dbo.Usuario(UsuarioId),
        MotivoAnulacion NVARCHAR(500) NULL,
        CHECK((AnuladoEnUtc IS NULL AND AnuladoPorId IS NULL AND MotivoAnulacion IS NULL)
           OR (AnuladoEnUtc IS NOT NULL AND AnuladoPorId IS NOT NULL AND MotivoAnulacion IS NOT NULL AND LEN(LTRIM(RTRIM(MotivoAnulacion)))>0))
    );
    CREATE INDEX IX_Cobro_Turno ON dbo.Cobro(TurnoId) INCLUDE(Importe,AnuladoEnUtc);
    CREATE INDEX IX_Cobro_Fecha ON dbo.Cobro(FechaCobroUtc) INCLUDE(Importe,Moneda,AnuladoEnUtc);
END;
COMMIT;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Cobro_Registrar
    @TurnoId BIGINT,@Importe DECIMAL(18,2),@Medio VARCHAR(20),@Solicitud UNIQUEIDENTIFIER,@UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Usuario WHERE UsuarioId=@UsuarioId AND Activo=1 AND Rol IN ('Administrador','Recepcion','Caja'))
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
CREATE OR ALTER PROCEDURE dbo.usp_Cobro_Anular @CobroId BIGINT,@Motivo NVARCHAR(500),@UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Usuario WHERE UsuarioId=@UsuarioId AND Activo=1 AND Rol IN ('Administrador','Caja'))
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
