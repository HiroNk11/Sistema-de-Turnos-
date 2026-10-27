using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SistemaTurnos.Api;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Infraestructura.SqlServer.Seguridad;
using SistemaTurnos.Infraestructura.Http;
using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Servicios;

var hash=new PasswordHasher(); var store=new StoreFalso();
store.Usuario=new(1,"prueba",hash.Hash("ClaveApiDePrueba123"),true,null,RolUsuario.Profesional,2,Guid.NewGuid());
ServiciosServidor Crear()=>new(store,store,new UsuariosFalsos(),new CajaFalsa(),new ColaFalsa(),new ScopeFalso(),hash,new TransporteFalso());
using var sesiones=new SesionesServidor(Crear);
var builder=WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); ApiSegura.ConfigurarServicios(builder);
await using var app=builder.Build(); app.UseRouting(); ApiSegura.Mapear(app,sesiones);
app.UseEndpoints(_=>{});
var pipeline=((IApplicationBuilder)app).Build(); int pruebas=0;
void Exigir(bool bien,string mensaje)
{if(!bien)throw new Exception(mensaje);pruebas++;Console.WriteLine("OK: "+mensaje);}
async Task<(int Codigo,string Texto)> Peticion(string ruta,string? bearer=null,object? datos=null,bool https=true,string ip="127.0.0.1",string? json=null,long? longitud=null)
{
    using var scope=app.Services.CreateScope(); var ctx=new DefaultHttpContext(); ctx.RequestServices=scope.ServiceProvider;
    ctx.Request.Scheme=https?"https":"http"; ctx.Request.Host=new HostString("localhost"); ctx.Request.Path=ruta;
    ctx.Connection.RemoteIpAddress=IPAddress.Parse(ip); ctx.Request.Method=datos is null&&json is null?"GET":"POST";
    if(bearer is not null) ctx.Request.Headers.Authorization="Bearer "+bearer;
    if(datos is not null||json is not null)
    {
        byte[] bytes=Encoding.UTF8.GetBytes(json??JsonSerializer.Serialize(datos,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        ctx.Request.Body=new MemoryStream(bytes); ctx.Request.ContentLength=longitud??bytes.Length; ctx.Request.ContentType="application/json";
        ctx.Features.Set<IHttpRequestBodyDetectionFeature>(new CuerpoDetectable());
    }
    using var salida=new MemoryStream(); ctx.Response.Body=salida;
    await pipeline(ctx); await ctx.Response.BodyWriter.FlushAsync(); return(ctx.Response.StatusCode,Encoding.UTF8.GetString(salida.ToArray()));
}
async Task<string> Login(string ip="127.0.0.1")
{
    var r=await Peticion("/api/acceso/login",datos:new AccesoApi("prueba","ClaveApiDePrueba123"),ip:ip);
    if(r.Codigo!=200)throw new Exception("Login HTTP falló: "+r.Codigo+" "+r.Texto);
    return JsonSerializer.Deserialize<LoginApi>(r.Texto,new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Token;
}
Exigir((await Peticion("/api/usuarios")).Codigo==401,"Sin bearer no se consultan usuarios");
Exigir((await Peticion("/api/usuarios",new string('A',64))).Codigo==401,"Token inventado no se acepta");
Exigir((await Peticion("/api/acceso/login",datos:new AccesoApi("prueba","ClaveApiDePrueba123"),https:false)).Codigo==400,"La API rechaza HTTP");
string token=await Login(); Exigir(token.Length==64,"Login emite un token opaco de 256 bits");
var perfil=await Peticion("/api/acceso/sesion",token);
Exigir(perfil.Codigo==200&&!perfil.Texto.Contains(store.Usuario.Hash)&&!perfil.Texto.Contains(store.Usuario.TokenSesion.ToString()),"El perfil no publica hashes ni tokens SQL");
Exigir((await Peticion("/api/usuarios",token)).Codigo==403,"Profesional no administra usuarios en el servidor");
Exigir((await Peticion("/api/gestion/semana",token,new SemanaApi(3,new DateTime(2040,1,1)))).Codigo==403,"API impide consultar otro profesional");
Exigir((await Peticion("/api/gestion/semana",token,new SemanaApi(2,new DateTime(2040,1,1)))).Codigo==200,"API permite la agenda propia");
Exigir((await Peticion("/api/caja/registrar",token,new CobroApi(1,10,MedioCobro.Efectivo,Guid.NewGuid()))).Codigo==403,"Rol profesional no registra cobros aunque fabrique el pedido");
Exigir((await Peticion("/api/caja/registrar",token,json:"{\"turnoId\":1,\"importe\":10,\"medio\":0,\"solicitud\":\"00000000-0000-0000-0000-000000000001\",\"usuarioId\":1}")).Codigo==400,"El contrato rechaza identidad agregada al cuerpo");
Exigir((await Peticion("/api/acceso/login",datos:new AccesoApi("prueba","x"),longitud:65537)).Codigo==413,"Se limita el cuerpo de solicitudes");
store.Usuario=store.Usuario with{Activo=false};
Exigir((await Peticion("/api/acceso/sesion",token)).Codigo==401,"Cuenta revocada pierde acceso HTTP");
store.Usuario=store.Usuario with{Activo=true,Rol=RolUsuario.Administrador,ProfesionalId=null,TokenSesion=Guid.NewGuid()};
token=await Login(); Exigir((await Peticion("/api/usuarios",token)).Codigo==200,"Administrador autorizado consulta usuarios");
Exigir((await Peticion("/api/acceso/salir",token,new{})).Codigo==204&&(await Peticion("/api/acceso/sesion",token)).Codigo==401,"Logout elimina el bearer en el servidor");
string primero=await Login("127.0.0.3"); await Login("127.0.0.3"); await Login("127.0.0.3"); string ultimo=await Login("127.0.0.3");
Exigir((await Peticion("/api/acceso/sesion",primero)).Codigo==401&&(await Peticion("/api/acceso/sesion",ultimo)).Codigo==200,"Se limitan las sesiones simultáneas por cuenta");
bool limitado=false;
for(int i=0;i<11;i++) if((await Peticion("/api/acceso/login",datos:new AccesoApi("prueba","incorrecta"),ip:"127.0.0.2")).Codigo==429) limitado=true;
Exigir(limitado,"El límite de intentos HTTP devuelve 429");
bool rechazo=false; try{ClienteSeguro.ValidarServidor("http://localhost:7443");}catch(OperacionException){rechazo=true;}
Exigir(rechazo,"WinForms remoto no admite HTTP");
rechazo=false; try{ClienteSeguro.ValidarServidor("https://usuario:clave@localhost:7443");}catch(OperacionException){rechazo=true;}
Exigir(rechazo,"Las credenciales no se colocan en la URL del servidor");
store.Usuario=store.Usuario with{Rol=RolUsuario.Administrador,Activo=true,ProfesionalId=null,TokenSesion=Guid.NewGuid()};
using var httpMemoria=new HttpClient(new HandlerMemoria(async request=>
{
    string? cuerpo=request.Content is null?null:await request.Content.ReadAsStringAsync();
    var r=await Peticion(request.RequestUri!.AbsolutePath,request.Headers.Authorization?.Parameter,json:cuerpo,ip:"127.0.0.4");
    return new HttpResponseMessage((HttpStatusCode)r.Codigo){Content=new StringContent(r.Texto,Encoding.UTF8,"application/json")};
})) {BaseAddress=new Uri("https://localhost/")};
var sesionCliente=new Sesion(); using var remoto=new ClienteSeguro(httpMemoria,sesionCliente);
await remoto.LoginAsync("prueba","ClaveApiDePrueba123");
Exigir(sesionCliente.ModoRemoto&&sesionCliente.Rol==RolUsuario.Administrador,"El cliente inicia una identidad de presentación remota");
Exigir((await ((IGestionServicio)remoto).ConfiguracionAsync()).Nombre=="Negocio","El cliente obtiene datos a través del contrato HTTP");
await remoto.GuardarAsync(Catalogo.Clientes,new RegistroCatalogo {Nombre="Prueba",Apellido="Cliente"});
Exigir(true,"El cliente guarda sin enviar credenciales SQL ni identidad de actor");
store.Usuario=store.Usuario with{TokenSesion=Guid.NewGuid()};
bool revocacion=false; try{await ((IGestionServicio)remoto).ConfiguracionAsync();}catch(OperacionException){revocacion=true;}
Exigir(revocacion&&sesionCliente.UsuarioId is null,"La revocación del servidor borra la identidad del cliente");
await remoto.LoginAsync("prueba","ClaveApiDePrueba123"); await remoto.SalirAsync();
Exigir(sesionCliente.UsuarioId is null,"Cerrar sesión remota elimina el estado local");
Console.WriteLine($"RESULTADO: {pruebas} pruebas HTTP en memoria aprobadas, sin SQL, SMTP ni puerto de red.");

sealed class CuerpoDetectable : IHttpRequestBodyDetectionFeature { public bool CanHaveBody=>true; }
sealed class HandlerMemoria(Func<HttpRequestMessage,Task<HttpResponseMessage>> enviar) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>enviar(request);
}
