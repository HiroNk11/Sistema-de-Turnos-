namespace SistemaTurnos.Aplicacion.Servicios;

public static class SemanaAgenda
{
    public static DateTime Lunes(DateTime fecha)
        => DateTime.SpecifyKind(fecha.Date.AddDays(-((int)fecha.DayOfWeek+6)%7),DateTimeKind.Unspecified);
}

public sealed partial class GestionServicio
{
    public async Task<IReadOnlyList<Modelos.TurnoListado>> AgendaSemanalAsync(int profesional,DateTime fecha)
    {
        await sesion.ExigirProfesionalAsync(profesional);
        if (profesional<=0) return [];
        var c=await store.ConfiguracionAsync();
        var zona=TimeZoneInfo.FindSystemTimeZoneById(c.ZonaHoraria);
        DateTime lunes=SemanaAgenda.Lunes(fecha);
        var turnos=await store.AgendaAsync(TimeZoneInfo.ConvertTimeToUtc(lunes,zona),
            TimeZoneInfo.ConvertTimeToUtc(lunes.AddDays(7),zona));
        await sesion.ExigirProfesionalAsync(profesional);
        return turnos.Where(t=>t.ProfesionalId==profesional).OrderBy(t=>t.InicioUtc).ToList();
    }
}

