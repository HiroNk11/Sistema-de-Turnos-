using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Aplicacion.Abstracciones;

public interface IAgendaStore
{
    Task<IReadOnlyList<Jornada>> JornadasAsync(int profesionalId);
    Task GuardarJornadasAsync(int profesionalId, IReadOnlyList<Jornada> jornadas);
}
public interface IColaCorreo
{
    Task<CorreoPendiente?> ReclamarAsync();
    Task<bool> FinalizarAsync(long id,Guid tokenReclamo,bool enviado);
    Task<bool> ReclamoVigenteAsync(long id,Guid tokenReclamo);
    Task<IReadOnlyList<CorreoEstado>> ListarCorreosAsync();
    Task ReintentarAsync(long id);
}
public interface ITransporteCorreo
{
    Task<ConfiguracionCorreo> ConfiguracionAsync();
    Task GuardarConfiguracionAsync(ConfiguracionCorreo configuracion);
    Task EnviarAsync(CorreoPendiente correo, CancellationToken cancellationToken);
}
