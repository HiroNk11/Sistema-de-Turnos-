using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTurnos.Dominio.Entidades;
using SistemaTurnos.Infraestructura.SqlServer.Repositorios;
using SistemaTurnos.Infraestructura.SqlServer.Transacciones;

// Ejecutar con la cuenta Windows propietaria de LocalDB.
// Se crean filas exclusivas de esta ejecución y se eliminan al terminar.
await using var temporal = args.FirstOrDefault()=="--aislada"
    ? await BaseTemporal.CrearAsync(args.ElementAtOrDefault(1)??throw new ArgumentException("Indicá la carpeta database."),
        args.ElementAtOrDefault(2)??@"(localdb)\MSSQLLocalDB") : null;
string cadena = temporal?.Cadena ?? args.FirstOrDefault()
    ?? @"Server=(localdb)\MSSQLLocalDB;Database=SistemaTurnos;Integrated Security=true;Encrypt=false;Connect Timeout=10";
string marca = "PRUEBA_" + Guid.NewGuid().ToString("N");
int cliente = 0, profesional = 0, servicio = 0, actorCaja = 0;
Guid tokenCaja = Guid.Empty;
DateTime inicio = new(2040, 1, 10, 12, 0, 0, DateTimeKind.Utc);
int aprobadas = 0;

