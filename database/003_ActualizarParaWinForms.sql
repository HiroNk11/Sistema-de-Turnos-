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

        IF NOT EXISTS (SELECT 1 FROM dbo.Cliente WITH (UPDLOCK,HOLDLOCK) WHERE ClienteId=@ClienteId AND Activo=1)
            THROW 51007, N'El cliente no existe o está inactivo.', 1;
        IF NOT EXISTS (
            SELECT 1 FROM dbo.ProfesionalServicio ps WITH (UPDLOCK,HOLDLOCK)
            JOIN dbo.Profesional p WITH (UPDLOCK,HOLDLOCK) ON p.ProfesionalId=ps.ProfesionalId
            JOIN dbo.Servicio s WITH (UPDLOCK,HOLDLOCK) ON s.ServicioId=ps.ServicioId
            WHERE ps.ProfesionalId=@ProfesionalId AND ps.ServicioId=@ServicioId
              AND ps.Activo=1 AND p.Activo=1 AND s.Activo=1)
            THROW 51008, N'El profesional o servicio están inactivos o no están asociados.', 1;
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

