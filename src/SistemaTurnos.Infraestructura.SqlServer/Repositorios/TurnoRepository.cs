using System.Data;
using Microsoft.Data.SqlClient;
using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Dominio.Entidades;
using SistemaTurnos.Infraestructura.SqlServer.Transacciones;

namespace SistemaTurnos.Infraestructura.SqlServer.Repositorios;

public sealed class TurnoRepository : ITurnoRepository
{
    private readonly SqlUnidadDeTrabajo unidad;
    public TurnoRepository(SqlUnidadDeTrabajo unidadDeTrabajo)
        => unidad = unidadDeTrabajo ?? throw new ArgumentNullException(nameof(unidadDeTrabajo));

    public async Task<long> CrearTurnoAsync(Turno turno)
    {
        ArgumentNullException.ThrowIfNull(turno);
        if (turno.ClienteId <= 0 || turno.ProfesionalId <= 0 || turno.ServicioId <= 0
            || turno.PacienteId is <= 0)
            throw new ArgumentException("Los identificadores deben ser positivos.");
        if (turno.InicioUtc.Kind != DateTimeKind.Utc || turno.FinUtc.Kind != DateTimeKind.Utc
            || turno.FinUtc <= turno.InicioUtc
            || turno.InicioUtc.Ticks % TimeSpan.TicksPerSecond != 0
            || turno.FinUtc.Ticks % TimeSpan.TicksPerSecond != 0)
            throw new ArgumentException("El intervalo debe ser válido, UTC y con precisión de segundos.");
        if (turno.PrecioAcordado < 0 || turno.PrecioAcordado > 9999999999999999.99m
            || decimal.Round(turno.PrecioAcordado, 2) != turno.PrecioAcordado)
            throw new ArgumentException("Precio inválido para DECIMAL(18,2).");
        string moneda = turno.Moneda?.Trim().ToUpperInvariant() ?? "";
        if (moneda.Length != 3 || moneda.Any(c => c < 'A' || c > 'Z'))
            throw new ArgumentException("La moneda debe contener tres letras.");

        using var comando = new SqlCommand("dbo.usp_Turno_Crear", unidad.Conexion, unidad.Transaccion)
        { CommandType = CommandType.StoredProcedure, CommandTimeout = 30 };
        comando.Parameters.Add("@ClienteId", SqlDbType.Int).Value = turno.ClienteId;
        comando.Parameters.Add("@PacienteId", SqlDbType.Int).Value = (object?)turno.PacienteId ?? DBNull.Value;
        comando.Parameters.Add("@ProfesionalId", SqlDbType.Int).Value = turno.ProfesionalId;
        comando.Parameters.Add("@ServicioId", SqlDbType.Int).Value = turno.ServicioId;
        var inicio = comando.Parameters.Add("@InicioUtc", SqlDbType.DateTime2);
        inicio.Scale = 0; inicio.Value = turno.InicioUtc;
        var fin = comando.Parameters.Add("@FinUtc", SqlDbType.DateTime2);
        fin.Scale = 0; fin.Value = turno.FinUtc;
        var precio = comando.Parameters.Add("@PrecioAcordado", SqlDbType.Decimal);
        precio.Precision = 18; precio.Scale = 2; precio.Value = turno.PrecioAcordado;
        comando.Parameters.Add("@Moneda", SqlDbType.Char, 3).Value = moneda;
        object? resultado = await comando.ExecuteScalarAsync().ConfigureAwait(false);
        return resultado is long id && id > 0 ? id
            : throw new InvalidOperationException("El procedimiento no devolvió un TurnoId válido.");
    }
}
