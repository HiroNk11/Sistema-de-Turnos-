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
