using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Infraestructura.SqlServer.Repositorios;

public sealed partial class AppStore(string cadena) : IAppStore, IAgendaStore, IColaCorreo, ISeguridadStore, ICajaStore
{
    private SqlConnection Conexion() => new(Seguridad.ConexionSegura.Validar(cadena));
    private static void Param(SqlCommand cmd, string name, SqlDbType type, object? value, int size = 0)
    {
        var p = size == 0 ? cmd.Parameters.Add(name, type) : cmd.Parameters.Add(name, type, size);
        p.Value = value ?? DBNull.Value;
    }
    public async Task<bool> HayUsuariosAsync()
    {
        using var cn = Conexion(); await cn.OpenAsync();
        using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Usuario;", cn);
        return (int)(await cmd.ExecuteScalarAsync())! > 0;
    }
    public async Task<UsuarioAcceso?> BuscarUsuarioAsync(string nombre)
    {
        using var cn = Conexion(); await cn.OpenAsync();
        using var cmd = new SqlCommand("SELECT UsuarioId,Nombre,PasswordHash,Activo,BloqueadoHastaUtc,Rol,ProfesionalId,TokenSesion FROM dbo.Usuario WHERE Nombre=@Nombre;", cn);
        Param(cmd, "@Nombre", SqlDbType.NVarChar, nombre, 50);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? new(r.GetInt32(0),r.GetString(1),r.GetString(2),r.GetBoolean(3),r.IsDBNull(4)?null:r.GetDateTime(4),
            Enum.Parse<RolUsuario>(r.GetString(5)),r.IsDBNull(6)?null:r.GetInt32(6),r.GetGuid(7)) : null;
    }
    public async Task RegistrarAdministradorAsync(string nombre, string hash)
    {
        using var cn = Conexion(); await cn.OpenAsync();
        using var tx = cn.BeginTransaction();
        using var cmd = new SqlCommand("""
            DECLARE @Resultado INT;
            EXEC @Resultado = sys.sp_getapplock @Resource=N'SistemaTurnos_PrimerAdmin',
                @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
            IF @Resultado < 0 THROW 51101, N'No se pudo iniciar la configuración.', 1;
            IF EXISTS (SELECT 1 FROM dbo.Usuario WITH (UPDLOCK,HOLDLOCK))
                THROW 51102, N'El administrador inicial ya existe.', 1;
            INSERT dbo.Usuario(Nombre,PasswordHash,Rol) VALUES(@Nombre,@Hash,'Administrador');
            """, cn, tx);
        Param(cmd,"@Nombre",SqlDbType.NVarChar,nombre,50);
        Param(cmd,"@Hash",SqlDbType.NVarChar,hash,200);
        try { await cmd.ExecuteNonQueryAsync(); tx.Commit(); }
        catch (SqlException ex) when (ex.Number is 51101 or 51102)
        { throw new OperacionException(ex.Message); }
    }
    public async Task RegistrarIntentoAsync(int usuarioId, bool correcto,Guid tokenEsperado)
    {
        using var cn = Conexion(); await cn.OpenAsync();
        using var cmd = new SqlCommand("""
            UPDATE dbo.Usuario SET
                IntentosFallidos = CASE WHEN @Correcto=1 THEN 0
                    WHEN BloqueadoHastaUtc <= SYSUTCDATETIME() THEN 1 ELSE IntentosFallidos+1 END,
                BloqueadoHastaUtc = CASE WHEN @Correcto=1 THEN NULL
                    WHEN BloqueadoHastaUtc <= SYSUTCDATETIME() THEN NULL
                    WHEN IntentosFallidos+1 >= 5 THEN DATEADD(MINUTE,15,SYSUTCDATETIME()) ELSE NULL END
            WHERE UsuarioId=@Id AND Activo=1 AND TokenSesion=@Token
                AND (BloqueadoHastaUtc IS NULL OR BloqueadoHastaUtc <= SYSUTCDATETIME());
            """, cn);
        Param(cmd,"@Id",SqlDbType.Int,usuarioId);
        Param(cmd,"@Token",SqlDbType.UniqueIdentifier,tokenEsperado);
        Param(cmd,"@Correcto",SqlDbType.Bit,correcto);
        if (await cmd.ExecuteNonQueryAsync() != 1)
            throw new OperacionException("Usuario o contraseña incorrectos.");
    }
    public async Task<EstadoSesion?> BuscarSesionAsync(int id)
    {
        using var cn=Conexion(); await cn.OpenAsync();
        using var cmd=new SqlCommand("""
            SELECT CONVERT(BIT,CASE WHEN u.Activo=1 AND (u.Rol<>'Profesional' OR p.Activo=1) THEN 1 ELSE 0 END),
                u.Rol,u.ProfesionalId,u.TokenSesion
            FROM dbo.Usuario u LEFT JOIN dbo.Profesional p ON p.ProfesionalId=u.ProfesionalId WHERE u.UsuarioId=@Id;
            """,cn);
        Param(cmd,"@Id",SqlDbType.Int,id); using var r=await cmd.ExecuteReaderAsync();
        return await r.ReadAsync()?new(r.GetBoolean(0),Enum.Parse<RolUsuario>(r.GetString(1)),r.IsDBNull(2)?null:r.GetInt32(2),r.GetGuid(3)):null;
    }

