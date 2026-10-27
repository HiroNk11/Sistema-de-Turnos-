using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Dominio.Entidades;
sealed class StoreFalso : IAppStore,IAgendaStore
{
    public UsuarioAcceso? Usuario;
    public int Fallos;
    public Action? AlIntentar=null;
    public TaskCompletionSource<EstadoSesion?>? ConsultaDiferida=null;
    public IReadOnlyList<TurnoListado> TurnosPrueba=[];
    public DateTime UltimoDesde,UltimoHasta;
    public Task<bool> HayUsuariosAsync()=>Task.FromResult(Usuario is not null);
    public Task<UsuarioAcceso?> BuscarUsuarioAsync(string nombre)=>Task.FromResult(Usuario?.Nombre==nombre?Usuario:null);
    public Task RegistrarAdministradorAsync(string nombre,string hash)
    { Usuario=new(1,nombre,hash,true,null,RolUsuario.Administrador,null,Guid.NewGuid()); return Task.CompletedTask; }
    public Task RegistrarIntentoAsync(int usuarioId,bool correcto,Guid tokenEsperado)
    { AlIntentar?.Invoke(); if(Usuario?.TokenSesion!=tokenEsperado) throw new OperacionException("La cuenta cambió."); if (!correcto) Fallos++; return Task.CompletedTask; }
    public Task<EstadoSesion?> BuscarSesionAsync(int id)=>ConsultaDiferida?.Task??Task.FromResult<EstadoSesion?>(Usuario is null?null:new(Usuario.Activo,Usuario.Rol,Usuario.ProfesionalId,Usuario.TokenSesion));
    public Task<IReadOnlyList<RegistroCatalogo>> ListarAsync(Catalogo tipo)
    {
        IReadOnlyList<RegistroCatalogo> datos=tipo switch
        {
            Catalogo.Clientes=>[new() { Id=1,Nombre="Cliente" }],
            Catalogo.Profesionales=>[new() { Id=2,Nombre="Profesional",ServiciosIds=[3] }],
            Catalogo.Servicios=>[new() { Id=3,Nombre="Consulta",DuracionMinutos=30,Precio=100 }],
            _=>[new() { Id=4,Nombre="Paciente",ResponsableId=1 }]
        };
        return Task.FromResult(datos);
    }
    public Task GuardarAsync(Catalogo tipo,RegistroCatalogo r)=>Task.CompletedTask;
    public Task<Configuracion> ConfiguracionAsync()=>Task.FromResult(new Configuracion("Negocio","ODONTOLOGIA","Cliente","Paciente",true,"Argentina Standard Time","ARS"));
    public Task GuardarConfiguracionAsync(Configuracion c)=>Task.CompletedTask;
    public Task<IReadOnlyList<TurnoListado>> AgendaAsync(DateTime desde,DateTime hasta)
    { UltimoDesde=desde; UltimoHasta=hasta; return Task.FromResult<IReadOnlyList<TurnoListado>>(TurnosPrueba.Where(x=>x.InicioUtc<hasta&&x.FinUtc>desde).ToList()); }
    public Task<IReadOnlyList<Jornada>> JornadasAsync(int id)=>Task.FromResult<IReadOnlyList<Jornada>>(
        Enumerable.Range(0,7).Select(d=>new Jornada(d,TimeSpan.Zero,new TimeSpan(23,59,0),15)).ToList());
    public Task GuardarJornadasAsync(int id,IReadOnlyList<Jornada> jornadas)=>Task.CompletedTask;
}
sealed class ScopeFalso : ITurnoScopeFactory,ITurnoScope,ITurnoRepository
{
    public bool Confirmada,Dispuesta;
    public Turno? Turno;
    public ITurnoRepository Turnos=>this;
    public ITurnoScope Crear()=>this;
    public Task<long> CrearTurnoAsync(Turno turno) { Turno=turno; return Task.FromResult(42L); }
    public void Commit()=>Confirmada=true;
    public void Rollback()=>Confirmada=false;
    public void Dispose()=>Dispuesta=true;
}
sealed class ColaFalsa : IColaCorreo
{
    private Guid token;
    public string Estado="Pendiente";
    public int Reclamados;
    public Task<CorreoPendiente?> ReclamarAsync()
    {
        if (Estado!="Pendiente") return Task.FromResult<CorreoPendiente?>(null);
        Estado="Procesando"; Reclamados++;
        token=Guid.NewGuid(); return Task.FromResult<CorreoPendiente?>(new(1,"prueba@example.invalid","Confirmación","Reserva",token));
    }
    public Task<bool> FinalizarAsync(long id,Guid propietario,bool enviado)
    { if(propietario!=token) return Task.FromResult(false); Estado=enviado?"Enviado":"Error"; return Task.FromResult(true); }
    public Task<bool> ReclamoVigenteAsync(long id,Guid propietario)=>Task.FromResult(propietario==token&&Estado=="Procesando");
    public Task<IReadOnlyList<CorreoEstado>> ListarCorreosAsync()=>Task.FromResult<IReadOnlyList<CorreoEstado>>([]);
    public Task ReintentarAsync(long id) { Estado="Pendiente"; return Task.CompletedTask; }
}
sealed class TransporteFalso : ITransporteCorreo
{
    public bool Habilitado=false,Fallar=false;
    public Task<ConfiguracionCorreo> ConfiguracionAsync()=>Task.FromResult(new ConfiguracionCorreo(Habilitado,"smtp.gmail.com",587,"prueba@example.invalid","Negocio","usuario","",true));
    public Task GuardarConfiguracionAsync(ConfiguracionCorreo c)=>Task.CompletedTask;
    public Task EnviarAsync(CorreoPendiente c,CancellationToken token)
        =>Fallar?Task.FromException(new System.Net.Mail.SmtpException("Fallo simulado")):Task.CompletedTask;
}
sealed class CajaFalsa : ICajaStore
{
    public bool Anulado; public DateTime Desde,Hasta; public string Zona="";
    public Task<CuentaTurno> CuentaAsync(long id)=>Task.FromResult(new CuentaTurno(id,"Cliente",100,50,"ARS","CONFIRMADO"));
    public Task<IReadOnlyList<CobroListado>> CobrosAsync(long id)=>Task.FromResult<IReadOnlyList<CobroListado>>([]);
    public Task<long> RegistrarCobroAsync(long id,decimal importe,MedioCobro medio,Guid solicitud,int actor,Guid token)=>Task.FromResult(10L);
    public Task AnularCobroAsync(long id,string motivo,int actor,Guid token) { Anulado=true; return Task.CompletedTask; }
    public Task<IReadOnlyList<ResumenCobro>> InformeAsync(DateTime desde,DateTime hasta,string zona)
    { Desde=desde; Hasta=hasta; Zona=zona; return Task.FromResult<IReadOnlyList<ResumenCobro>>([]); }
}
sealed class UsuariosFalsos : ISeguridadStore
{
    public Task<IReadOnlyList<UsuarioGestion>> UsuariosAsync()=>Task.FromResult<IReadOnlyList<UsuarioGestion>>([]);
    public Task GuardarUsuarioAsync(UsuarioGestion u,string? hash,int actor,Guid token)=>Task.CompletedTask;
}
sealed class RelojPrueba : TimeProvider
{
    private DateTimeOffset actual=new(2026,10,27,15,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow()=>actual;
    public void Avanzar(TimeSpan tiempo)=>actual=actual.Add(tiempo);
}

