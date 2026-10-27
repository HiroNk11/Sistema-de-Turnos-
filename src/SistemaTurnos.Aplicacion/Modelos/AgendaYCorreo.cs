namespace SistemaTurnos.Aplicacion.Modelos;

// DiaSemana: lunes=0, domingo=6. Jornada dentro de un mismo día.
public sealed record Jornada(int DiaSemana, TimeSpan Desde, TimeSpan Hasta, int PasoMinutos);
public sealed record HorarioLibre(DateTime InicioLocal, DateTime FinLocal)
{
    public string Texto => $"{InicioLocal:HH:mm} a {FinLocal:HH:mm}";
}
public sealed record CorreoPendiente(long Id, string Destinatario, string Asunto, string Cuerpo,Guid TokenReclamo);
public sealed record CorreoEstado(long Id, long TurnoId, string Destinatario, string Estado, int Intentos, string Detalle);
public sealed record ConfiguracionCorreo(bool Habilitado, string Servidor, int Puerto, string Remitente,
    string NombreRemitente, string Usuario, string Password, bool TienePassword = false)
{ public override string ToString()=>"ConfiguracionCorreo { Credenciales = [ocultas] }"; }
