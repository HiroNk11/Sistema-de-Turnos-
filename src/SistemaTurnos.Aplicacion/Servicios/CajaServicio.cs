using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Aplicacion.Servicios;

public static class PeriodosCaja
{
    public static (DateTime Desde,DateTime Hasta) Limites(DateTime fecha,PeriodoInforme periodo)
    {
        DateTime inicio=periodo switch { PeriodoInforme.Dia=>fecha.Date,PeriodoInforme.Semana=>SemanaAgenda.Lunes(fecha),
            PeriodoInforme.Mes=>new DateTime(fecha.Year,fecha.Month,1),_=>throw new ArgumentOutOfRangeException(nameof(periodo)) };
        inicio=DateTime.SpecifyKind(inicio,DateTimeKind.Unspecified);
        return (inicio,periodo switch { PeriodoInforme.Dia=>inicio.AddDays(1),PeriodoInforme.Semana=>inicio.AddDays(7),_=>inicio.AddMonths(1) });
    }
}
public sealed class CajaServicio(ICajaStore caja,IAppStore store,Sesion sesion) : ICajaServicio
{
    public async Task<DateTime> HoyAsync()
    {
        await sesion.ExigirAsync(Permiso.VerInformes); var c=await store.ConfiguracionAsync();
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(c.ZonaHoraria)).Date;
    }
    public async Task<CuentaTurno> CuentaAsync(long id) { await sesion.ExigirAsync(Permiso.VerCobros); return await caja.CuentaAsync(id); }
    public async Task<IReadOnlyList<CobroListado>> CobrosAsync(long id) { await sesion.ExigirAsync(Permiso.VerCobros); return await caja.CobrosAsync(id); }
    public async Task<long> RegistrarAsync(long turnoId,decimal importe,MedioCobro medio,Guid solicitud)
    {
        await sesion.ExigirAsync(Permiso.RegistrarCobros);
        if (turnoId<=0 || importe<=0 || importe>9999999999999999.99m || decimal.Round(importe,2)!=importe
            || !Enum.IsDefined(medio)||solicitud==Guid.Empty) throw new OperacionException("Revisá turno, importe y medio de pago.");
        return await caja.RegistrarCobroAsync(turnoId,importe,medio,solicitud,sesion.UsuarioId!.Value,sesion.Token);
    }
    public async Task AnularAsync(long id,string motivo)
    {
        await sesion.ExigirAsync(Permiso.AnularCobros);
        if (string.IsNullOrWhiteSpace(motivo)||motivo.Trim().Length>500)
            throw new OperacionException("Ingresá un motivo de anulación de hasta 500 caracteres.");
        await caja.AnularCobroAsync(id,motivo.Trim(),sesion.UsuarioId!.Value,sesion.Token);
    }
    public async Task<IReadOnlyList<ResumenCobro>> InformeAsync(DateTime fecha,PeriodoInforme periodo)
    {
        await sesion.ExigirAsync(Permiso.VerInformes);
        var c=await store.ConfiguracionAsync(); var zona=TimeZoneInfo.FindSystemTimeZoneById(c.ZonaHoraria);
        var limites=PeriodosCaja.Limites(fecha,periodo);
        return await caja.InformeAsync(TimeZoneInfo.ConvertTimeToUtc(limites.Desde,zona),TimeZoneInfo.ConvertTimeToUtc(limites.Hasta,zona),c.ZonaHoraria);
    }
}

