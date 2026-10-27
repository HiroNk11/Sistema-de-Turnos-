using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Aplicacion.Servicios;

public static class CalculadorHorarios
{
    public static IReadOnlyList<HorarioLibre> Calcular(DateTime dia, TimeZoneInfo zona, int duracion,
        IEnumerable<Jornada> jornadas, IEnumerable<TurnoListado> turnos, DateTime ahoraUtc)
    {
        if (duracion <= 0) throw new ArgumentOutOfRangeException(nameof(duracion));
        var resultado=new List<HorarioLibre>();
        int semana=((int)dia.DayOfWeek+6)%7;
        var ocupados=turnos.Where(x=>x.CodigoEstado!="CANCELADO" && x.Estado!="Cancelado").ToList();
        foreach (var j in jornadas.Where(x=>x.DiaSemana==semana).OrderBy(x=>x.Desde))
        {
            if (j.PasoMinutos<=0 || j.Hasta<=j.Desde) continue;
            DateTime limite=DateTime.SpecifyKind(dia.Date+j.Hasta,DateTimeKind.Unspecified);
            for (var inicio=DateTime.SpecifyKind(dia.Date+j.Desde,DateTimeKind.Unspecified);
                inicio.AddMinutes(duracion)<=limite; inicio=inicio.AddMinutes(j.PasoMinutos))
            {
                DateTime fin=inicio.AddMinutes(duracion);
                if (zona.IsInvalidTime(inicio)||zona.IsAmbiguousTime(inicio)
                    ||zona.IsInvalidTime(fin)||zona.IsAmbiguousTime(fin)) continue;
                DateTime utc=TimeZoneInfo.ConvertTimeToUtc(inicio,zona), finUtc=TimeZoneInfo.ConvertTimeToUtc(fin,zona);
                if (utc<=ahoraUtc || finUtc-utc!=TimeSpan.FromMinutes(duracion)) continue;
                if (ocupados.Any(t=>t.InicioUtc<finUtc && t.FinUtc>utc)) continue;
                resultado.Add(new(inicio,fin));
            }
        }
        return resultado.DistinctBy(x=>x.InicioLocal).ToList();
    }
}