try
{
    using (var conexion = new SqlConnection(cadena))
    {
        await conexion.OpenAsync();
        using var tx = conexion.BeginTransaction();
        using var cmd = new SqlCommand("""
            IF OBJECT_ID(N'dbo.usp_Turno_Crear', N'P') IS NULL
                THROW 52001, N'Falta instalar usp_Turno_Crear.', 1;
            INSERT dbo.Cliente(Nombre,Apellido) VALUES(@Marca,N'Integración');
            DECLARE @Cliente INT = CONVERT(INT,SCOPE_IDENTITY());
            INSERT dbo.Profesional(Nombre,Apellido) VALUES(@Marca,N'Integración');
            DECLARE @Profesional INT = CONVERT(INT,SCOPE_IDENTITY());
            INSERT dbo.Servicio(Nombre,DuracionMinutos,PrecioBase) VALUES(@Marca,30,100);
            DECLARE @Servicio INT = CONVERT(INT,SCOPE_IDENTITY());
            INSERT dbo.ProfesionalServicio(ProfesionalId,ServicioId) VALUES(@Profesional,@Servicio);
            INSERT dbo.JornadaProfesional(ProfesionalId,DiaSemana,Desde,Hasta,PasoMinutos)
            SELECT @Profesional,d,'00:00','23:59',15 FROM (VALUES(0),(1),(2),(3),(4),(5),(6)) dias(d);
            SELECT @Cliente,@Profesional,@Servicio;
            """, conexion, tx);
        cmd.Parameters.Add("@Marca", SqlDbType.NVarChar, 100).Value = marca;
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) throw new Exception("No se creó la fixture.");
            cliente = reader.GetInt32(0);
            profesional = reader.GetInt32(1);
            servicio = reader.GetInt32(2);
        }
        tx.Commit();
    }

    if(temporal is not null)
    {
        using var cn=new SqlConnection(cadena); await cn.OpenAsync();
        using(var alta=new SqlCommand("CREATE USER PruebaRuntime WITHOUT LOGIN; ALTER ROLE SistemaTurnos_ApiRuntime ADD MEMBER PruebaRuntime; EXECUTE AS USER='PruebaRuntime';",cn))
            await alta.ExecuteNonQueryAsync();
        using(var permisos=new SqlCommand("SELECT HAS_PERMS_BY_NAME('dbo.Turno','OBJECT','UPDATE'), HAS_PERMS_BY_NAME('dbo.Cobro','OBJECT','DELETE'), HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER ANY USER');",cn))
        using(var lector=await permisos.ExecuteReaderAsync())
        { await lector.ReadAsync(); Exigir(lector.GetInt32(0)==0&&lector.GetInt32(1)==0&&lector.GetInt32(2)==0,"El rol runtime no edita reservas, borra cobros ni administra usuarios SQL"); }
        bool denegado=false;
        using(var directo=new SqlCommand("UPDATE dbo.Turno SET PrecioAcordado=0 WHERE 1=0;",cn))
            try {await directo.ExecuteNonQueryAsync();} catch(SqlException ex) when(ex.Number==229){denegado=true;}
        Exigir(denegado,"SQL deniega DML directo aun sin filas afectadas");
        using(var proc=new SqlCommand("dbo.usp_Turno_Crear",cn){CommandType=CommandType.StoredProcedure})
        {
            proc.Parameters.AddWithValue("@ClienteId",cliente); proc.Parameters.AddWithValue("@PacienteId",DBNull.Value);
            proc.Parameters.AddWithValue("@ProfesionalId",profesional);proc.Parameters.AddWithValue("@ServicioId",servicio);
            proc.Parameters.AddWithValue("@InicioUtc",inicio.AddDays(10));proc.Parameters.AddWithValue("@FinUtc",inicio.AddDays(10).AddMinutes(30));
            proc.Parameters.AddWithValue("@PrecioAcordado",100m);proc.Parameters.AddWithValue("@Moneda","ARS");
            Exigir((long)(await proc.ExecuteScalarAsync())!>0,"El rol runtime ejecuta la reserva mediante el procedimiento y su cadena de propiedad");
        }
        using(var revertir=new SqlCommand("REVERT;",cn)) await revertir.ExecuteNonQueryAsync();
    }
    long primero = await Crear(inicio);
    Exigir(primero > 0 && await Contar(inicio) == 1, "Creación y persistencia");

    Exigir(await Rechaza(() => Crear(inicio.AddMinutes(15)), 51006),
        "Rechazo de superposición parcial");
    Exigir(await Rechaza(() => Crear(inicio), 51006), "Rechazo de duplicado");

    await Crear(inicio.AddMinutes(30));
    Exigir(await Contar(inicio.AddMinutes(30)) == 1, "Turnos consecutivos permitidos");

    DateTime canceladoInicio = inicio.AddDays(1);
    long cancelado = await Crear(canceladoInicio);
    using (var conexion = new SqlConnection(cadena))
    {
        await conexion.OpenAsync();
        using var cmd = new SqlCommand("""
            UPDATE dbo.Turno SET EstadoTurnoId =
                (SELECT EstadoTurnoId FROM dbo.EstadoTurno WHERE Codigo='CANCELADO')
            WHERE TurnoId=@Id AND ClienteId=@Cliente;
            """, conexion);
        cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = cancelado;
        cmd.Parameters.Add("@Cliente", SqlDbType.Int).Value = cliente;
        await cmd.ExecuteNonQueryAsync();
    }
    await Crear(canceladoInicio);
    Exigir(await Contar(canceladoInicio) == 1, "Cancelados no bloquean la reserva");

    DateTime rollbackInicio = inicio.AddDays(2);
    using (var unidad = new SqlUnidadDeTrabajo(cadena))
    {
        await new TurnoRepository(unidad).CrearTurnoAsync(Nuevo(rollbackInicio));
        unidad.Rollback();
    }
    Exigir(await Contar(rollbackInicio) == 0, "Rollback explícito");
    using (var unidad = new SqlUnidadDeTrabajo(cadena))
        await new TurnoRepository(unidad).CrearTurnoAsync(Nuevo(rollbackInicio));
    Exigir(await Contar(rollbackInicio) == 0, "Dispose revierte transacción pendiente");

    DateTime simultaneoInicio = inicio.AddDays(3);
    var salida = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var listas = new CountdownEvent(2);
    Task<int> Intentar() => Task.Run(async () =>
    {
        using var unidad = new SqlUnidadDeTrabajo(cadena);
        listas.Signal();
        await salida.Task;
        try
        {
            await new TurnoRepository(unidad).CrearTurnoAsync(Nuevo(simultaneoInicio));
            // Conserva el bloqueo para que la otra sesión tenga que esperar.
            await Task.Delay(300);
            unidad.Commit();
            return 0;
        }
        catch (SqlException ex) when (ex.Number == 51006)
        {
            unidad.Rollback();
            return ex.Number;
        }
    });
    var a = Intentar();
    var b = Intentar();
    bool preparadas = listas.Wait(TimeSpan.FromSeconds(20));
    salida.TrySetResult(true);
    int[] resultados = await Task.WhenAll(a, b);
    Exigir(preparadas && resultados.Count(x => x == 0) == 1
        && resultados.Count(x => x == 51006) == 1
        && await Contar(simultaneoInicio) == 1,
        "Concurrencia: una reserva confirmada y otra rechazada");

    using(var cn=new SqlConnection(cadena))
    {
        await cn.OpenAsync();
        using var cmd=new SqlCommand("INSERT dbo.Usuario(Nombre,PasswordHash,Rol) OUTPUT INSERTED.UsuarioId VALUES(@Nombre,N'CUENTA_PRUEBA_SIN_LOGIN','Caja');",cn);
        cmd.Parameters.Add("@Nombre",SqlDbType.NVarChar,50).Value=marca;
        actorCaja=(int)(await cmd.ExecuteScalarAsync())!;
    }
    var cajaStore=new SistemaTurnos.Infraestructura.SqlServer.Repositorios.AppStore(cadena);
    tokenCaja=(await cajaStore.BuscarSesionAsync(actorCaja))!.TokenSesion;
    Guid solicitud=Guid.NewGuid();
    long cobroId=await cajaStore.RegistrarCobroAsync(primero,60,SistemaTurnos.Aplicacion.Modelos.MedioCobro.Efectivo,solicitud,actorCaja,tokenCaja);
    Exigir(await cajaStore.RegistrarCobroAsync(primero,60,SistemaTurnos.Aplicacion.Modelos.MedioCobro.Efectivo,solicitud,actorCaja,tokenCaja)==cobroId,
        "Reintentar la misma solicitud no duplica el cobro");
    Exigir((await cajaStore.CuentaAsync(primero)).Saldo==40,"Pago parcial deja saldo pendiente");
    bool exceso=false;
    try { await cajaStore.RegistrarCobroAsync(primero,41,SistemaTurnos.Aplicacion.Modelos.MedioCobro.Tarjeta,Guid.NewGuid(),actorCaja,tokenCaja); }
    catch(SistemaTurnos.Aplicacion.Modelos.OperacionException) { exceso=true; }
    Exigir(exceso&&(await cajaStore.CuentaAsync(primero)).Saldo==40,"SQL rechaza importe superior al saldo sin alterar la cuenta");
    var simultaneos=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>
    {
        try { await cajaStore.RegistrarCobroAsync(primero,30,SistemaTurnos.Aplicacion.Modelos.MedioCobro.Transferencia,Guid.NewGuid(),actorCaja,tokenCaja); return true; }
        catch(SistemaTurnos.Aplicacion.Modelos.OperacionException) { return false; }
    }));
    Exigir(simultaneos.Count(x=>x)==1&&(await cajaStore.CuentaAsync(primero)).Saldo==10,"Cobros concurrentes no superan el saldo");
    await cajaStore.AnularCobroAsync(cobroId,"Anulación de prueba",actorCaja,tokenCaja);
    Exigir((await cajaStore.CuentaAsync(primero)).Saldo==70&&(await cajaStore.CobrosAsync(primero)).Any(x=>x.Id==cobroId&&x.Anulado),
        "Anulación recompone saldo y conserva historial");
    using(var cn=new SqlConnection(cadena))
    {
        await cn.OpenAsync();
        using var cmd=new SqlCommand("UPDATE dbo.Usuario SET Activo=0 WHERE UsuarioId=@Id;",cn);
        cmd.Parameters.Add("@Id",SqlDbType.Int).Value=actorCaja; await cmd.ExecuteNonQueryAsync();
    }
    var revocada=await cajaStore.BuscarSesionAsync(actorCaja);
    Exigir(revocada is {Activo:false}&&revocada.TokenSesion!=tokenCaja,"Cambiar la cuenta rota su token de sesión en SQL");
    bool tokenRechazado=false;
    try { await cajaStore.RegistrarCobroAsync(primero,1,SistemaTurnos.Aplicacion.Modelos.MedioCobro.Efectivo,Guid.NewGuid(),actorCaja,tokenCaja); }
    catch(SistemaTurnos.Aplicacion.Modelos.OperacionException){ tokenRechazado=true; }
    Exigir(tokenRechazado,"Un token anterior no autoriza cobros después de revocar la cuenta");
    var movimiento=(await cajaStore.CobrosAsync(primero)).First(x=>!x.Anulado);
    using(var cn=new SqlConnection(cadena))
    {
        await cn.OpenAsync(); bool inmutable=false;
        using var cmd=new SqlCommand("UPDATE dbo.Cobro SET Importe=Importe+1 WHERE CobroId=@Id;",cn);
        cmd.Parameters.Add("@Id",SqlDbType.BigInt).Value=movimiento.Id;
        try { await cmd.ExecuteNonQueryAsync(); } catch(SqlException ex) when(ex.Number==53201){inmutable=true;}
        Exigir(inmutable,"No se editan los importes originales de un cobro");
    }
    using(var cn=new SqlConnection(cadena))
    {
        await cn.OpenAsync(); bool monedaProtegida=false;
        using var cmd=new SqlCommand("UPDATE dbo.Turno SET Moneda='USD' WHERE TurnoId=@Id;",cn);
        cmd.Parameters.Add("@Id",SqlDbType.BigInt).Value=primero;
        try { await cmd.ExecuteNonQueryAsync(); } catch(SqlException ex) when(ex.Number==53203){monedaProtegida=true;}
        Exigir(monedaProtegida,"La moneda no cambia cuando existen movimientos de caja");
    }
    Guid propietario=Guid.NewGuid(); long aviso;
    using(var cn=new SqlConnection(cadena))
    {
        await cn.OpenAsync();
        using var cmd=new SqlCommand("""
            INSERT dbo.CorreoSalida(TurnoId,Destinatario,Asunto,Cuerpo,Estado,Intentos,ReservaHastaUtc,TokenReclamo)
            OUTPUT INSERTED.CorreoId VALUES(@Turno,N'prueba@example.invalid',N'Prueba',N'Sin envío real','Procesando',1,DATEADD(MINUTE,5,SYSUTCDATETIME()),@Token);
            """,cn);
        cmd.Parameters.Add("@Turno",SqlDbType.BigInt).Value=primero;
        cmd.Parameters.Add("@Token",SqlDbType.UniqueIdentifier).Value=propietario;
        aviso=(long)(await cmd.ExecuteScalarAsync())!;
    }
    Exigir(!await cajaStore.FinalizarAsync(aviso,Guid.NewGuid(),true),"La cola real rechaza una finalización de otro propietario");
    Exigir(await cajaStore.ReclamoVigenteAsync(aviso,propietario)&&await cajaStore.FinalizarAsync(aviso,propietario,true),"El propietario vigente puede finalizar su aviso de prueba");
    Console.WriteLine($"RESULTADO: {aprobadas} pruebas aprobadas.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    Environment.ExitCode = 1;
}
finally
{
    if (cliente > 0 && profesional > 0 && servicio > 0)
    {
        try
        {
            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();
            using var tx = conexion.BeginTransaction();
            using var cmd = new SqlCommand("""
                DELETE c FROM dbo.Cobro c JOIN dbo.Turno t ON t.TurnoId=c.TurnoId
                  WHERE t.ClienteId=@Cliente AND t.ProfesionalId=@Profesional AND t.ServicioId=@Servicio;
                DELETE c FROM dbo.CorreoSalida c JOIN dbo.Turno t ON t.TurnoId=c.TurnoId
                  WHERE t.ClienteId=@Cliente AND t.ProfesionalId=@Profesional AND t.ServicioId=@Servicio;
                DELETE dbo.Turno WHERE ClienteId=@Cliente AND ProfesionalId=@Profesional AND ServicioId=@Servicio;
                DELETE dbo.JornadaProfesional WHERE ProfesionalId=@Profesional;
                DELETE dbo.ProfesionalServicio WHERE ProfesionalId=@Profesional AND ServicioId=@Servicio;
                DELETE dbo.Servicio WHERE ServicioId=@Servicio AND Nombre=@Marca;
                DELETE dbo.Profesional WHERE ProfesionalId=@Profesional AND Nombre=@Marca;
                DELETE dbo.Cliente WHERE ClienteId=@Cliente AND Nombre=@Marca;
                DELETE dbo.Usuario WHERE UsuarioId=@Actor AND Nombre=@Marca;
                """, conexion, tx);
            cmd.Parameters.Add("@Cliente", SqlDbType.Int).Value = cliente;
            cmd.Parameters.Add("@Profesional", SqlDbType.Int).Value = profesional;
            cmd.Parameters.Add("@Servicio", SqlDbType.Int).Value = servicio;
            cmd.Parameters.Add("@Marca", SqlDbType.NVarChar, 100).Value = marca;
            cmd.Parameters.Add("@Actor",SqlDbType.Int).Value=actorCaja;
            await cmd.ExecuteNonQueryAsync();
            tx.Commit();
            Console.WriteLine("Datos de esta prueba eliminados.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Falló la limpieza de {marca}: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}

Turno Nuevo(DateTime fecha) => new()
{
    ClienteId = cliente, ProfesionalId = profesional, ServicioId = servicio,
    InicioUtc = fecha, FinUtc = fecha.AddMinutes(30), PrecioAcordado = 100, Moneda = "ARS"
};

async Task<long> Crear(DateTime fecha)
{
    using var unidad = new SqlUnidadDeTrabajo(cadena);
    long id = await new TurnoRepository(unidad).CrearTurnoAsync(Nuevo(fecha));
    unidad.Commit();
    return id;
}

async Task<int> Contar(DateTime fecha)
{
    using var conexion = new SqlConnection(cadena);
    await conexion.OpenAsync();
    using var cmd = new SqlCommand("""
        SELECT COUNT(*) FROM dbo.Turno t
        JOIN dbo.EstadoTurno e ON e.EstadoTurnoId=t.EstadoTurnoId
        WHERE t.ClienteId=@Cliente AND t.ProfesionalId=@Profesional
          AND t.InicioUtc=@Inicio AND e.Codigo<>'CANCELADO';
        """, conexion);
    cmd.Parameters.Add("@Cliente", SqlDbType.Int).Value = cliente;
    cmd.Parameters.Add("@Profesional", SqlDbType.Int).Value = profesional;
    var parametro = cmd.Parameters.Add("@Inicio", SqlDbType.DateTime2);
    parametro.Scale = 0; parametro.Value = fecha;
    return (int)(await cmd.ExecuteScalarAsync())!;
}

async Task<bool> Rechaza(Func<Task<long>> accion, int numero)
{
    try { await accion(); return false; }
    catch (SqlException ex) when (ex.Number == numero) { return true; }
}

void Exigir(bool condicion, string nombre)
{
    if (!condicion) throw new Exception("Falló: " + nombre);
    aprobadas++;
    Console.WriteLine("OK: " + nombre);
}

