namespace SistemaTurnos.Dominio.Entidades;

public sealed class Turno
{
    public int ClienteId { get; set; }
    public int? PacienteId { get; set; }
    public int ProfesionalId { get; set; }
    public int ServicioId { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public decimal PrecioAcordado { get; set; }
    public string Moneda { get; set; } = string.Empty;
}
