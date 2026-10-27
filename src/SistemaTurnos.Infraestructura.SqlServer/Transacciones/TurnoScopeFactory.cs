using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Infraestructura.SqlServer.Repositorios;
using Microsoft.Data.SqlClient;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Dominio.Entidades;

namespace SistemaTurnos.Infraestructura.SqlServer.Transacciones;

public sealed class TurnoScopeFactory(string cadena) : ITurnoScopeFactory
{
    public ITurnoScope Crear() => new Scope(cadena);
    private sealed class Scope : ITurnoScope
    {
        private readonly SqlUnidadDeTrabajo unidad;
        public ITurnoRepository Turnos { get; }
        public Scope(string cadena)
        { unidad = new(cadena); Turnos = new RepositorioAplicacion(new TurnoRepository(unidad)); }
        public void Commit() => unidad.Commit();
        public void Rollback() => unidad.Rollback();
        public void Dispose() => unidad.Dispose();
    }
    private sealed class RepositorioAplicacion(ITurnoRepository repositorio) : ITurnoRepository
    {
        public async Task<long> CrearTurnoAsync(Turno turno)
        {
            try { return await repositorio.CrearTurnoAsync(turno); }
            catch (SqlException ex) when (ex.Number is >= 51001 and <= 51013)
            { throw new OperacionException(ex.Message); }
            catch (SqlException ex) when (ex.Number == 1205)
            { throw new OperacionException("La agenda está siendo modificada. Actualizá e intentá nuevamente."); }
        }
    }
}
