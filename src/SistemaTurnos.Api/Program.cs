using SistemaTurnos.Api;
using SistemaTurnos.Infraestructura.SqlServer.Correo;
using SistemaTurnos.Infraestructura.SqlServer.Repositorios;
using SistemaTurnos.Infraestructura.SqlServer.Seguridad;
using SistemaTurnos.Infraestructura.SqlServer.Transacciones;

if(!OperatingSystem.IsWindows()) throw new InvalidOperationException("Esta versión almacena SMTP con DPAPI de Windows.");
string cadena=Environment.GetEnvironmentVariable("SISTEMATURNOS_CONNECTION")
    ??throw new InvalidOperationException("Configurá SISTEMATURNOS_CONNECTION solo en el servidor.");
var store=new AppStore(ConexionSegura.Validar(cadena));
ServiciosServidor Crear()
{
    if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
    return new(store,store,store,store,store,new TurnoScopeFactory(cadena),new PasswordHasher(),new TransporteSmtp());
}
if(args.Contains("--crear-administrador"))
{
    Console.Write("Usuario administrador: "); string usuario=Console.ReadLine()??"";
    string LeerSecreto(string mensaje)
    {
        Console.Write(mensaje); var caracteres=new List<char>();
        while(true)
        {
            var tecla=Console.ReadKey(true); if(tecla.Key==ConsoleKey.Enter) break;
            if(tecla.Key==ConsoleKey.Backspace){if(caracteres.Count>0)caracteres.RemoveAt(caracteres.Count-1);continue;}
            if(!char.IsControl(tecla.KeyChar)&&caracteres.Count<128)caracteres.Add(tecla.KeyChar);
        }
        Console.WriteLine(); string valor=new(caracteres.ToArray()); caracteres.Clear(); return valor;
    }
    await Crear().Acceso.CrearAdministradorAsync(usuario,LeerSecreto("Contraseña: "),LeerSecreto("Repetir: "));
    Console.WriteLine("Administrador inicial creado."); return;
}
var builder=WebApplication.CreateBuilder(args);
if(!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration["Kestrel:Certificates:Default:Path"]))
    throw new InvalidOperationException("Configurá un certificado HTTPS explícito para el servidor fuera de Development.");
if(Environment.GetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED")?.Equals("true",StringComparison.OrdinalIgnoreCase)==true)
    throw new InvalidOperationException("No se admiten encabezados de proxy sin configurar explícitamente una frontera de confianza.");
string escucha=Environment.GetEnvironmentVariable("SISTEMATURNOS_HTTPS_BIND")??"https://localhost:7443";
if(!Uri.TryCreate(escucha,UriKind.Absolute,out var enlace)||enlace.Scheme!="https"||!string.IsNullOrEmpty(enlace.UserInfo)||enlace.AbsolutePath!="/"
    ||!string.IsNullOrEmpty(enlace.Query)||!string.IsNullOrEmpty(enlace.Fragment))
    throw new InvalidOperationException("La API solo admite un enlace HTTPS explícito.");
builder.WebHost.UseUrls(escucha);
builder.WebHost.ConfigureKestrel(k=>
{
    k.AddServerHeader=false; k.Limits.MaxRequestBodySize=65536; k.Limits.MaxRequestHeadersTotalSize=16384;
    k.Limits.RequestHeadersTimeout=TimeSpan.FromSeconds(15);
});
ApiSegura.ConfigurarServicios(builder);
var app=builder.Build();
using var sesiones=new SesionesServidor(Crear,TimeSpan.FromMinutes(1));
ApiSegura.Mapear(app,sesiones);
app.Run();
