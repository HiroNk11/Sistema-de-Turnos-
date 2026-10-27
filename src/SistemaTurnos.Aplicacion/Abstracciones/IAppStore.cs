using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Aplicacion.Abstracciones;

public interface IAppStore
{
    Task<bool> HayUsuariosAsync();
    Task<UsuarioAcceso?> BuscarUsuarioAsync(string nombre);
    Task RegistrarAdministradorAsync(string nombre, string hash);
    Task RegistrarIntentoAsync(int usuarioId, bool correcto,Guid tokenEsperado);
    Task<EstadoSesion?> BuscarSesionAsync(int usuarioId);
    Task<IReadOnlyList<RegistroCatalogo>> ListarAsync(Catalogo tipo);
    Task GuardarAsync(Catalogo tipo, RegistroCatalogo registro);
    Task<Configuracion> ConfiguracionAsync();
    Task GuardarConfiguracionAsync(Configuracion configuracion);
    Task<IReadOnlyList<TurnoListado>> AgendaAsync(DateTime desdeUtc, DateTime hastaUtc);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITurnoScope : IUnidadDeTrabajo
{
    ITurnoRepository Turnos { get; }
}

public interface ITurnoScopeFactory
{
    ITurnoScope Crear();
}
