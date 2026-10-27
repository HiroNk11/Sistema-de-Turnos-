using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Aplicacion.Servicios;

public sealed partial class GestionServicio
{
    public async Task<IReadOnlyList<Jornada>> JornadasAsync(int profesional)
    { await sesion.ExigirProfesionalAsync(profesional); return await agenda.JornadasAsync(profesional); }

    public async Task GuardarJornadasAsync(int profesional, IReadOnlyList<Jornada> jornadas)
    {
        await sesion.ExigirAsync(Permiso.GestionarCatalogos);
        if (!(await store.ListarAsync(Catalogo.Profesionales)).Any(x=>x.Id==profesional && x.Activo))
            throw new OperacionException("Seleccioná un profesional activo.");
        foreach (var j in jornadas)
        {
            if (j.DiaSemana is < 0 or > 6 || j.Desde<TimeSpan.Zero || j.Hasta>=TimeSpan.FromDays(1)
                || j.Hasta<=j.Desde || j.Desde.Ticks%TimeSpan.TicksPerMinute!=0
                || j.Hasta.Ticks%TimeSpan.TicksPerMinute!=0 || j.PasoMinutos is < 5 or > 120)
                throw new OperacionException("Revisá las jornadas: día válido, horas del mismo día y paso entre 5 y 120 minutos.");
        }
        foreach (var grupo in jornadas.GroupBy(x=>x.DiaSemana))
        {
            var ordenadas=grupo.OrderBy(x=>x.Desde).ToList();
            for (int i=1;i<ordenadas.Count;i++)
                if (ordenadas[i].Desde<ordenadas[i-1].Hasta)
                    throw new OperacionException("Hay jornadas superpuestas en un mismo día.");
        }
        await agenda.GuardarJornadasAsync(profesional,jornadas);
    }

    public async Task<IReadOnlyList<HorarioLibre>> HorariosAsync(int profesional,int servicio,DateTime dia)
    {
        await sesion.ExigirProfesionalAsync(profesional);
        var p=(await store.ListarAsync(Catalogo.Profesionales)).SingleOrDefault(x=>x.Id==profesional && x.Activo);
        var s=(await store.ListarAsync(Catalogo.Servicios)).SingleOrDefault(x=>x.Id==servicio && x.Activo);
        if (p is null || s is null || !p.ServiciosIds.Contains(servicio)) return [];
        var c=await store.ConfiguracionAsync();
        var zona=TimeZoneInfo.FindSystemTimeZoneById(c.ZonaHoraria);
        var turnos=(await AgendaAsync(dia)).Where(x=>x.ProfesionalId==profesional);
        return CalculadorHorarios.Calcular(dia,zona,s.DuracionMinutos,await agenda.JornadasAsync(profesional),turnos,DateTime.UtcNow);
    }
}

