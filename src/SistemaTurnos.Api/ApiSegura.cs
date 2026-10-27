using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Api;

public static class ApiSegura
{
    public static void ConfigurarServicios(WebApplicationBuilder builder)
    {
        builder.Services.ConfigureHttpJsonOptions(o=>
        { o.SerializerOptions.MaxDepth=16; o.SerializerOptions.UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow; });
        builder.Services.AddRateLimiter(o=>
        {
            o.RejectionStatusCode=StatusCodes.Status429TooManyRequests;
            o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(ctx=>RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString()??"local",_=>new FixedWindowRateLimiterOptions {PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
            o.AddPolicy("login",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"local",
                _=>new FixedWindowRateLimiterOptions {PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
        });
    }
    public static void Mapear(WebApplication app,SesionesServidor sesiones)
    {
        app.Use(async (ctx,next)=>
        {
            ctx.Response.Headers.CacheControl="no-store"; ctx.Response.Headers.XContentTypeOptions="nosniff";
            if(ctx.Request.ContentLength>65536) {ctx.Response.StatusCode=413;return;}
            if(!ctx.Request.IsHttps) { ctx.Response.StatusCode=400; await ctx.Response.WriteAsJsonAsync(new ErrorApi("Se requiere HTTPS.","HTTPS_REQUIRED")); return; }
            try { await next(ctx); }
            catch(OperacionException ex)
            {
                if(ctx.Response.HasStarted) throw;
                bool activa=ctx.Items["servicios"] is ServiciosServidor s&&s.Sesion.UsuarioId.HasValue;
                ctx.Response.StatusCode=ctx.Request.Path=="/api/acceso/login"||!activa?401:403;
                await ctx.Response.WriteAsJsonAsync(new ErrorApi(ex.Message,"OPERACION_RECHAZADA"));
            }
            catch(Exception ex) when(ex is BadHttpRequestException or JsonException or ArgumentException)
            { ctx.Response.StatusCode=400; await ctx.Response.WriteAsJsonAsync(new ErrorApi("Solicitud inválida.","INVALID_REQUEST")); }
            catch(Exception ex)
            {
                string id=Guid.NewGuid().ToString("N");
                app.Logger.LogError("Error API {Id}; tipo {Tipo}; HResult {Resultado}",id,ex.GetType().Name,ex.HResult);
                if(ctx.Response.HasStarted) throw;
                ctx.Response.StatusCode=500; await ctx.Response.WriteAsJsonAsync(new ErrorApi("No se pudo completar la operación.",id));
            }
        });
        app.UseRateLimiter();
        app.Use(async (ctx,next)=>
        {
            if(ctx.Request.Path=="/api/acceso/login") { await next(ctx); return; }
            string cabecera=ctx.Request.Headers.Authorization.ToString();
            string token=cabecera.StartsWith("Bearer ",StringComparison.Ordinal)?cabecera[7..]:"";
            var entrada=sesiones.Obtener(token);
            if(entrada is null) { ctx.Response.StatusCode=401; return; }
            await entrada.Exclusivo.WaitAsync(ctx.RequestAborted);
            try
            {
                bool actividad=ctx.Request.Path!="/api/acceso/sesion"&&ctx.Request.Path!="/api/correo/procesar";
                await entrada.Servicios.Sesion.ExigirAccesoAsync(actividad);
                if(actividad) entrada.Actividad=DateTime.UtcNow;
                ctx.Items["servicios"]=entrada.Servicios; ctx.Items["token"]=token;
                await next(ctx);
            }
            finally { entrada.Exclusivo.Release(); }
        });
        app.MapPost("/api/acceso/login",async (AccesoApi p)=>Results.Ok(await sesiones.LoginAsync(p))).RequireRateLimiting("login");
        app.MapGet("/api/acceso/sesion",(HttpContext c)=>Results.Ok(Servicios(c).Perfil()));
        app.MapPost("/api/acceso/salir",(HttpContext c)=> { sesiones.Quitar((string)c.Items["token"]!); return Results.NoContent(); });
        app.MapPost("/api/gestion/listar",async (CatalogoApi p,HttpContext c)=>Results.Ok(await Servicios(c).Gestion.ListarAsync(p.Tipo)));
        app.MapPost("/api/gestion/guardar",async (GuardarCatalogoApi p,HttpContext c)=> { await Servicios(c).Gestion.GuardarAsync(p.Tipo,p.Registro); return Results.NoContent(); });
        app.MapGet("/api/gestion/configuracion",(Func<HttpContext,Task<IResult>>)(async c=>Results.Ok(await Servicios(c).Gestion.ConfiguracionAsync())));
        app.MapPost("/api/gestion/configuracion",async (Configuracion p,HttpContext c)=> { await Servicios(c).Gestion.GuardarConfiguracionAsync(p); return Results.NoContent(); });
        app.MapPost("/api/gestion/agenda",async (FechaApi p,HttpContext c)=>Results.Ok(await Servicios(c).Gestion.AgendaAsync(p.Fecha)));
        app.MapPost("/api/gestion/semana",async (SemanaApi p,HttpContext c)=>Results.Ok(await Servicios(c).Gestion.AgendaSemanalAsync(p.Profesional,p.Fecha)));
        app.MapPost("/api/gestion/jornadas",async (ProfesionalApi p,HttpContext c)=>Results.Ok(await Servicios(c).Gestion.JornadasAsync(p.Profesional)));
        app.MapPost("/api/gestion/jornadas/guardar",async (JornadasApi p,HttpContext c)=> { await Servicios(c).Gestion.GuardarJornadasAsync(p.Profesional,p.Jornadas); return Results.NoContent(); });
        app.MapPost("/api/gestion/horarios",async (HorariosApi p,HttpContext c)=>Results.Ok(await Servicios(c).Gestion.HorariosAsync(p.Profesional,p.Servicio,p.Dia)));
        app.MapPost("/api/gestion/reservar",async (ReservaApi p,HttpContext c)=>Results.Ok(await Servicios(c).Gestion.CrearTurnoAsync(p.Cliente,p.Paciente,p.Profesional,p.Servicio,p.InicioLocal,p.Precio)));
        app.MapGet("/api/caja/hoy",(Func<HttpContext,Task<IResult>>)(async c=>Results.Ok(await Servicios(c).Caja.HoyAsync())));
        app.MapPost("/api/caja/cuenta",async (IdApi p,HttpContext c)=>Results.Ok(await Servicios(c).Caja.CuentaAsync(p.Id)));
        app.MapPost("/api/caja/cobros",async (IdApi p,HttpContext c)=>Results.Ok(await Servicios(c).Caja.CobrosAsync(p.Id)));
        app.MapPost("/api/caja/registrar",async (CobroApi p,HttpContext c)=>Results.Ok(await Servicios(c).Caja.RegistrarAsync(p.TurnoId,p.Importe,p.Medio,p.Solicitud)));
        app.MapPost("/api/caja/anular",async (AnularApi p,HttpContext c)=> { await Servicios(c).Caja.AnularAsync(p.Id,p.Motivo); return Results.NoContent(); });
        app.MapPost("/api/caja/informe",async (InformeApi p,HttpContext c)=>Results.Ok(await Servicios(c).Caja.InformeAsync(p.Fecha,p.Periodo)));
        app.MapGet("/api/usuarios",(Func<HttpContext,Task<IResult>>)(async c=>Results.Ok(await Servicios(c).Usuarios.ListarAsync())));
        app.MapPost("/api/usuarios",async (UsuarioApi p,HttpContext c)=> { await Servicios(c).Usuarios.GuardarAsync(p.Usuario,p.Password); return Results.NoContent(); });
        app.MapGet("/api/correo/configuracion",(Func<HttpContext,Task<IResult>>)(async c=>Results.Ok(await Servicios(c).Correo.ConfiguracionAsync())));
        app.MapPost("/api/correo/configuracion",async (ConfiguracionCorreo p,HttpContext c)=> { await Servicios(c).Correo.GuardarAsync(p); return Results.NoContent(); });
        app.MapGet("/api/correo/listar",(Func<HttpContext,Task<IResult>>)(async c=>Results.Ok(await Servicios(c).Correo.ListarAsync())));
        app.MapPost("/api/correo/reintentar",async (IdApi p,HttpContext c)=> { await Servicios(c).Correo.ReintentarAsync(p.Id); return Results.NoContent(); });
        app.MapPost("/api/correo/procesar",(Func<HttpContext,Task<IResult>>)(async c=>Results.Ok(await Servicios(c).Correo.ProcesarAsync(c.RequestAborted,1))));
    }
    private static ServiciosServidor Servicios(HttpContext c)=>(ServiciosServidor)c.Items["servicios"]!;
}
