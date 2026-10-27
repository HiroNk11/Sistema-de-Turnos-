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
