using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Infraestructura.SqlServer.Repositorios;

public sealed partial class AppStore
{
    public async Task<IReadOnlyList<UsuarioGestion>> UsuariosAsync()
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("SELECT UsuarioId,Nombre,Rol,Activo,ProfesionalId,Version FROM dbo.Usuario ORDER BY Nombre;",cn);
        using var r=await cmd.ExecuteReaderAsync(); var lista=new List<UsuarioGestion>();
        while(await r.ReadAsync()) lista.Add(new(r.GetInt32(0),r.GetString(1),Enum.Parse<RolUsuario>(r.GetString(2)),r.GetBoolean(3),r.IsDBNull(4)?null:r.GetInt32(4),(byte[])r[5]));
        return lista;
    }
    public async Task GuardarUsuarioAsync(UsuarioGestion u,string? hash,int actorId,Guid tokenSesion)
    {
        using var cn=Conexion(); await cn.OpenAsync(); using var tx=cn.BeginTransaction();
        using var cmd=new SqlCommand("""
            DECLARE @Resultado INT;
            EXEC @Resultado=sys.sp_getapplock @Resource=N'SistemaTurnos_Usuarios',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
            IF @Resultado<0 THROW 53101,N'No se pudo bloquear la gestión de usuarios.',1;
            IF NOT EXISTS(SELECT 1 FROM dbo.Usuario WITH(UPDLOCK,HOLDLOCK) WHERE UsuarioId=@Actor AND Activo=1 AND Rol='Administrador' AND TokenSesion=@Token)
                THROW 53102,N'Solo un administrador activo puede gestionar usuarios.',1;
            IF @Id=@Actor AND (@Activo=0 OR @Rol<>'Administrador') THROW 53103,N'No podés desactivar tu cuenta ni cambiar tu propio rol.',1;
            IF @Rol='Profesional' AND NOT EXISTS(SELECT 1 FROM dbo.Profesional WHERE ProfesionalId=@Profesional AND Activo=1)
                THROW 53104,N'El profesional vinculado debe estar activo.',1;
            IF EXISTS(SELECT 1 FROM dbo.Usuario WHERE UsuarioId=@Id AND Rol='Administrador' AND Activo=1)
               AND (@Activo=0 OR @Rol<>'Administrador') AND (SELECT COUNT(*) FROM dbo.Usuario WHERE Rol='Administrador' AND Activo=1)<=1
                THROW 53105,N'Debe quedar al menos un administrador activo.',1;
            IF @Id=0
                INSERT dbo.Usuario(Nombre,PasswordHash,Rol,Activo,ProfesionalId) VALUES(@Nombre,@Hash,@Rol,@Activo,@Profesional);
            ELSE
            BEGIN
                UPDATE dbo.Usuario SET Nombre=@Nombre,Rol=@Rol,Activo=@Activo,ProfesionalId=@Profesional,
                    PasswordHash=COALESCE(@Hash,PasswordHash),
                    IntentosFallidos=CASE WHEN @Hash IS NULL THEN IntentosFallidos ELSE 0 END,
                    BloqueadoHastaUtc=CASE WHEN @Hash IS NULL THEN BloqueadoHastaUtc ELSE NULL END
                WHERE UsuarioId=@Id AND Version=@Version;
                IF @@ROWCOUNT=0 THROW 53106,N'Otro usuario modificó esta cuenta. Actualizá la lista.',1;
            END;
            """,cn,tx);
        Param(cmd,"@Id",SqlDbType.Int,u.Id); Param(cmd,"@Actor",SqlDbType.Int,actorId);
        Param(cmd,"@Token",SqlDbType.UniqueIdentifier,tokenSesion);
        Param(cmd,"@Nombre",SqlDbType.NVarChar,u.Nombre,50); Param(cmd,"@Rol",SqlDbType.VarChar,u.Rol.ToString(),20);
        Param(cmd,"@Activo",SqlDbType.Bit,u.Activo); Param(cmd,"@Profesional",SqlDbType.Int,u.ProfesionalId);
        Param(cmd,"@Hash",SqlDbType.NVarChar,hash,200); Param(cmd,"@Version",SqlDbType.Binary,u.Version,8);
        try { await cmd.ExecuteNonQueryAsync(); tx.Commit(); }
        catch(SqlException ex) when(ex.Number is >=53101 and <=53106) { throw new OperacionException(ex.Message); }
        catch(SqlException ex) when(ex.Number is 2601 or 2627) { throw new OperacionException("Ese nombre de usuario ya está registrado."); }
    }
    public async Task<CuentaTurno> CuentaAsync(long id)
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("""
            SELECT t.TurnoId,c.Nombre+N' '+c.Apellido,t.PrecioAcordado,
              COALESCE((SELECT SUM(Importe) FROM dbo.Cobro WHERE TurnoId=t.TurnoId AND AnuladoEnUtc IS NULL),0),t.Moneda,e.Codigo
            FROM dbo.Turno t JOIN dbo.Cliente c ON c.ClienteId=t.ClienteId
            JOIN dbo.EstadoTurno e ON e.EstadoTurnoId=t.EstadoTurnoId WHERE t.TurnoId=@Id;
            """,cn);
        Param(cmd,"@Id",SqlDbType.BigInt,id); using var r=await cmd.ExecuteReaderAsync();
        if(!await r.ReadAsync()) throw new OperacionException("El turno no existe.");
        return new(r.GetInt64(0),r.GetString(1),r.GetDecimal(2),r.GetDecimal(3),r.GetString(4),r.GetString(5));
    }
    public async Task<IReadOnlyList<CobroListado>> CobrosAsync(long id)
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("""
            SELECT c.CobroId,c.TurnoId,c.FechaCobroUtc,c.Importe,c.Moneda,c.Medio,u.Nombre,
                CONVERT(BIT,CASE WHEN c.AnuladoEnUtc IS NULL THEN 0 ELSE 1 END),COALESCE(c.MotivoAnulacion,N'')
            FROM dbo.Cobro c JOIN dbo.Usuario u ON u.UsuarioId=c.UsuarioId WHERE c.TurnoId=@Id ORDER BY c.CobroId DESC;
            """,cn);
        Param(cmd,"@Id",SqlDbType.BigInt,id); using var r=await cmd.ExecuteReaderAsync(); var lista=new List<CobroListado>();
        while(await r.ReadAsync()) lista.Add(new(r.GetInt64(0),r.GetInt64(1),DateTime.SpecifyKind(r.GetDateTime(2),DateTimeKind.Utc),r.GetDecimal(3),r.GetString(4),r.GetString(5),r.GetString(6),r.GetBoolean(7),r.GetString(8)));
        return lista;
    }
    public async Task<long> RegistrarCobroAsync(long id,decimal importe,MedioCobro medio,Guid solicitud,int actorId,Guid tokenSesion)
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("dbo.usp_Cobro_Registrar",cn) { CommandType=CommandType.StoredProcedure };
        Param(cmd,"@TurnoId",SqlDbType.BigInt,id); Param(cmd,"@UsuarioId",SqlDbType.Int,actorId);
        Param(cmd,"@TokenSesion",SqlDbType.UniqueIdentifier,tokenSesion);
        Param(cmd,"@Solicitud",SqlDbType.UniqueIdentifier,solicitud); Param(cmd,"@Medio",SqlDbType.VarChar,medio.ToString(),20);
        var p=cmd.Parameters.Add("@Importe",SqlDbType.Decimal); p.Precision=18; p.Scale=2; p.Value=importe;
        try { return (long)(await cmd.ExecuteScalarAsync())!; }
        catch(SqlException ex) when(ex.Number is >=53001 and <=53009) { throw new OperacionException(ex.Message); }
    }
    public async Task AnularCobroAsync(long id,string motivo,int actorId,Guid tokenSesion)
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("dbo.usp_Cobro_Anular",cn) { CommandType=CommandType.StoredProcedure };
        Param(cmd,"@CobroId",SqlDbType.BigInt,id); Param(cmd,"@UsuarioId",SqlDbType.Int,actorId); Param(cmd,"@Motivo",SqlDbType.NVarChar,motivo,500);
        Param(cmd,"@TokenSesion",SqlDbType.UniqueIdentifier,tokenSesion);
        try { await cmd.ExecuteNonQueryAsync(); }
        catch(SqlException ex) when(ex.Number is >=53001 and <=53009) { throw new OperacionException(ex.Message); }
    }
    public async Task<IReadOnlyList<ResumenCobro>> InformeAsync(DateTime desde,DateTime hasta,string zona)
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("""
            SELECT CONVERT(DATE,FechaCobroUtc AT TIME ZONE 'UTC' AT TIME ZONE @Zona) Dia,Moneda,
              SUM(Importe),SUM(CASE WHEN AnuladoEnUtc IS NOT NULL THEN Importe ELSE 0 END),
              SUM(CASE WHEN AnuladoEnUtc IS NULL THEN Importe ELSE 0 END),
              SUM(CASE WHEN AnuladoEnUtc IS NULL THEN 1 ELSE 0 END)
            FROM dbo.Cobro WHERE FechaCobroUtc>=@Desde AND FechaCobroUtc<@Hasta
            GROUP BY CONVERT(DATE,FechaCobroUtc AT TIME ZONE 'UTC' AT TIME ZONE @Zona),Moneda ORDER BY Dia,Moneda;
            """,cn);
        Param(cmd,"@Desde",SqlDbType.DateTime2,desde); Param(cmd,"@Hasta",SqlDbType.DateTime2,hasta); Param(cmd,"@Zona",SqlDbType.NVarChar,zona,100);
        using var r=await cmd.ExecuteReaderAsync(); var lista=new List<ResumenCobro>();
        while(await r.ReadAsync()) lista.Add(new(r.GetDateTime(0),r.GetString(1),r.GetDecimal(2),r.GetDecimal(3),r.GetDecimal(4),r.GetInt32(5)));
        return lista;
    }
}
