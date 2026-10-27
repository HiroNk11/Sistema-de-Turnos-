namespace SistemaTurnos.Aplicacion.Modelos;

public sealed record PerfilSesion(int Id,string Nombre,RolUsuario Rol,int? ProfesionalId);
public sealed record AccesoApi(string Usuario,string Password)
{ public override string ToString()=>"AccesoApi { [oculto] }"; }
public sealed record LoginApi(string Token,PerfilSesion Perfil)
{ public override string ToString()=>"LoginApi { [oculto] }"; }
public sealed record ErrorApi(string Mensaje,string Codigo);
public sealed record CatalogoApi(Catalogo Tipo);
public sealed record GuardarCatalogoApi(Catalogo Tipo,RegistroCatalogo Registro);
public sealed record FechaApi(DateTime Fecha);
public sealed record ProfesionalApi(int Profesional);
public sealed record SemanaApi(int Profesional,DateTime Fecha);
public sealed record JornadasApi(int Profesional,IReadOnlyList<Jornada> Jornadas);
public sealed record HorariosApi(int Profesional,int Servicio,DateTime Dia);
public sealed record ReservaApi(int Cliente,int? Paciente,int Profesional,int Servicio,DateTime InicioLocal,decimal Precio);
public sealed record IdApi(long Id);
public sealed record CobroApi(long TurnoId,decimal Importe,MedioCobro Medio,Guid Solicitud);
public sealed record AnularApi(long Id,string Motivo);
public sealed record InformeApi(DateTime Fecha,PeriodoInforme Periodo);
public sealed record UsuarioApi(UsuarioGestion Usuario,string Password)
{ public override string ToString()=>"UsuarioApi { [oculto] }"; }
