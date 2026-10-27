-- Solo lectura. Ejecutar después de 006_ReforzarSeguridad.sql.
-- Devuelve cantidades de inconsistencias, sin nombres, emails ni contraseñas.
USE SistemaTurnos;
GO
SET NOCOUNT ON;
SELECT N'Cuentas con rol inválido o vínculo faltante' AS Control,COUNT_BIG(*) AS Cantidad
FROM dbo.Usuario WHERE Rol NOT IN('Administrador','Recepcion','Caja','Profesional') OR (Rol='Profesional' AND ProfesionalId IS NULL)
UNION ALL
SELECT N'Cuentas sin token válido',COUNT_BIG(*) FROM dbo.Usuario WHERE TokenSesion='00000000-0000-0000-0000-000000000000'
UNION ALL
SELECT N'Reservas con saldo negativo',COUNT_BIG(*) FROM dbo.Turno t
WHERE t.PrecioAcordado<(SELECT COALESCE(SUM(Importe),0) FROM dbo.Cobro WHERE TurnoId=t.TurnoId AND AnuladoEnUtc IS NULL)
UNION ALL
SELECT N'Cobros con moneda distinta a la reserva',COUNT_BIG(*) FROM dbo.Cobro c JOIN dbo.Turno t ON t.TurnoId=c.TurnoId WHERE c.Moneda<>t.Moneda
UNION ALL
SELECT N'Jornadas superpuestas',COUNT_BIG(*) FROM dbo.JornadaProfesional a JOIN dbo.JornadaProfesional b
ON a.ProfesionalId=b.ProfesionalId AND a.DiaSemana=b.DiaSemana AND a.JornadaId<b.JornadaId AND a.Desde<b.Hasta AND a.Hasta>b.Desde
UNION ALL
SELECT N'Turnos no cancelados superpuestos',COUNT_BIG(*) FROM dbo.Turno a JOIN dbo.Turno b
ON a.ProfesionalId=b.ProfesionalId AND a.TurnoId<b.TurnoId AND a.InicioUtc<b.FinUtc AND a.FinUtc>b.InicioUtc
JOIN dbo.EstadoTurno ea ON ea.EstadoTurnoId=a.EstadoTurnoId JOIN dbo.EstadoTurno eb ON eb.EstadoTurnoId=b.EstadoTurnoId
WHERE ea.Codigo<>'CANCELADO' AND eb.Codigo<>'CANCELADO'
UNION ALL
SELECT N'Extensiones de animal asociadas a personas',COUNT_BIG(*) FROM dbo.PacienteAnimal a JOIN dbo.Paciente p ON p.PacienteId=a.PacienteId WHERE p.Tipo<>'A'
UNION ALL
SELECT N'Zonas horarias no reconocidas',COUNT_BIG(*) FROM dbo.ConfiguracionNegocio c WHERE NOT EXISTS(SELECT 1 FROM sys.time_zone_info z WHERE z.name=c.ZonaHorariaId)
UNION ALL
SELECT N'Reclamos de correo vencidos',COUNT_BIG(*) FROM dbo.CorreoSalida WHERE Estado='Procesando' AND ReservaHastaUtc<SYSUTCDATETIME();
GO