    public async Task<IReadOnlyList<RegistroCatalogo>> ListarAsync(Catalogo tipo)
    {
        string sql = tipo switch
        {
            Catalogo.Clientes => "SELECT ClienteId,Nombre,Apellido,Telefono,Email,Activo,Version FROM dbo.Cliente ORDER BY Nombre,Apellido;",
            Catalogo.Profesionales => "SELECT ProfesionalId,Nombre,Apellido,Telefono,Email,Activo,Version FROM dbo.Profesional ORDER BY Nombre,Apellido;",
            Catalogo.Servicios => "SELECT ServicioId,Nombre,DuracionMinutos,PrecioBase,Activo,Version FROM dbo.Servicio ORDER BY Nombre;",
            Catalogo.Pacientes => "SELECT p.PacienteId,p.Nombre,p.Apellido,p.ClienteResponsableId,p.Tipo,a.Especie,p.Activo,p.Version,p.Email FROM dbo.Paciente p LEFT JOIN dbo.PacienteAnimal a ON a.PacienteId=p.PacienteId ORDER BY p.Nombre;",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
        using var cn = Conexion(); await cn.OpenAsync();
        using var cmd = new SqlCommand(sql, cn);
        var lista = new List<RegistroCatalogo>();
        using (var r = await cmd.ExecuteReaderAsync())
        {
            string Texto(int i) => r.IsDBNull(i) ? "" : r.GetString(i);
            while (await r.ReadAsync())
                lista.Add(tipo switch
                {
                    Catalogo.Servicios => new() { Id=r.GetInt32(0), Nombre=Texto(1), DuracionMinutos=r.GetInt16(2), Precio=r.GetDecimal(3), Activo=r.GetBoolean(4), Version=(byte[])r[5] },
                    Catalogo.Pacientes => new() { Id=r.GetInt32(0), Nombre=Texto(1), Apellido=Texto(2), ResponsableId=r.GetInt32(3), TipoPaciente=Texto(4), Especie=Texto(5), Activo=r.GetBoolean(6), Version=(byte[])r[7], Email=Texto(8) },
                    _ => new() { Id=r.GetInt32(0), Nombre=Texto(1), Apellido=Texto(2), Telefono=Texto(3), Email=Texto(4), Activo=r.GetBoolean(5), Version=(byte[])r[6] }
                });
        }
        if (tipo == Catalogo.Profesionales)
        {
            using var relaciones = new SqlCommand("SELECT ProfesionalId,ServicioId FROM dbo.ProfesionalServicio WHERE Activo=1;", cn);
            using var r = await relaciones.ExecuteReaderAsync();
            var ids = new Dictionary<int,List<int>>();
            while (await r.ReadAsync())
            { int id = r.GetInt32(0); if (!ids.ContainsKey(id)) ids[id]=[]; ids[id].Add(r.GetInt32(1)); }
            lista = lista.Select(x=>x with { ServiciosIds=ids.GetValueOrDefault(x.Id) ?? [] }).ToList();
        }
        return lista;
    }

    public async Task GuardarAsync(Catalogo tipo, RegistroCatalogo r)
    {
        string tabla = tipo switch { Catalogo.Clientes=>"Cliente", Catalogo.Pacientes=>"Paciente", Catalogo.Profesionales=>"Profesional", Catalogo.Servicios=>"Servicio", _=>throw new ArgumentOutOfRangeException(nameof(tipo)) };
        string campos, valores, actualizar;
        if (tipo == Catalogo.Servicios)
        { campos="Nombre,DuracionMinutos,PrecioBase,Activo"; valores="@Nombre,@Duracion,@Precio,@Activo"; actualizar="Nombre=@Nombre,DuracionMinutos=@Duracion,PrecioBase=@Precio,Activo=@Activo"; }
        else if (tipo == Catalogo.Pacientes)
        { campos="Nombre,Apellido,ClienteResponsableId,Tipo,Activo,Email"; valores="@Nombre,@Apellido,@Responsable,@Tipo,@Activo,@Email"; actualizar="Nombre=@Nombre,Apellido=@Apellido,ClienteResponsableId=@Responsable,Tipo=@Tipo,Activo=@Activo,Email=@Email"; }
        else
        { campos="Nombre,Apellido,Telefono,Email,Activo"; valores="@Nombre,@Apellido,@Telefono,@Email,@Activo"; actualizar="Nombre=@Nombre,Apellido=@Apellido,Telefono=@Telefono,Email=@Email,Activo=@Activo"; }
        // Los identificadores SQL provienen exclusivamente del enum, nunca del usuario.
        string sql = r.Id == 0
            ? $"INSERT dbo.{tabla}({campos}) OUTPUT INSERTED.{tabla}Id VALUES({valores});"
            : $"UPDATE dbo.{tabla} SET {actualizar} OUTPUT INSERTED.{tabla}Id WHERE {tabla}Id=@Id AND Version=@Version;";
        using var cn = Conexion(); await cn.OpenAsync();
        using var tx = cn.BeginTransaction();
        using var cmd = new SqlCommand(sql, cn, tx);
        Param(cmd,"@Nombre",SqlDbType.NVarChar,r.Nombre,100);
        Param(cmd,"@Apellido",SqlDbType.NVarChar,r.Apellido,100);
        Param(cmd,"@Telefono",SqlDbType.NVarChar,r.Telefono,40);
        Param(cmd,"@Email",SqlDbType.NVarChar,r.Email,254);
        Param(cmd,"@Activo",SqlDbType.Bit,r.Activo);
        Param(cmd,"@Duracion",SqlDbType.SmallInt,r.DuracionMinutos);
        var precio = cmd.Parameters.Add("@Precio",SqlDbType.Decimal); precio.Precision=18; precio.Scale=2; precio.Value=r.Precio;
        Param(cmd,"@Responsable",SqlDbType.Int,r.ResponsableId);
        Param(cmd,"@Tipo",SqlDbType.Char,r.TipoPaciente,1);
        Param(cmd,"@Id",SqlDbType.Int,r.Id);
        Param(cmd,"@Version",SqlDbType.Binary,r.Version,8);
        object? resultado = await cmd.ExecuteScalarAsync();
        if (resultado is not int id) throw new OperacionException("Otro usuario modificó el registro. Actualizá la lista.");
        if (tipo == Catalogo.Pacientes)
        {
            using var animal = new SqlCommand("""
                IF @Tipo='A'
                BEGIN
                    UPDATE dbo.PacienteAnimal SET Especie=@Especie WHERE PacienteId=@Id;
                    IF @@ROWCOUNT=0 INSERT dbo.PacienteAnimal(PacienteId,Especie) VALUES(@Id,@Especie);
                END
                ELSE DELETE dbo.PacienteAnimal WHERE PacienteId=@Id;
                """,cn,tx);
            Param(animal,"@Tipo",SqlDbType.Char,r.TipoPaciente,1);
            Param(animal,"@Id",SqlDbType.Int,id);
            Param(animal,"@Especie",SqlDbType.NVarChar,r.Especie,60);
            await animal.ExecuteNonQueryAsync();
        }
        if (tipo == Catalogo.Profesionales)
        {
            using var desactivar = new SqlCommand("UPDATE dbo.ProfesionalServicio SET Activo=0 WHERE ProfesionalId=@Id;",cn,tx);
            Param(desactivar,"@Id",SqlDbType.Int,id); await desactivar.ExecuteNonQueryAsync();
            foreach (int servicio in r.ServiciosIds.Distinct())
            {
                using var relacion = new SqlCommand("""
                    UPDATE dbo.ProfesionalServicio SET Activo=1 WHERE ProfesionalId=@Id AND ServicioId=@Servicio;
                    IF @@ROWCOUNT=0 INSERT dbo.ProfesionalServicio(ProfesionalId,ServicioId) VALUES(@Id,@Servicio);
                    """,cn,tx);
                Param(relacion,"@Id",SqlDbType.Int,id); Param(relacion,"@Servicio",SqlDbType.Int,servicio);
                await relacion.ExecuteNonQueryAsync();
            }
        }
        tx.Commit();
    }

    public async Task<Configuracion> ConfiguracionAsync()
    {
        using var cn = Conexion(); await cn.OpenAsync();
        using var cmd = new SqlCommand("""
            SELECT c.NombreComercial,r.Codigo,c.EtiquetaCliente,c.EtiquetaPaciente,c.UsaPacientes,c.ZonaHorariaId,c.Moneda
            FROM dbo.ConfiguracionNegocio c JOIN dbo.Rubro r ON r.RubroId=c.RubroId WHERE c.ConfiguracionId=1;
            """,cn);
        using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) throw new OperacionException("Ejecutá la migración 002 para configurar el negocio.");
        return new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetBoolean(4),r.GetString(5),r.GetString(6));
    }
    public async Task GuardarConfiguracionAsync(Configuracion c)
    {
        using var cn = Conexion(); await cn.OpenAsync();
        using var cmd = new SqlCommand("""
            DECLARE @Rubro SMALLINT=(SELECT RubroId FROM dbo.Rubro WHERE Codigo=@RubroCodigo);
            IF @Rubro IS NULL THROW 51103,N'Rubro inválido.',1;
            UPDATE dbo.ConfiguracionNegocio SET NombreComercial=@Nombre,RubroId=@Rubro,
              EtiquetaCliente=@Cliente,EtiquetaPaciente=@Paciente,UsaPacientes=@Usa,
              ZonaHorariaId=@Zona,Moneda=@Moneda WHERE ConfiguracionId=1;
            """,cn);
        Param(cmd,"@RubroCodigo",SqlDbType.VarChar,c.Rubro,30);
        Param(cmd,"@Nombre",SqlDbType.NVarChar,c.Nombre,150);
        Param(cmd,"@Cliente",SqlDbType.NVarChar,c.EtiquetaCliente,40);
        Param(cmd,"@Paciente",SqlDbType.NVarChar,c.EtiquetaPaciente,40);
        Param(cmd,"@Usa",SqlDbType.Bit,c.UsaPacientes);
        Param(cmd,"@Zona",SqlDbType.NVarChar,c.ZonaHoraria,100);
        Param(cmd,"@Moneda",SqlDbType.Char,c.Moneda,3);
        await cmd.ExecuteNonQueryAsync();
    }
    public async Task<IReadOnlyList<TurnoListado>> AgendaAsync(DateTime desdeUtc, DateTime hastaUtc)
    {
        using var cn = Conexion(); await cn.OpenAsync();
        using var cmd = new SqlCommand("""
            SELECT t.TurnoId,t.InicioUtc,t.FinUtc,c.Nombre+N' '+c.Apellido,
                COALESCE(p.Nombre+N' '+COALESCE(p.Apellido,N''),N''),
                pr.Nombre+N' '+pr.Apellido,s.Nombre,e.Nombre,t.PrecioAcordado,t.Moneda,t.ProfesionalId,e.Codigo
            FROM dbo.Turno t JOIN dbo.Cliente c ON c.ClienteId=t.ClienteId
            LEFT JOIN dbo.Paciente p ON p.PacienteId=t.PacienteId
            JOIN dbo.Profesional pr ON pr.ProfesionalId=t.ProfesionalId
            JOIN dbo.Servicio s ON s.ServicioId=t.ServicioId
            JOIN dbo.EstadoTurno e ON e.EstadoTurnoId=t.EstadoTurnoId
            WHERE t.InicioUtc < @Hasta AND t.FinUtc > @Desde ORDER BY t.InicioUtc;
            """,cn);
        Param(cmd,"@Desde",SqlDbType.DateTime2,desdeUtc); Param(cmd,"@Hasta",SqlDbType.DateTime2,hastaUtc);
        using var r = await cmd.ExecuteReaderAsync(); var lista = new List<TurnoListado>();
        while (await r.ReadAsync()) lista.Add(new(r.GetInt64(0),DateTime.SpecifyKind(r.GetDateTime(1),DateTimeKind.Utc),
            DateTime.SpecifyKind(r.GetDateTime(2),DateTimeKind.Utc),r.GetString(3),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7),r.GetDecimal(8),r.GetString(9),r.GetInt32(10),r.GetString(11)));
        return lista;
    }
}
