USE master;
GO
IF DB_ID(N'SistemaTurnos') IS NULL CREATE DATABASE SistemaTurnos;
GO
USE SistemaTurnos;
GO
-- Ejecutar una sola vez sobre una base vacía.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
CREATE TABLE dbo.Rubro (
 RubroId SMALLINT IDENTITY PRIMARY KEY, Codigo VARCHAR(30) NOT NULL UNIQUE,
 Nombre NVARCHAR(80) NOT NULL
);
CREATE TABLE dbo.ConfiguracionNegocio (
 ConfiguracionId TINYINT NOT NULL PRIMARY KEY CHECK (ConfiguracionId = 1),
 RubroId SMALLINT NOT NULL REFERENCES dbo.Rubro(RubroId),
 NombreComercial NVARCHAR(150) NOT NULL, Telefono NVARCHAR(40) NULL,
 Email NVARCHAR(254) NULL, Direccion NVARCHAR(250) NULL,
 EtiquetaCliente NVARCHAR(40) NOT NULL DEFAULT N'Cliente',
 EtiquetaPaciente NVARCHAR(40) NOT NULL DEFAULT N'Paciente',
 UsaPacientes BIT NOT NULL DEFAULT 0, ZonaHorariaId NVARCHAR(100) NOT NULL,
 Moneda CHAR(3) NOT NULL, Version ROWVERSION NOT NULL
);
CREATE TABLE dbo.Cliente (
 ClienteId INT IDENTITY PRIMARY KEY, Nombre NVARCHAR(100) NOT NULL,
 Apellido NVARCHAR(100) NOT NULL, TipoDocumento NVARCHAR(20) NULL,
 NumeroDocumento NVARCHAR(30) NULL, Telefono NVARCHAR(40) NULL,
 Email NVARCHAR(254) NULL, Observaciones NVARCHAR(1000) NULL,
 Activo BIT NOT NULL DEFAULT 1, CreadoEnUtc DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 Version ROWVERSION NOT NULL,
 CHECK ((TipoDocumento IS NULL AND NumeroDocumento IS NULL)
     OR (TipoDocumento IS NOT NULL AND NumeroDocumento IS NOT NULL))
);
CREATE UNIQUE INDEX UX_Cliente_Documento ON dbo.Cliente(TipoDocumento,NumeroDocumento)
 WHERE TipoDocumento IS NOT NULL AND NumeroDocumento IS NOT NULL;
CREATE TABLE dbo.Paciente (
 PacienteId INT IDENTITY PRIMARY KEY,
 ClienteResponsableId INT NOT NULL REFERENCES dbo.Cliente(ClienteId),
 Tipo CHAR(1) NOT NULL CHECK (Tipo IN ('P','A')),
 Nombre NVARCHAR(100) NOT NULL, Apellido NVARCHAR(100) NULL,
 FechaNacimiento DATE NULL, Activo BIT NOT NULL DEFAULT 1, Version ROWVERSION NOT NULL
);
CREATE TABLE dbo.PacienteAnimal (
 PacienteId INT NOT NULL PRIMARY KEY REFERENCES dbo.Paciente(PacienteId),
 Especie NVARCHAR(60) NOT NULL, Raza NVARCHAR(80) NULL, Microchip NVARCHAR(50) NULL
);
CREATE TABLE dbo.Profesional (
 ProfesionalId INT IDENTITY PRIMARY KEY, Nombre NVARCHAR(100) NOT NULL,
 Apellido NVARCHAR(100) NOT NULL, Matricula NVARCHAR(50) NULL,
 Telefono NVARCHAR(40) NULL, Email NVARCHAR(254) NULL,
 Activo BIT NOT NULL DEFAULT 1, Version ROWVERSION NOT NULL
);
CREATE TABLE dbo.Servicio (
 ServicioId INT IDENTITY PRIMARY KEY, Nombre NVARCHAR(120) NOT NULL,
 Descripcion NVARCHAR(500) NULL, DuracionMinutos SMALLINT NOT NULL CHECK (DuracionMinutos > 0),
 PrecioBase DECIMAL(18,2) NOT NULL CHECK (PrecioBase >= 0),
 Activo BIT NOT NULL DEFAULT 1, Version ROWVERSION NOT NULL
);
CREATE TABLE dbo.ProfesionalServicio (
 ProfesionalId INT NOT NULL REFERENCES dbo.Profesional(ProfesionalId),
 ServicioId INT NOT NULL REFERENCES dbo.Servicio(ServicioId),
 Activo BIT NOT NULL DEFAULT 1, PRIMARY KEY (ProfesionalId,ServicioId)
);
CREATE TABLE dbo.EstadoTurno (
 EstadoTurnoId TINYINT NOT NULL PRIMARY KEY,
 Codigo VARCHAR(25) NOT NULL UNIQUE, Nombre NVARCHAR(50) NOT NULL
);
CREATE TABLE dbo.Turno (
 TurnoId BIGINT IDENTITY PRIMARY KEY,
 ClienteId INT NOT NULL REFERENCES dbo.Cliente(ClienteId),
 PacienteId INT NULL REFERENCES dbo.Paciente(PacienteId),
 ProfesionalId INT NOT NULL, ServicioId INT NOT NULL,
 EstadoTurnoId TINYINT NOT NULL DEFAULT 1 REFERENCES dbo.EstadoTurno(EstadoTurnoId),
 InicioUtc DATETIME2(0) NOT NULL, FinUtc DATETIME2(0) NOT NULL,
 PrecioAcordado DECIMAL(18,2) NOT NULL CHECK (PrecioAcordado >= 0),
 Moneda CHAR(3) NOT NULL, Observaciones NVARCHAR(1000) NULL,
 MotivoCancelacion NVARCHAR(500) NULL,
 CreadoEnUtc DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(), Version ROWVERSION NOT NULL,
 CHECK (FinUtc > InicioUtc),
 FOREIGN KEY (ProfesionalId,ServicioId) REFERENCES dbo.ProfesionalServicio(ProfesionalId,ServicioId)
);
CREATE INDEX IX_Turno_AgendaProfesional ON dbo.Turno(ProfesionalId,InicioUtc) INCLUDE (FinUtc,EstadoTurnoId);
CREATE INDEX IX_Turno_Cliente ON dbo.Turno(ClienteId,InicioUtc);
CREATE INDEX IX_Turno_Paciente ON dbo.Turno(PacienteId,InicioUtc) WHERE PacienteId IS NOT NULL;
CREATE INDEX IX_Paciente_Responsable ON dbo.Paciente(ClienteResponsableId);
CREATE INDEX IX_ProfesionalServicio_Servicio ON dbo.ProfesionalServicio(ServicioId,ProfesionalId);
INSERT dbo.Rubro(Codigo,Nombre) VALUES
 ('GENERAL',N'General'),('ODONTOLOGIA',N'Odontología'),
 ('PELUQUERIA',N'Peluquería'),('VETERINARIA',N'Veterinaria');
