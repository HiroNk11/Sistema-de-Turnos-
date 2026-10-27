namespace SistemaTurnos.Aplicacion.Modelos;

public enum RolUsuario { Administrador, Recepcion, Caja, Profesional }
public enum Permiso { VerAgenda, GestionarPersonas, Reservar, GestionarCatalogos, Configurar,
    AdministrarUsuarios, VerCobros, RegistrarCobros, AnularCobros, VerInformes, ProcesarCorreos }
public enum MedioCobro { Efectivo, Transferencia, Tarjeta, Otro }
public enum PeriodoInforme { Dia, Semana, Mes }
public sealed record UsuarioGestion(int Id,string Nombre,RolUsuario Rol,bool Activo,int? ProfesionalId,byte[] Version);
public sealed record CuentaTurno(long TurnoId,string Cliente,decimal Precio,decimal Cobrado,string Moneda,string Estado)
{ public decimal Saldo=>Precio-Cobrado; }
public sealed record CobroListado(long Id,long TurnoId,DateTime FechaUtc,decimal Importe,string Moneda,string Medio,
    string Usuario,bool Anulado,string Motivo);
public sealed record ResumenCobro(DateTime Dia,string Moneda,decimal Registrado,decimal Anulado,decimal Neto,int Cantidad);
