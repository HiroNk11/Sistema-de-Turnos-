using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Aplicacion.Servicios;

public sealed class AccesoServicio(IAppStore store, IPasswordHasher hasher, Sesion sesion) : IAccesoServicio
{
    public Task SalirAsync() { sesion.Cerrar(); return Task.CompletedTask; }
    public Task<bool> HayUsuariosAsync() => store.HayUsuariosAsync();

    public async Task CrearAdministradorAsync(string nombre, string password, string repeticion)
    {
        if(string.IsNullOrWhiteSpace(nombre)||string.IsNullOrWhiteSpace(password))
            throw new OperacionException("Completá usuario y contraseña.");
        nombre = nombre.Trim();
        if (nombre.Length is < 3 or > 50)
            throw new OperacionException("El usuario debe tener entre 3 y 50 caracteres.");
        PoliticaPassword.ValidarNueva(password,nombre);
        if (password != repeticion) throw new OperacionException("Las contraseñas no coinciden.");
        if (await store.HayUsuariosAsync())
            throw new OperacionException("El administrador inicial ya fue creado.");
        string hash = await Task.Run(() => hasher.Hash(password));
        await store.RegistrarAdministradorAsync(nombre, hash);
    }

    public async Task LoginAsync(string nombre, string password)
    {
        sesion.Cerrar();
        if (string.IsNullOrWhiteSpace(nombre)||string.IsNullOrEmpty(password)||nombre.Trim().Length is < 1 or > 50 || password.Length is < 1 or > 128)
            throw new OperacionException("Usuario o contraseña incorrectos.");
        var usuario = await store.BuscarUsuarioAsync(nombre.Trim());
        // Mismo costo de derivación para usuarios inexistentes.
        bool correcta = await Task.Run(() => usuario is null
            ? hasher.Verify(password, HashInexistente)
            : hasher.Verify(password, usuario.Hash));
        if (usuario is null) throw new OperacionException("Usuario o contraseña incorrectos.");
        if (!usuario.Activo || usuario.BloqueadoHastaUtc > DateTime.UtcNow)
            throw new OperacionException("Usuario o contraseña incorrectos.");
        await store.RegistrarIntentoAsync(usuario.Id, correcta,usuario.TokenSesion);
        if (!correcta) throw new OperacionException("Usuario o contraseña incorrectos.");
        if (usuario.Rol==RolUsuario.Profesional && !usuario.ProfesionalId.HasValue)
            throw new OperacionException("El usuario profesional no tiene un profesional vinculado.");
        if(usuario.Rol==RolUsuario.Profesional && !(await store.ListarAsync(Catalogo.Profesionales)).Any(x=>x.Id==usuario.ProfesionalId&&x.Activo))
            throw new OperacionException("El profesional vinculado está inactivo. Contactá al administrador.");
        sesion.Iniciar(usuario,store.BuscarSesionAsync);
        await sesion.ExigirAccesoAsync();
    }

    private const string HashInexistente = "600000.AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
}
