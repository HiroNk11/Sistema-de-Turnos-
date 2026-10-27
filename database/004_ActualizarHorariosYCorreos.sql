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

