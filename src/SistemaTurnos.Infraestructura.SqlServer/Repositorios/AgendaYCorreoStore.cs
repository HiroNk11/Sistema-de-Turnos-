using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Infraestructura.SqlServer.Repositorios;

public sealed partial class AppStore
{
    public async Task<IReadOnlyList<Jornada>> JornadasAsync(int profesionalId)
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("SELECT DiaSemana,Desde,Hasta,PasoMinutos FROM dbo.JornadaProfesional WHERE ProfesionalId=@Id ORDER BY DiaSemana,Desde;",cn);
        Param(cmd,"@Id",SqlDbType.Int,profesionalId);
        using var r=await cmd.ExecuteReaderAsync(); var datos=new List<Jornada>();
        while (await r.ReadAsync()) datos.Add(new(r.GetByte(0),r.GetTimeSpan(1),r.GetTimeSpan(2),r.GetInt16(3)));
        return datos;
    }
    public async Task GuardarJornadasAsync(int profesionalId,IReadOnlyList<Jornada> jornadas)
    {
        using var cn=Conexion(); await cn.OpenAsync(); using var tx=cn.BeginTransaction();
        using var bloqueo=new SqlCommand("""
            DECLARE @Resultado INT,@Recurso NVARCHAR(255)=CONCAT(N'Lock_Agenda_Prof_',@Id);
            EXEC @Resultado=sys.sp_getapplock @Resource=@Recurso,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
            IF @Resultado<0 THROW 51201,N'La agenda está ocupada. Intentá nuevamente.',1;
            DELETE dbo.JornadaProfesional WHERE ProfesionalId=@Id;
            """,cn,tx);
        Param(bloqueo,"@Id",SqlDbType.Int,profesionalId);
        try
        {
            await bloqueo.ExecuteNonQueryAsync();
            foreach (var j in jornadas)
            {
                using var insertar=new SqlCommand("INSERT dbo.JornadaProfesional(ProfesionalId,DiaSemana,Desde,Hasta,PasoMinutos) VALUES(@Id,@Dia,@Desde,@Hasta,@Paso);",cn,tx);
                Param(insertar,"@Id",SqlDbType.Int,profesionalId); Param(insertar,"@Dia",SqlDbType.TinyInt,j.DiaSemana);
                Param(insertar,"@Desde",SqlDbType.Time,j.Desde); Param(insertar,"@Hasta",SqlDbType.Time,j.Hasta);
                Param(insertar,"@Paso",SqlDbType.SmallInt,j.PasoMinutos); await insertar.ExecuteNonQueryAsync();
            }
            using var validar=new SqlCommand("""
                DECLARE @Zona NVARCHAR(100)=(SELECT ZonaHorariaId FROM dbo.ConfiguracionNegocio WHERE ConfiguracionId=1);
                IF EXISTS (
                    SELECT 1 FROM dbo.Turno t JOIN dbo.EstadoTurno e ON e.EstadoTurnoId=t.EstadoTurnoId
                    CROSS APPLY (SELECT CONVERT(DATETIME2(0),t.InicioUtc AT TIME ZONE 'UTC' AT TIME ZONE @Zona) AS Inicio,
                                        CONVERT(DATETIME2(0),t.FinUtc AT TIME ZONE 'UTC' AT TIME ZONE @Zona) AS Fin) h
                    WHERE t.ProfesionalId=@Id AND t.InicioUtc>SYSUTCDATETIME() AND e.Codigo<>'CANCELADO'
                    AND NOT EXISTS (SELECT 1 FROM dbo.JornadaProfesional j WHERE j.ProfesionalId=@Id
                        AND j.DiaSemana=DATEDIFF(DAY,CONVERT(DATE,'19000101'),CONVERT(DATE,h.Inicio))%7
                        AND CONVERT(DATE,h.Inicio)=CONVERT(DATE,h.Fin)
                        AND CONVERT(TIME,h.Inicio)>=j.Desde AND CONVERT(TIME,h.Fin)<=j.Hasta
                        AND DATEDIFF(MINUTE,j.Desde,CONVERT(TIME,h.Inicio))%j.PasoMinutos=0))
                    THROW 51202,N'Hay turnos futuros fuera de la nueva jornada. Conservá esos horarios.',1;
                """,cn,tx);
            Param(validar,"@Id",SqlDbType.Int,profesionalId); await validar.ExecuteNonQueryAsync();
            tx.Commit();
        }
        catch (SqlException ex) when (ex.Number is 51201 or 51202)
        { throw new OperacionException(ex.Message); }
    }

    public async Task<CorreoPendiente?> ReclamarAsync()
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("""
            UPDATE dbo.CorreoSalida SET Estado='Error',ReservaHastaUtc=NULL,Detalle=N'Se agotaron los intentos tras interrumpirse el envío.'
            WHERE Estado='Procesando' AND ReservaHastaUtc<SYSUTCDATETIME() AND Intentos>=5;
            UPDATE c SET Estado='Error',Intentos=5,Detalle=N'No se envía: el turno fue cancelado o ya comenzó.'
            FROM dbo.CorreoSalida c JOIN dbo.Turno t ON t.TurnoId=c.TurnoId
            JOIN dbo.EstadoTurno e ON e.EstadoTurnoId=t.EstadoTurnoId
            WHERE (c.Estado IN ('Pendiente','Error') OR(c.Estado='Procesando' AND c.ReservaHastaUtc<SYSUTCDATETIME()))
              AND (t.InicioUtc<=SYSUTCDATETIME() OR e.Codigo='CANCELADO');
            ;WITH Candidato AS (
                SELECT TOP(1) * FROM dbo.CorreoSalida WITH (UPDLOCK,READPAST,READCOMMITTEDLOCK)
                WHERE Intentos<5 AND ProximoIntentoUtc<=SYSUTCDATETIME()
                  AND (Estado IN ('Pendiente','Error') OR (Estado='Procesando' AND ReservaHastaUtc<SYSUTCDATETIME()))
                ORDER BY CorreoId)
            UPDATE Candidato SET Estado='Procesando',Intentos=Intentos+1,ReservaHastaUtc=DATEADD(MINUTE,5,SYSUTCDATETIME()),TokenReclamo=NEWID()
            OUTPUT INSERTED.CorreoId,INSERTED.Destinatario,INSERTED.Asunto,INSERTED.Cuerpo,INSERTED.TokenReclamo;
            """,cn);
        using var r=await cmd.ExecuteReaderAsync();
        return await r.ReadAsync()?new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetGuid(4)):null;
    }
    public async Task<bool> FinalizarAsync(long id,Guid tokenReclamo,bool enviado)
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("""
            UPDATE dbo.CorreoSalida SET Estado=CASE WHEN @Enviado=1 THEN 'Enviado' ELSE 'Error' END,
              EnviadoEnUtc=CASE WHEN @Enviado=1 THEN SYSUTCDATETIME() ELSE NULL END,
              Detalle=CASE WHEN @Enviado=1 THEN N'Aceptado por el servidor SMTP.' ELSE N'No se pudo enviar. Revisá la configuración SMTP. Se reintentará hasta 5 veces.' END,
              ProximoIntentoUtc=DATEADD(MINUTE,Intentos*2,SYSUTCDATETIME()),ReservaHastaUtc=NULL
            WHERE CorreoId=@Id AND Estado='Procesando' AND TokenReclamo=@Token AND ReservaHastaUtc>SYSUTCDATETIME();
            """,cn);
        Param(cmd,"@Id",SqlDbType.BigInt,id); Param(cmd,"@Enviado",SqlDbType.Bit,enviado);
        Param(cmd,"@Token",SqlDbType.UniqueIdentifier,tokenReclamo); return await cmd.ExecuteNonQueryAsync()==1;
    }
    public async Task<bool> ReclamoVigenteAsync(long id,Guid tokenReclamo)
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("""
            SELECT COUNT(*) FROM dbo.CorreoSalida c JOIN dbo.Turno t ON t.TurnoId=c.TurnoId
            JOIN dbo.EstadoTurno e ON e.EstadoTurnoId=t.EstadoTurnoId WHERE c.CorreoId=@Id
                AND c.TokenReclamo=@Token AND c.Estado='Procesando' AND c.ReservaHastaUtc>SYSUTCDATETIME()
                AND t.InicioUtc>SYSUTCDATETIME() AND e.Codigo<>'CANCELADO';
            """,cn);
        Param(cmd,"@Id",SqlDbType.BigInt,id); Param(cmd,"@Token",SqlDbType.UniqueIdentifier,tokenReclamo);
        return (int)(await cmd.ExecuteScalarAsync())!>0;
    }
    public async Task<IReadOnlyList<CorreoEstado>> ListarCorreosAsync()
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("SELECT TOP(200) CorreoId,TurnoId,Destinatario,Estado,Intentos,Detalle FROM dbo.CorreoSalida ORDER BY CorreoId DESC;",cn);
        using var r=await cmd.ExecuteReaderAsync(); var datos=new List<CorreoEstado>();
        while (await r.ReadAsync()) datos.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetString(2),r.GetString(3),r.GetInt32(4),r.GetString(5)));
        return datos;
    }
    public async Task ReintentarAsync(long id)
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("UPDATE dbo.CorreoSalida SET Estado='Pendiente',Intentos=0,ProximoIntentoUtc=SYSUTCDATETIME(),Detalle=N'Reintento solicitado.' WHERE CorreoId=@Id AND Estado='Error';",cn);
        Param(cmd,"@Id",SqlDbType.BigInt,id); await cmd.ExecuteNonQueryAsync();
    }
}
