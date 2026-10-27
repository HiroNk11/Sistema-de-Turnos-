using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SistemaTurnos.Api;

// PKI exclusiva de esta prueba: claves en memoria y confianza limitada al HttpClient.
// No instala certificados, no modifica el cliente de producción y no usa SQL/SMTP.
using var rootKey=RSA.Create(2048);
var rootRequest=new CertificateRequest("CN=Turnos Test Root",rootKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign,true));
using var root=rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddDays(1));
using var key=RSA.Create(2048);
var request=new CertificateRequest("CN=Turnos TLS Test",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));
request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection{new Oid("1.3.6.1.5.5.7.3.1")},true));
var names=new SubjectAlternativeNameBuilder();names.AddIpAddress(IPAddress.Loopback);request.CertificateExtensions.Add(names.Build());
using var leaf=request.Create(root,DateTimeOffset.UtcNow.AddMinutes(-2),DateTimeOffset.UtcNow.AddHours(1),RandomNumberGenerator.GetBytes(16));
using var certificate=leaf.CopyWithPrivateKey(key);
var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(k=>k.Listen(IPAddress.Loopback,0,e=>e.UseHttps(certificate)));
ApiSegura.ConfigurarServicios(builder);
await using var app=builder.Build();
using var sesiones=new SesionesServidor(()=>throw new InvalidOperationException("Esta prueba no inicia sesiones."));
ApiSegura.Mapear(app,sesiones);
using var limite=new CancellationTokenSource(TimeSpan.FromSeconds(40));
await app.StartAsync(limite.Token);
string url=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var handler=new HttpClientHandler {UseProxy=false};
handler.ServerCertificateCustomValidationCallback=(_,cert,_,errors)=>
{
    if(cert is null || (errors&(SslPolicyErrors.RemoteCertificateNameMismatch|SslPolicyErrors.RemoteCertificateNotAvailable))!=0)return false;
    using var chain=new X509Chain();
    chain.ChainPolicy.TrustMode=X509ChainTrustMode.CustomRootTrust;
    chain.ChainPolicy.CustomTrustStore.Add(root);
    // La CA efímera no publica CRL/OCSP. Esta política pertenece solo a esta prueba.
    chain.ChainPolicy.RevocationMode=X509RevocationMode.NoCheck;
    chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
    return chain.Build(cert);
};
using var cliente=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(8)};
using var respuesta=await cliente.GetAsync(url+"/api/acceso/sesion",limite.Token);
if(respuesta.StatusCode!=HttpStatusCode.Unauthorized || respuesta.Headers.CacheControl?.NoStore!=true)throw new Exception("HTTPS no devolvió 401 con no-store.");
Console.WriteLine("OK: HTTPS real con CA confiable para este cliente y rechazo de acceso anónimo.");
using var normal=new HttpClient(new HttpClientHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(8)};
bool rechazo=false;
try{using var r=await normal.GetAsync(url+"/api/acceso/sesion",limite.Token);}catch(HttpRequestException){rechazo=true;}
if(!rechazo)throw new Exception("Se aceptó una CA no confiable para el cliente normal.");
Console.WriteLine("OK: Un cliente normal rechaza el certificado de pruebas no confiable.");
rechazo=false;
try{using var r=await cliente.GetAsync(url.Replace("127.0.0.1","localhost")+"/api/acceso/sesion",limite.Token);}catch(HttpRequestException){rechazo=true;}
if(!rechazo)throw new Exception("Se aceptó un nombre ajeno al certificado.");
Console.WriteLine("OK: Se rechaza un nombre de servidor que no coincide con el certificado.");
using var apagar=new CancellationTokenSource(TimeSpan.FromSeconds(5));await app.StopAsync(apagar.Token);
Console.WriteLine("RESULTADO: 3 pruebas TLS reales aprobadas sin modificar el almacén de certificados.");
