using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Aplicacion.Servicios;

namespace SistemaTurnos.Infraestructura.Http;

public sealed class ClienteSeguro : IAccesoServicio,IGestionServicio,ICajaServicio,IUsuariosServicio,ICorreoServicio,IDisposable
{
    private readonly HttpClient http;
    private readonly Sesion sesion;
    private string? token;
    public static Uri ValidarServidor(string direccion)
    {
        if(!Uri.TryCreate(direccion,UriKind.Absolute,out var uri)||uri.Scheme!=Uri.UriSchemeHttps
            ||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)||uri.AbsolutePath!="/")
            throw new OperacionException("Configurá una URL HTTPS de servidor, sin usuario, contraseña, ruta ni parámetros.");
        return uri;
    }
    private static HttpClient CrearHttp(string servidor)
    {
        var uri=ValidarServidor(servidor);
        // Sin redirecciones, cookies, credenciales Windows ni excepciones de certificado.
        return new HttpClient(new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false,UseDefaultCredentials=false})
            {BaseAddress=uri,Timeout=TimeSpan.FromSeconds(45),MaxResponseContentBufferSize=8*1024*1024};
    }
    public ClienteSeguro(string servidor,Sesion sesion):this(CrearHttp(servidor),sesion) { }
    internal ClienteSeguro(HttpClient cliente,Sesion sesion)
    { http=cliente; this.sesion=sesion; ValidarServidor(cliente.BaseAddress?.ToString()??""); }
    private async Task<T> Enviar<T>(string ruta,object? datos=null,CancellationToken cancelacion=default)
    {
        string? enviado=token;
        using var request=new HttpRequestMessage(datos is null?HttpMethod.Get:HttpMethod.Post,ruta);
        if(enviado is not null) request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",enviado);
        if(datos is not null) request.Content=JsonContent.Create(datos);
        HttpResponseMessage response;
        try { response=await http.SendAsync(request,cancelacion).ConfigureAwait(false); }
        catch(HttpRequestException) { throw new OperacionException("No se pudo conectar con el servidor seguro. Verificá la dirección y su certificado."); }
        catch(OperationCanceledException) when(!cancelacion.IsCancellationRequested)
        { throw new OperacionException("El servidor tardó demasiado. Revisá el estado de la operación antes de reintentar."); }
        using(response)
        {
            if(!response.IsSuccessStatusCode)
            {
                if(response.StatusCode==HttpStatusCode.Unauthorized&&token==enviado) { token=null; sesion.Cerrar(); }
                ErrorApi? error=null;
                if(response.Content.Headers.ContentLength is null or <=65536)
                    try { error=await response.Content.ReadFromJsonAsync<ErrorApi>(cancellationToken:cancelacion).ConfigureAwait(false); } catch(System.Text.Json.JsonException) { }
                throw new OperacionException(response.StatusCode==HttpStatusCode.TooManyRequests?"Demasiados intentos. Esperá un minuto.":error?.Mensaje??"La operación fue rechazada por el servidor.");
            }
            if(response.StatusCode==HttpStatusCode.NoContent) return default!;
            try
            {
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken:cancelacion).ConfigureAwait(false)
                    ??throw new OperacionException("El servidor devolvió una respuesta inválida.");
            }
            catch(System.Text.Json.JsonException) {throw new OperacionException("El servidor devolvió una respuesta inválida.");}
        }
    }
    private async Task Ejecutar(string ruta,object datos)=>await Enviar<object>(ruta,datos).ConfigureAwait(false);
    public Task<bool> HayUsuariosAsync()=>Task.FromResult(true);
    public Task CrearAdministradorAsync(string nombre,string password,string repeticion)=>Task.FromException(new OperacionException("El administrador inicial se crea desde la consola del servidor."));
    public async Task LoginAsync(string nombre,string password)
    {
        sesion.Cerrar(); token=null;
        var resultado=await Enviar<LoginApi>("api/acceso/login",new AccesoApi(nombre,password)).ConfigureAwait(false);
        token=resultado.Token;
        await sesion.IniciarRemotaAsync(resultado.Perfil,async ()=>
        {
            try { var perfil=await Enviar<PerfilSesion>("api/acceso/sesion").ConfigureAwait(false); return perfil==resultado.Perfil; }
            catch(OperacionException) { return false; }
        }).ConfigureAwait(false);
    }
    public async Task SalirAsync()
    {
        try { if(token is not null) await Ejecutar("api/acceso/salir",new{}).ConfigureAwait(false); }
        catch(Exception ex) when(ex is OperacionException or OperationCanceledException) { }
        finally { token=null; sesion.Cerrar(); }
    }
    public async Task<IReadOnlyList<RegistroCatalogo>> ListarAsync(Catalogo tipo)=>await Enviar<List<RegistroCatalogo>>("api/gestion/listar",new CatalogoApi(tipo)).ConfigureAwait(false);
    public Task GuardarAsync(Catalogo tipo,RegistroCatalogo r)=>Ejecutar("api/gestion/guardar",new GuardarCatalogoApi(tipo,r));
    Task<Configuracion> IGestionServicio.ConfiguracionAsync()=>Enviar<Configuracion>("api/gestion/configuracion");
    public Task GuardarConfiguracionAsync(Configuracion c)=>Ejecutar("api/gestion/configuracion",c);
    public async Task<IReadOnlyList<TurnoListado>> AgendaAsync(DateTime dia)=>await Enviar<List<TurnoListado>>("api/gestion/agenda",new FechaApi(dia)).ConfigureAwait(false);
    public async Task<IReadOnlyList<TurnoListado>> AgendaSemanalAsync(int p,DateTime fecha)=>await Enviar<List<TurnoListado>>("api/gestion/semana",new SemanaApi(p,fecha)).ConfigureAwait(false);
    public async Task<IReadOnlyList<Jornada>> JornadasAsync(int p)=>await Enviar<List<Jornada>>("api/gestion/jornadas",new ProfesionalApi(p)).ConfigureAwait(false);
    public Task GuardarJornadasAsync(int p,IReadOnlyList<Jornada> j)=>Ejecutar("api/gestion/jornadas/guardar",new JornadasApi(p,j));
    public async Task<IReadOnlyList<HorarioLibre>> HorariosAsync(int p,int s,DateTime d)=>await Enviar<List<HorarioLibre>>("api/gestion/horarios",new HorariosApi(p,s,d)).ConfigureAwait(false);
    public Task<long> CrearTurnoAsync(int c,int? paciente,int p,int s,DateTime inicio,decimal precio)=>Enviar<long>("api/gestion/reservar",new ReservaApi(c,paciente,p,s,inicio,precio));
    public Task<DateTime> HoyAsync()=>Enviar<DateTime>("api/caja/hoy");
    public Task<CuentaTurno> CuentaAsync(long id)=>Enviar<CuentaTurno>("api/caja/cuenta",new IdApi(id));
    public async Task<IReadOnlyList<CobroListado>> CobrosAsync(long id)=>await Enviar<List<CobroListado>>("api/caja/cobros",new IdApi(id)).ConfigureAwait(false);
    public Task<long> RegistrarAsync(long id,decimal importe,MedioCobro medio,Guid solicitud)=>Enviar<long>("api/caja/registrar",new CobroApi(id,importe,medio,solicitud));
    public Task AnularAsync(long id,string motivo)=>Ejecutar("api/caja/anular",new AnularApi(id,motivo));
    public async Task<IReadOnlyList<ResumenCobro>> InformeAsync(DateTime f,PeriodoInforme p)=>await Enviar<List<ResumenCobro>>("api/caja/informe",new InformeApi(f,p)).ConfigureAwait(false);
    public bool EsCuentaActual(int id)=>sesion.UsuarioId==id;
    async Task<IReadOnlyList<UsuarioGestion>> IUsuariosServicio.ListarAsync()=>await Enviar<List<UsuarioGestion>>("api/usuarios").ConfigureAwait(false);
    public Task GuardarAsync(UsuarioGestion u,string password)=>Ejecutar("api/usuarios",new UsuarioApi(u,password));
    Task<ConfiguracionCorreo> ICorreoServicio.ConfiguracionAsync()=>Enviar<ConfiguracionCorreo>("api/correo/configuracion");
    async Task<IReadOnlyList<CorreoEstado>> ICorreoServicio.ListarAsync()=>await Enviar<List<CorreoEstado>>("api/correo/listar").ConfigureAwait(false);
    public Task ReintentarAsync(long id)=>Ejecutar("api/correo/reintentar",new IdApi(id));
    public Task GuardarAsync(ConfiguracionCorreo c)=>Ejecutar("api/correo/configuracion",c);
    public Task<int> ProcesarAsync(CancellationToken token=default,int limite=10)=>Enviar<int>("api/correo/procesar",new{},token);
    public void Dispose() { token=null; http.Dispose(); }
}
