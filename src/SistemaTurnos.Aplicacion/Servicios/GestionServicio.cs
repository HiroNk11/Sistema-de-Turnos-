using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Dominio.Entidades;

namespace SistemaTurnos.Aplicacion.Servicios;

public sealed partial class GestionServicio(IAppStore store, ITurnoScopeFactory scopes, Sesion sesion, IAgendaStore agenda) : IGestionServicio
{
    public async Task<IReadOnlyList<RegistroCatalogo>> ListarAsync(Catalogo tipo)
    {
        await sesion.ExigirAccesoAsync();
        var rol=sesion.Rol; var profesional=sesion.ProfesionalId;
        if (tipo is Catalogo.Clientes or Catalogo.Pacientes) await sesion.ExigirAsync(Permiso.GestionarPersonas);
        var lista=await store.ListarAsync(tipo);
        await sesion.ExigirAccesoAsync();
        if (rol==RolUsuario.Profesional && tipo==Catalogo.Profesionales)
            return lista.Where(x=>x.Id==profesional).ToList();
        if (rol==RolUsuario.Profesional && tipo==Catalogo.Servicios)
        {
            var p=(await store.ListarAsync(Catalogo.Profesionales)).SingleOrDefault(x=>x.Id==profesional);
            return lista.Where(x=>p?.ServiciosIds.Contains(x.Id)==true).ToList();
        }
        return lista;
    }
    public async Task<Configuracion> ConfiguracionAsync()
    { await sesion.ExigirAccesoAsync(); return await store.ConfiguracionAsync(); }

    public async Task GuardarAsync(Catalogo tipo, RegistroCatalogo registro)
    {
        await sesion.ExigirAsync(tipo is Catalogo.Clientes or Catalogo.Pacientes?Permiso.GestionarPersonas:Permiso.GestionarCatalogos);
        if (string.IsNullOrWhiteSpace(registro.Nombre) || registro.Nombre.Trim().Length > 100)
            throw new OperacionException("Ingresá un nombre de hasta 100 caracteres.");
        if (tipo is Catalogo.Clientes or Catalogo.Profesionales && string.IsNullOrWhiteSpace(registro.Apellido))
            throw new OperacionException("Ingresá el apellido.");
        if (registro.Apellido.Length > 100 || registro.Telefono.Length > 40 || registro.Email.Length > 254)
            throw new OperacionException("Apellido, teléfono o correo demasiado extensos.");
        if (tipo == Catalogo.Servicios && (registro.DuracionMinutos is < 1 or > 1440 || registro.Precio < 0
            || registro.Precio > 9999999999999999.99m || decimal.Round(registro.Precio,2) != registro.Precio))
            throw new OperacionException("Revisá duración y precio del servicio.");
        if (tipo == Catalogo.Pacientes && (registro.ResponsableId is null or <= 0
            || registro.TipoPaciente is not ("P" or "A")
            || registro.TipoPaciente == "A" && string.IsNullOrWhiteSpace(registro.Especie)))
            throw new OperacionException("Seleccioná responsable, tipo y especie cuando sea un animal.");
        if (registro.Especie.Length > 60) throw new OperacionException("La especie admite hasta 60 caracteres.");
        if (!string.IsNullOrWhiteSpace(registro.Email)
            && (!System.Net.Mail.MailAddress.TryCreate(registro.Email.Trim(),out var correo) || correo.Address!=registro.Email.Trim()))
            throw new OperacionException("El correo electrónico no es válido.");
        await store.GuardarAsync(tipo, registro with { Nombre = registro.Nombre.Trim(), Apellido = registro.Apellido.Trim() });
    }

    public async Task GuardarConfiguracionAsync(Configuracion c)
    {
        await sesion.ExigirAsync(Permiso.Configurar);
        if (string.IsNullOrWhiteSpace(c.Nombre) || c.Nombre.Length > 150
            || string.IsNullOrWhiteSpace(c.EtiquetaCliente) || c.EtiquetaCliente.Length > 40
            || string.IsNullOrWhiteSpace(c.EtiquetaPaciente) || c.EtiquetaPaciente.Length > 40)
            throw new OperacionException("Revisá el nombre y las etiquetas del negocio.");
        if (c.Moneda.Length != 3 || c.Moneda.Any(x => x < 'A' || x > 'Z'))
            throw new OperacionException("Ingresá una moneda de tres letras, por ejemplo ARS.");
        try { TimeZoneInfo.FindSystemTimeZoneById(c.ZonaHoraria); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new OperacionException("La zona horaria no es válida."); }
        await store.GuardarConfiguracionAsync(c);
    }

