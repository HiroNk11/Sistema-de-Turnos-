using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Aplicacion.Servicios;

public sealed class UsuariosServicio(ISeguridadStore store,IPasswordHasher hasher,Sesion sesion) : IUsuariosServicio
{
    public bool EsCuentaActual(int id)=>sesion.UsuarioId==id;
    public async Task<IReadOnlyList<UsuarioGestion>> ListarAsync()
    { await sesion.ExigirAsync(Permiso.AdministrarUsuarios); return await store.UsuariosAsync(); }
    public async Task GuardarAsync(UsuarioGestion usuario,string password)
    {
        await sesion.ExigirAsync(Permiso.AdministrarUsuarios);
        if (usuario.Nombre.Trim().Length is <3 or >50 || !Enum.IsDefined(usuario.Rol))
            throw new OperacionException("Revisá el nombre y rol del usuario.");
        if (usuario.Rol==RolUsuario.Profesional && usuario.ProfesionalId is null or <=0)
            throw new OperacionException("Vinculá el usuario a un profesional.");
        if (usuario.Id==sesion.UsuarioId && (!usuario.Activo||usuario.Rol!=RolUsuario.Administrador))
            throw new OperacionException("No podés desactivar tu cuenta ni cambiar tu propio rol durante esta sesión.");
        if(usuario.Id==0||password.Length>0) PoliticaPassword.ValidarNueva(password,usuario.Nombre.Trim());
        int actor=sesion.UsuarioId!.Value; Guid token=sesion.Token;
        string? hash=password.Length==0?null:await Task.Run(()=>hasher.Hash(password));
        await sesion.ExigirAsync(Permiso.AdministrarUsuarios);
        await store.GuardarUsuarioAsync(usuario with {Nombre=usuario.Nombre.Trim(),ProfesionalId=usuario.Rol==RolUsuario.Profesional?usuario.ProfesionalId:null},hash,actor,token);
    }
}

