namespace SistemaTurnos.Aplicacion.Modelos;

public enum Catalogo { Clientes, Pacientes, Profesionales, Servicios }

public sealed record UsuarioAcceso(int Id, string Nombre, string Hash, bool Activo, DateTime? BloqueadoHastaUtc,
    RolUsuario Rol, int? ProfesionalId,Guid TokenSesion)
{ public override string ToString()=>$"UsuarioAcceso {{ Id = {Id}, Credenciales = [ocultas] }}"; }
public sealed record EstadoSesion(bool Activo,RolUsuario Rol,int? ProfesionalId,Guid TokenSesion);

public sealed record RegistroCatalogo
{
    public int Id { get; init; }
    public string Nombre { get; init; } = "";
    public string Apellido { get; init; } = "";
    public string Telefono { get; init; } = "";
    public string Email { get; init; } = "";
    public int DuracionMinutos { get; init; } = 30;
    public decimal Precio { get; init; }
    public int? ResponsableId { get; init; }
    public string TipoPaciente { get; init; } = "P";
    public string Especie { get; init; } = "";
    public bool Activo { get; init; } = true;
    public byte[] Version { get; init; } = [];
    public IReadOnlyList<int> ServiciosIds { get; init; } = [];
    public string Texto => $"{Nombre} {Apellido}".Trim();
}

public sealed record Configuracion(string Nombre, string Rubro, string EtiquetaCliente,
    string EtiquetaPaciente, bool UsaPacientes, string ZonaHoraria, string Moneda);

public sealed record TurnoListado(long Id, DateTime InicioUtc, DateTime FinUtc,
    string Cliente, string Paciente, string Profesional, string Servicio, string Estado,
    decimal Precio, string Moneda, int ProfesionalId = 0, string CodigoEstado = "");

public sealed class OperacionException(string mensaje) : Exception(mensaje);
