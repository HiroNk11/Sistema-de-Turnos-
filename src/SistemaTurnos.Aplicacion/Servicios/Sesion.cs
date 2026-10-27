using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Aplicacion.Servicios;

public sealed class Sesion(TimeProvider? reloj=null)
{
    private readonly TimeProvider tiempo=reloj??TimeProvider.System;
    private Func<int,Task<EstadoSesion?>>? consultar;
    private DateTimeOffset inicio,ultimaActividad;
    private long generacion;
    public int? UsuarioId { get; private set; }
    public string Nombre { get; private set; }="";
    public RolUsuario? Rol { get; private set; }
    public int? ProfesionalId { get; private set; }
    public Guid Token { get; private set; }
    public bool ModoRemoto {get;private set;}
    internal void Iniciar(UsuarioAcceso usuario,Func<int,Task<EstadoSesion?>> consultarEstado)
    {
        Cerrar();
        if(!Enum.IsDefined(usuario.Rol)||usuario.TokenSesion==Guid.Empty) throw new OperacionException("La cuenta no tiene una configuración de seguridad válida.");
        UsuarioId=usuario.Id; Nombre=usuario.Nombre; Rol=usuario.Rol; ProfesionalId=usuario.ProfesionalId;
        Token=usuario.TokenSesion; consultar=consultarEstado; inicio=ultimaActividad=tiempo.GetUtcNow();
    }
    public void Cerrar()
    { generacion++; UsuarioId=null; Nombre=""; Rol=null; ProfesionalId=null; Token=Guid.Empty; consultar=null; ModoRemoto=false; }
    // Identidad de presentación. En modo remoto la autoridad es siempre la API.
    public async Task IniciarRemotaAsync(PerfilSesion perfil,Func<Task<bool>> verificar)
    {
        Guid local=Guid.NewGuid();
        Iniciar(new(perfil.Id,perfil.Nombre,"",true,null,perfil.Rol,perfil.ProfesionalId,local),async _=>
            await verificar()?new EstadoSesion(true,perfil.Rol,perfil.ProfesionalId,local):null);
        ModoRemoto=true;
        await ExigirAccesoAsync();
    }
    public bool Puede(Permiso permiso)=>UsuarioId.HasValue&&Rol.HasValue&&Enum.IsDefined(permiso)&&(Rol==RolUsuario.Administrador||permiso switch
    {
        Permiso.VerAgenda=>true,
        Permiso.GestionarPersonas or Permiso.Reservar or Permiso.ProcesarCorreos=>Rol==RolUsuario.Recepcion,
        Permiso.VerCobros or Permiso.RegistrarCobros=>Rol is RolUsuario.Caja or RolUsuario.Recepcion,
        Permiso.AnularCobros or Permiso.VerInformes=>Rol==RolUsuario.Caja,
        _=>false
    });
    public async Task ExigirAccesoAsync(bool actividad=true)
    {
        if(UsuarioId is not int id||consultar is null) throw new OperacionException("Iniciá sesión para continuar.");
        long version=generacion; var ahora=tiempo.GetUtcNow();
        if(ahora-inicio>=TimeSpan.FromHours(8)||ahora-ultimaActividad>=TimeSpan.FromMinutes(30))
        { Cerrar(); throw new OperacionException("La sesión venció. Volvé a ingresar."); }
        var estado=await consultar(id);
        if(version!=generacion) throw new OperacionException("La sesión cambió durante la operación. Volvé a ingresar.");
        if(estado is null||!estado.Activo||estado.TokenSesion!=Token||estado.Rol!=Rol||estado.ProfesionalId!=ProfesionalId)
        { Cerrar(); throw new OperacionException("La cuenta o sus permisos cambiaron. Volvé a ingresar."); }
        if(actividad) ultimaActividad=tiempo.GetUtcNow();
    }
    public async Task ExigirAsync(Permiso permiso,bool actividad=true)
    { await ExigirAccesoAsync(actividad); if(!Puede(permiso)) throw new OperacionException("Tu rol no permite realizar esta operación."); }
    public async Task ExigirProfesionalAsync(int id)
    {
        await ExigirAsync(Permiso.VerAgenda);
        if(Rol==RolUsuario.Profesional&&ProfesionalId!=id) throw new OperacionException("Solo podés consultar tu propia agenda.");
    }
}
