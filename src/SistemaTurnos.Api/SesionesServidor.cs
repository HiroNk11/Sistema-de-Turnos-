using System.Security.Cryptography;
using System.Text;
using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Aplicacion.Servicios;

namespace SistemaTurnos.Api;

public sealed class ServiciosServidor
{
    public Sesion Sesion { get; }=new();
    public AccesoServicio Acceso { get; }
    public GestionServicio Gestion { get; }
    public UsuariosServicio Usuarios { get; }
    public CajaServicio Caja { get; }
    public CorreoServicio Correo { get; }
    public ServiciosServidor(IAppStore store,IAgendaStore agenda,ISeguridadStore usuarios,ICajaStore caja,
        IColaCorreo cola,ITurnoScopeFactory scopes,IPasswordHasher hasher,ITransporteCorreo transporte)
    {
        Acceso=new(store,hasher,Sesion); Gestion=new(store,scopes,Sesion,agenda);
        Usuarios=new(usuarios,hasher,Sesion); Caja=new(caja,store,Sesion); Correo=new(cola,transporte,Sesion);
    }
    public PerfilSesion Perfil()=>new(Sesion.UsuarioId!.Value,Sesion.Nombre,Sesion.Rol!.Value,Sesion.ProfesionalId);
}

public sealed class SesionesServidor : IDisposable
{
    public sealed class Entrada(ServiciosServidor servicios)
    {
        public ServiciosServidor Servicios {get;}=servicios;
        public SemaphoreSlim Exclusivo {get;}=new(1,1);
        public DateTime Creada {get;}=DateTime.UtcNow;
        public DateTime Actividad {get;set;}=DateTime.UtcNow;
    }
    private readonly Dictionary<string,Entrada> sesiones=[];
    private readonly object bloqueo=new();
    private readonly Timer limpieza;
    private readonly Func<ServiciosServidor> crear;
    public SesionesServidor(Func<ServiciosServidor> crear,TimeSpan? frecuencia=null)
    { this.crear=crear; limpieza=new(_=>Limpiar(),null,frecuencia??TimeSpan.FromMinutes(1),frecuencia??TimeSpan.FromMinutes(1)); }
    private static string Clave(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
    public async Task<LoginApi> LoginAsync(AccesoApi datos)
    {
        if(string.IsNullOrWhiteSpace(datos.Usuario)||datos.Usuario.Length>50||string.IsNullOrEmpty(datos.Password)||datos.Password.Length>128)
            throw new OperacionException("Usuario o contraseña incorrectos.");
        var servicios=crear(); await servicios.Acceso.LoginAsync(datos.Usuario,datos.Password);
        string token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        lock(bloqueo)
        {
            LimpiarInterno();
            var anteriores=sesiones.Where(x=>x.Value.Servicios.Sesion.UsuarioId==servicios.Sesion.UsuarioId).OrderBy(x=>x.Value.Creada).ToList();
            foreach(var anterior in anteriores.Take(Math.Max(0,anteriores.Count-2))) QuitarInterno(anterior.Key);
            if(sesiones.Count>=2048) { servicios.Sesion.Cerrar(); throw new OperacionException("El servidor alcanzó su capacidad de sesiones. Intentá más tarde."); }
            sesiones.Add(Clave(token),new(servicios));
        }
        return new(token,servicios.Perfil());
    }
    public Entrada? Obtener(string token)
    {
        if(token.Length!=64||token.Any(c=>!Uri.IsHexDigit(c))) return null;
        lock(bloqueo)
        { LimpiarInterno(); return sesiones.GetValueOrDefault(Clave(token)); }
    }
    public void Quitar(string token) { lock(bloqueo) QuitarInterno(Clave(token)); }
    private void QuitarInterno(string clave)
    { if(sesiones.Remove(clave,out var entrada)) entrada.Servicios.Sesion.Cerrar(); }
    private void LimpiarInterno()
    {
        foreach(var par in sesiones.Where(p=>DateTime.UtcNow-p.Value.Creada>=TimeSpan.FromHours(8)
            ||DateTime.UtcNow-p.Value.Actividad>=TimeSpan.FromMinutes(30)||!p.Value.Servicios.Sesion.UsuarioId.HasValue).ToList()) QuitarInterno(par.Key);
    }
    private void Limpiar() { lock(bloqueo) LimpiarInterno(); }
    public void Dispose() { limpieza?.Dispose(); lock(bloqueo) { foreach(var e in sesiones.Values) e.Servicios.Sesion.Cerrar(); sesiones.Clear(); } }
}