INSERT dbo.EstadoTurno(EstadoTurnoId,Codigo,Nombre) VALUES
 (1,'PENDIENTE',N'Pendiente'),(2,'CONFIRMADO',N'Confirmado'),
 (3,'EN_ATENCION',N'En atención'),(4,'FINALIZADO',N'Finalizado'),
 (5,'CANCELADO',N'Cancelado'),(6,'AUSENTE',N'Ausente');
COMMIT;
GO

USE SistemaTurnos;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Usuario',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuario (
        UsuarioId INT IDENTITY PRIMARY KEY,
        Nombre NVARCHAR(50) COLLATE Latin1_General_100_CI_AS NOT NULL UNIQUE,
        PasswordHash NVARCHAR(200) NOT NULL,
        Activo BIT NOT NULL DEFAULT 1,
        IntentosFallidos INT NOT NULL DEFAULT 0 CHECK (IntentosFallidos >= 0),
        BloqueadoHastaUtc DATETIME2(0) NULL,
        CreadoEnUtc DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
        Version ROWVERSION NOT NULL
    );
END;
IF NOT EXISTS (SELECT 1 FROM dbo.ConfiguracionNegocio WHERE ConfiguracionId=1)
    INSERT dbo.ConfiguracionNegocio
        (ConfiguracionId,RubroId,NombreComercial,EtiquetaCliente,EtiquetaPaciente,UsaPacientes,ZonaHorariaId,Moneda)
    SELECT 1,RubroId,N'Mi negocio',N'Cliente',N'Paciente',0,N'Argentina Standard Time','ARS'
    FROM dbo.Rubro WHERE Codigo='GENERAL';
COMMIT;
GO
-- No se crean usuarios ni contraseñas predeterminadas.
-- El primer administrador se registra desde el formulario inicial.