    public async Task<IReadOnlyList<TurnoListado>> AgendaAsync(DateTime dia)
    {
        await sesion.ExigirAsync(Permiso.VerAgenda);
        var rol=sesion.Rol; var profesional=sesion.ProfesionalId;
        var c = await store.ConfiguracionAsync();
        var zona = TimeZoneInfo.FindSystemTimeZoneById(c.ZonaHoraria);
        DateTime desde = DateTime.SpecifyKind(dia.Date, DateTimeKind.Unspecified);
        var datos=await store.AgendaAsync(TimeZoneInfo.ConvertTimeToUtc(desde, zona),
            TimeZoneInfo.ConvertTimeToUtc(desde.AddDays(1), zona));
        await sesion.ExigirAsync(Permiso.VerAgenda);
        return rol==RolUsuario.Profesional?datos.Where(x=>x.ProfesionalId==profesional).ToList():datos;
    }

    public async Task<long> CrearTurnoAsync(int cliente, int? paciente, int profesional,
        int servicio, DateTime inicioLocal, decimal precio)
    {
        await sesion.ExigirAsync(Permiso.Reservar);
        var c = await store.ConfiguracionAsync();
        var clientes = await store.ListarAsync(Catalogo.Clientes);
        var profesionales = await store.ListarAsync(Catalogo.Profesionales);
        var servicios = await store.ListarAsync(Catalogo.Servicios);
        var s = servicios.SingleOrDefault(x => x.Id == servicio && x.Activo);
        if (!clientes.Any(x => x.Id == cliente && x.Activo)
            || !profesionales.Any(x => x.Id == profesional && x.Activo) || s is null)
            throw new OperacionException("Seleccioná cliente, profesional y servicio activos.");
        if (paciente.HasValue)
        {
            var pacientes = await store.ListarAsync(Catalogo.Pacientes);
            if (!pacientes.Any(x => x.Id == paciente && x.ResponsableId == cliente && x.Activo))
                throw new OperacionException("El paciente no pertenece al cliente seleccionado o está inactivo.");
        }
        if (c.UsaPacientes && !paciente.HasValue)
            throw new OperacionException("Seleccioná el paciente.");
        var zona = TimeZoneInfo.FindSystemTimeZoneById(c.ZonaHoraria);
        inicioLocal = DateTime.SpecifyKind(inicioLocal, DateTimeKind.Unspecified);
        if (zona.IsInvalidTime(inicioLocal) || zona.IsAmbiguousTime(inicioLocal))
            throw new OperacionException("El horario es ambiguo o inexistente por el cambio de hora.");
        DateTime utc = TimeZoneInfo.ConvertTimeToUtc(inicioLocal, zona);
        if (utc <= DateTime.UtcNow) throw new OperacionException("El turno debe ser futuro.");
        if (precio < 0 || precio > 9999999999999999.99m || decimal.Round(precio, 2) != precio)
            throw new OperacionException("El precio no es válido.");
        if (!(await HorariosAsync(profesional,servicio,inicioLocal.Date)).Any(x=>x.InicioLocal==inicioLocal))
            throw new OperacionException("Ese horario ya no está disponible o está fuera de la jornada. Actualizá los horarios.");
        using var scope = await Task.Run(scopes.Crear);
        long id = await scope.Turnos.CrearTurnoAsync(new Turno
        {
            ClienteId = cliente, PacienteId = paciente, ProfesionalId = profesional, ServicioId = servicio,
            InicioUtc = utc, FinUtc = utc.AddMinutes(s.DuracionMinutos), PrecioAcordado = precio, Moneda = c.Moneda
        });
        await sesion.ExigirAsync(Permiso.Reservar);
        scope.Commit();
        return id;
    }
}

