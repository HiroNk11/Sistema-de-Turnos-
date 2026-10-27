using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTurnos.Aplicacion.Abstracciones;

namespace SistemaTurnos.Infraestructura.SqlServer.Transacciones;

// Una instancia por operación; no compartir entre tareas concurrentes.
public sealed class SqlUnidadDeTrabajo : IUnidadDeTrabajo
{
    private readonly SqlConnection conexion;
    private readonly SqlTransaction transaccion;
    private bool finalizada;
    private bool disposed;

    public SqlUnidadDeTrabajo(string cadenaConexion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenaConexion);
        conexion = new SqlConnection(Seguridad.ConexionSegura.Validar(cadenaConexion));
        try
        {
            conexion.Open();
            transaccion = conexion.BeginTransaction(IsolationLevel.ReadCommitted);
        }
        catch { conexion.Dispose(); throw; }
    }

    internal SqlConnection Conexion { get { ExigirActiva(); return conexion; } }
    internal SqlTransaction Transaccion { get { ExigirActiva(); return transaccion; } }

    public void Commit()
    {
        ExigirActiva();
        transaccion.Commit();
        finalizada = true;
    }

    public void Rollback()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (finalizada) return;
        if (transaccion.Connection != null) transaccion.Rollback();
        finalizada = true;
    }

    private void ExigirActiva()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (finalizada || transaccion.Connection == null)
            throw new InvalidOperationException("La transacción ya no está activa.");
    }

    public void Dispose()
    {
        if (disposed) return;
        try { transaccion.Dispose(); }
        finally { conexion.Dispose(); disposed = true; }
    }
}
