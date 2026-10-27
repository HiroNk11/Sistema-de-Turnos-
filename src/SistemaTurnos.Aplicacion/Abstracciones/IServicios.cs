using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Aplicacion.Abstracciones;

public interface IAccesoServicio
{
    Task<bool> HayUsuariosAsync();
    Task CrearAdministradorAsync(string nombre,string password,string repeticion);
    Task LoginAsync(string nombre,string password);
    Task SalirAsync();
}
public interface IGestionServicio
{
    Task<IReadOnlyList<RegistroCatalogo>> ListarAsync(Catalogo tipo);
    Task GuardarAsync(Catalogo tipo,RegistroCatalogo registro);
    Task<Configuracion> ConfiguracionAsync();
    Task GuardarConfiguracionAsync(Configuracion configuracion);
    Task<IReadOnlyList<TurnoListado>> AgendaAsync(DateTime dia);
    Task<IReadOnlyList<TurnoListado>> AgendaSemanalAsync(int profesional,DateTime fecha);
    Task<IReadOnlyList<Jornada>> JornadasAsync(int profesional);
    Task GuardarJornadasAsync(int profesional,IReadOnlyList<Jornada> jornadas);
    Task<IReadOnlyList<HorarioLibre>> HorariosAsync(int profesional,int servicio,DateTime dia);
    Task<long> CrearTurnoAsync(int cliente,int? paciente,int profesional,int servicio,DateTime inicioLocal,decimal precio);
}
public interface ICajaServicio
{
    Task<DateTime> HoyAsync();
    Task<CuentaTurno> CuentaAsync(long id);
    Task<IReadOnlyList<CobroListado>> CobrosAsync(long id);
    Task<long> RegistrarAsync(long turnoId,decimal importe,MedioCobro medio,Guid solicitud);
    Task AnularAsync(long id,string motivo);
    Task<IReadOnlyList<ResumenCobro>> InformeAsync(DateTime fecha,PeriodoInforme periodo);
}
public interface IUsuariosServicio
{
    bool EsCuentaActual(int id);
    Task<IReadOnlyList<UsuarioGestion>> ListarAsync();
    Task GuardarAsync(UsuarioGestion usuario,string password);
}
public interface ICorreoServicio
{
    Task<ConfiguracionCorreo> ConfiguracionAsync();
    Task<IReadOnlyList<CorreoEstado>> ListarAsync();
    Task ReintentarAsync(long id);
    Task GuardarAsync(ConfiguracionCorreo configuracion);
    Task<int> ProcesarAsync(CancellationToken token=default,int limite=10);
}