USE SistemaTurnos;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.Paciente','Email') IS NULL ALTER TABLE dbo.Paciente ADD Email NVARCHAR(254) NULL;
IF OBJECT_ID('dbo.JornadaProfesional','U') IS NULL
CREATE TABLE dbo.JornadaProfesional (
    JornadaId INT IDENTITY PRIMARY KEY,
    ProfesionalId INT NOT NULL REFERENCES dbo.Profesional(ProfesionalId),
    DiaSemana TINYINT NOT NULL CHECK (DiaSemana BETWEEN 0 AND 6),
    Desde TIME(0) NOT NULL, Hasta TIME(0) NOT NULL,
    PasoMinutos SMALLINT NOT NULL CHECK (PasoMinutos BETWEEN 5 AND 120),
    CHECK (Hasta>Desde), UNIQUE(ProfesionalId,DiaSemana,Desde)
);
IF OBJECT_ID('dbo.CorreoSalida','U') IS NULL
BEGIN
    CREATE TABLE dbo.CorreoSalida (
        CorreoId BIGINT IDENTITY PRIMARY KEY,
        TurnoId BIGINT NOT NULL UNIQUE REFERENCES dbo.Turno(TurnoId),
        Destinatario NVARCHAR(254) NOT NULL,
        Asunto NVARCHAR(200) NOT NULL, Cuerpo NVARCHAR(MAX) NOT NULL,
        Estado VARCHAR(15) NOT NULL DEFAULT 'Pendiente'
          CHECK (Estado IN ('Pendiente','Procesando','Enviado','Error')),
        Intentos INT NOT NULL DEFAULT 0,
        ProximoIntentoUtc DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
        ReservaHastaUtc DATETIME2(0) NULL, EnviadoEnUtc DATETIME2(0) NULL,
        Detalle NVARCHAR(500) NOT NULL DEFAULT N'Pendiente de envío.',
        CreadoEnUtc DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_CorreoSalida_Pendientes ON dbo.CorreoSalida(Estado,ProximoIntentoUtc);
END;
COMMIT;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Turno_Crear
    @ClienteId INT,
    @PacienteId INT = NULL,
    @ProfesionalId INT,
    @ServicioId INT,
    @InicioUtc DATETIME2(0),
    @FinUtc DATETIME2(0),
    @PrecioAcordado DECIMAL(18,2),
    @Moneda CHAR(3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Propia BIT = 0, @Bloqueo INT;
    DECLARE @Recurso NVARCHAR(255) = CONCAT(N'Lock_Agenda_Prof_', @ProfesionalId);
    DECLARE @Pendiente TINYINT, @Cancelado TINYINT, @TurnoId BIGINT;
    BEGIN TRY
        IF @@TRANCOUNT = 0
        BEGIN
            BEGIN TRANSACTION;
            SET @Propia = 1;
        END
        ELSE SAVE TRANSACTION GuardarTurnoCrear;

        IF @InicioUtc IS NULL OR @FinUtc IS NULL OR @FinUtc <= @InicioUtc
            THROW 51001, N'El intervalo del turno es inválido.', 1;
        IF @PrecioAcordado IS NULL OR @PrecioAcordado < 0
            THROW 51002, N'El precio acordado es inválido.', 1;
        IF @Moneda IS NULL OR LEN(LTRIM(RTRIM(@Moneda))) <> 3
            THROW 51003, N'La moneda debe contener tres caracteres.', 1;

        SELECT @Pendiente = EstadoTurnoId FROM dbo.EstadoTurno WHERE Codigo = 'PENDIENTE';
        SELECT @Cancelado = EstadoTurnoId FROM dbo.EstadoTurno WHERE Codigo = 'CANCELADO';
        IF @Pendiente IS NULL OR @Cancelado IS NULL
            THROW 51004, N'Faltan estados requeridos en EstadoTurno.', 1;

        EXEC @Bloqueo = sys.sp_getapplock
            @Resource = @Recurso, @LockMode = 'Exclusive',
            @LockOwner = 'Transaction', @LockTimeout = 10000, @DbPrincipal = 'public';
        IF @Bloqueo < 0
            THROW 51005, N'No se pudo bloquear la agenda del profesional.', 1;

        DECLARE @Zona NVARCHAR(100)=(SELECT ZonaHorariaId FROM dbo.ConfiguracionNegocio WHERE ConfiguracionId=1);
        IF @Zona IS NULL THROW 51010,N'Configurá la zona horaria del negocio.',1;
        IF @InicioUtc<=SYSUTCDATETIME() THROW 51011,N'El turno debe ser futuro.',1;
        DECLARE @Local DATETIME2(0)=CONVERT(DATETIME2(0),@InicioUtc AT TIME ZONE 'UTC' AT TIME ZONE @Zona);
        DECLARE @FinLocal DATETIME2(0)=CONVERT(DATETIME2(0),@FinUtc AT TIME ZONE 'UTC' AT TIME ZONE @Zona);
        IF NOT EXISTS (SELECT 1 FROM dbo.JornadaProfesional WITH (UPDLOCK,HOLDLOCK)
            WHERE ProfesionalId=@ProfesionalId
              AND DiaSemana=DATEDIFF(DAY,CONVERT(DATE,'19000101'),CONVERT(DATE,@Local))%7
              AND CONVERT(DATE,@Local)=CONVERT(DATE,@FinLocal)
              AND CONVERT(TIME,@Local)>=Desde AND CONVERT(TIME,@FinLocal)<=Hasta
              AND DATEDIFF(MINUTE,Desde,CONVERT(TIME,@Local))%PasoMinutos=0
              AND DATEPART(SECOND,@Local)=0)
            THROW 51012,N'El horario está fuera de la jornada o no coincide con un inicio disponible.',1;

        IF NOT EXISTS (SELECT 1 FROM dbo.Cliente WITH (UPDLOCK,HOLDLOCK) WHERE ClienteId=@ClienteId AND Activo=1)
            THROW 51007, N'El cliente no existe o está inactivo.', 1;
        IF NOT EXISTS (
            SELECT 1 FROM dbo.ProfesionalServicio ps WITH (UPDLOCK,HOLDLOCK)
            JOIN dbo.Profesional p WITH (UPDLOCK,HOLDLOCK) ON p.ProfesionalId=ps.ProfesionalId
            JOIN dbo.Servicio s WITH (UPDLOCK,HOLDLOCK) ON s.ServicioId=ps.ServicioId
            WHERE ps.ProfesionalId=@ProfesionalId AND ps.ServicioId=@ServicioId
              AND ps.Activo=1 AND p.Activo=1 AND s.Activo=1)
            THROW 51008, N'El profesional o servicio están inactivos o no están asociados.', 1;
        IF @FinUtc<>DATEADD(MINUTE,(SELECT DuracionMinutos FROM dbo.Servicio WHERE ServicioId=@ServicioId),@InicioUtc)
            THROW 51013,N'La duración debe coincidir con el servicio. Actualizá la lista.',1;
        IF @PacienteId IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM dbo.Paciente WITH (UPDLOCK,HOLDLOCK)
            WHERE PacienteId=@PacienteId AND ClienteResponsableId=@ClienteId AND Activo=1)
            THROW 51009, N'El paciente está inactivo o no pertenece al responsable.', 1;

        -- Reprogramar y reactivar deben usar el mismo recurso de bloqueo.
        IF EXISTS (
            SELECT 1 FROM dbo.Turno WITH (READCOMMITTEDLOCK)
            WHERE ProfesionalId = @ProfesionalId AND EstadoTurnoId <> @Cancelado
              AND InicioUtc < @FinUtc AND FinUtc > @InicioUtc
        )
            THROW 51006, N'El profesional ya tiene un turno en ese horario.', 1;

        INSERT INTO dbo.Turno
            (ClienteId, PacienteId, ProfesionalId, ServicioId, EstadoTurnoId,
             InicioUtc, FinUtc, PrecioAcordado, Moneda)
        VALUES
            (@ClienteId, @PacienteId, @ProfesionalId, @ServicioId, @Pendiente,
             @InicioUtc, @FinUtc, @PrecioAcordado, UPPER(@Moneda));
        SET @TurnoId = CONVERT(BIGINT, SCOPE_IDENTITY());
        DECLARE @Destino NVARCHAR(254),@Negocio NVARCHAR(150);
        SELECT @Destino=COALESCE(NULLIF(LTRIM(RTRIM(CASE WHEN p.Tipo='P' THEN p.Email END)),N''),NULLIF(LTRIM(RTRIM(c.Email)),N''))
        FROM dbo.Cliente c LEFT JOIN dbo.Paciente p ON p.PacienteId=@PacienteId WHERE c.ClienteId=@ClienteId;
        SELECT @Negocio=NombreComercial FROM dbo.ConfiguracionNegocio WHERE ConfiguracionId=1;
        IF @Destino IS NOT NULL
            INSERT dbo.CorreoSalida(TurnoId,Destinatario,Asunto,Cuerpo)
            VALUES (@TurnoId,@Destino,N'Confirmación de turno · '+@Negocio,
                N'Tu reserva #'+CONVERT(NVARCHAR(30),@TurnoId)+N' fue confirmada en '+@Negocio+N'.'+NCHAR(13)+NCHAR(10)
                +N'Fecha y hora: '+CONVERT(NVARCHAR(10),@Local,103)+N' '+CONVERT(NVARCHAR(5),@Local,108)
                +N' a '+CONVERT(NVARCHAR(5),@FinLocal,108)+N' ('+@Zona+N').'+NCHAR(13)+NCHAR(10)
                +N'Si necesitás modificarla, comunicate con el negocio.');
        IF @Propia = 1 COMMIT TRANSACTION;
        SELECT @TurnoId AS TurnoId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
        BEGIN
            IF @Propia = 1 ROLLBACK TRANSACTION;
            ELSE IF XACT_STATE() = 1 ROLLBACK TRANSACTION GuardarTurnoCrear;
            -- Si la transacción externa está condenada (-1), su propietario la revierte.
        END;
        THROW;
    END CATCH;
END;
GO

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


