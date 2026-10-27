namespace SistemaTurnos.Aplicacion.Abstracciones;

public interface IUnidadDeTrabajo : IDisposable
{
    void Commit();
    void Rollback();
}
