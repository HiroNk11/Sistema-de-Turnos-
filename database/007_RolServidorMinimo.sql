-- Ejecutar con la cuenta de mantenimiento, después de 006.
-- No crea logins, no altera al propietario ni asigna operadores a este rol.
USE SistemaTurnos;
GO
IF DATABASE_PRINCIPAL_ID(N'SistemaTurnos_ApiRuntime') IS NULL
    CREATE ROLE SistemaTurnos_ApiRuntime AUTHORIZATION dbo;
GO
GRANT SELECT ON dbo.Rubro TO SistemaTurnos_ApiRuntime;
GRANT SELECT ON dbo.EstadoTurno TO SistemaTurnos_ApiRuntime;
GRANT SELECT,INSERT,UPDATE ON dbo.Cliente TO SistemaTurnos_ApiRuntime;
GRANT SELECT,INSERT,UPDATE ON dbo.Paciente TO SistemaTurnos_ApiRuntime;
GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.PacienteAnimal TO SistemaTurnos_ApiRuntime;
GRANT SELECT,INSERT,UPDATE ON dbo.Profesional TO SistemaTurnos_ApiRuntime;
GRANT SELECT,INSERT,UPDATE ON dbo.Servicio TO SistemaTurnos_ApiRuntime;
GRANT SELECT,INSERT,UPDATE ON dbo.ProfesionalServicio TO SistemaTurnos_ApiRuntime;
GRANT SELECT,UPDATE ON dbo.ConfiguracionNegocio TO SistemaTurnos_ApiRuntime;
GRANT SELECT,INSERT,UPDATE ON dbo.Usuario TO SistemaTurnos_ApiRuntime;
GRANT SELECT,INSERT,DELETE ON dbo.JornadaProfesional TO SistemaTurnos_ApiRuntime;
GRANT SELECT,UPDATE ON dbo.CorreoSalida TO SistemaTurnos_ApiRuntime;
GRANT SELECT ON dbo.Turno TO SistemaTurnos_ApiRuntime;
GRANT SELECT ON dbo.Cobro TO SistemaTurnos_ApiRuntime;
GRANT EXECUTE ON dbo.usp_Turno_Crear TO SistemaTurnos_ApiRuntime;
GRANT EXECUTE ON dbo.usp_Cobro_Registrar TO SistemaTurnos_ApiRuntime;
GRANT EXECUTE ON dbo.usp_Cobro_Anular TO SistemaTurnos_ApiRuntime;
DENY INSERT,UPDATE,DELETE ON dbo.Turno TO SistemaTurnos_ApiRuntime;
DENY INSERT,UPDATE,DELETE ON dbo.Cobro TO SistemaTurnos_ApiRuntime;
DENY ALTER ANY USER,ALTER ANY ROLE,ALTER ANY SCHEMA TO SistemaTurnos_ApiRuntime;
GO
-- En el servidor: crear una identidad Windows dedicada y un usuario SQL para ella.
-- Agregar SOLO esa identidad a este rol. No usar db_owner/db_datareader/db_datawriter.
-- Los operadores de WinForms no deben tener usuario ni login con acceso a esta base.
-- La cuenta de mantenimiento queda separada de la cuenta que ejecuta la API.
