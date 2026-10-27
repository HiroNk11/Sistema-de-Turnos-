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
