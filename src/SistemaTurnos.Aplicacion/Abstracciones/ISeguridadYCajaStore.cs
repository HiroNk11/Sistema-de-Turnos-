using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Aplicacion.Abstracciones;

public interface ISeguridadStore
{
    Task<IReadOnlyList<UsuarioGestion>> UsuariosAsync();
    Task GuardarUsuarioAsync(UsuarioGestion usuario,string? hash,int actorId,Guid tokenSesion);
}
public interface ICajaStore
{
    Task<CuentaTurno> CuentaAsync(long turnoId);
    Task<IReadOnlyList<CobroListado>> CobrosAsync(long turnoId);
    Task<long> RegistrarCobroAsync(long turnoId,decimal importe,MedioCobro medio,Guid solicitud,int actorId,Guid tokenSesion);
    Task AnularCobroAsync(long cobroId,string motivo,int actorId,Guid tokenSesion);
    Task<IReadOnlyList<ResumenCobro>> InformeAsync(DateTime desdeUtc,DateTime hastaUtc,string zona);
}
