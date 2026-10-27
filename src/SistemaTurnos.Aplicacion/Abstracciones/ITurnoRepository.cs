using SistemaTurnos.Dominio.Entidades;

namespace SistemaTurnos.Aplicacion.Abstracciones;

public interface ITurnoRepository
{
    Task<long> CrearTurnoAsync(Turno turno);
}
