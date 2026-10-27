using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Infraestructura.SqlServer.Correo;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class TransporteSmtp : ITransporteCorreo
{
    private static string Ruta => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SistemaTurnos","smtp.protegido");
    private static ConfiguracionCorreo Leer()
    {
        if (!File.Exists(Ruta)) return new(false,"smtp.gmail.com",587,"","","","");
        if(new FileInfo(Ruta).Length>65536) throw new OperacionException("El archivo de configuración SMTP no es válido.");
        byte[] bytes=ProtectedData.Unprotect(File.ReadAllBytes(Ruta),null,DataProtectionScope.CurrentUser);
        try { return JsonSerializer.Deserialize<ConfiguracionCorreo>(bytes) ?? throw new InvalidOperationException("Configuración SMTP inválida."); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public Task<ConfiguracionCorreo> ConfiguracionAsync()
    { var c=Leer(); return Task.FromResult(c with { Password="",TienePassword=!string.IsNullOrEmpty(c.Password) }); }
    public Task GuardarConfiguracionAsync(ConfiguracionCorreo c)
    {
        var anterior=Leer();
        c=PoliticaCredencialesSmtp.Resolver(c,anterior);
        byte[] claro=JsonSerializer.SerializeToUtf8Bytes(c),protegido;
        try { protegido=ProtectedData.Protect(claro,null,DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(claro); }
        Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
        string temporal=Ruta+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllBytes(temporal,protegido); File.Move(temporal,Ruta,true); }
        finally { if(File.Exists(temporal)) File.Delete(temporal); }
        return Task.CompletedTask;
    }
    public async Task EnviarAsync(CorreoPendiente correo,CancellationToken cancellationToken)
    {
        var c=Leer(); if (!c.Habilitado) throw new InvalidOperationException("SMTP deshabilitado.");
        using var mensaje=new MailMessage
        {
            From=new MailAddress(c.Remitente,c.NombreRemitente),Subject=correo.Asunto,Body=correo.Cuerpo,
            SubjectEncoding=Encoding.UTF8,BodyEncoding=Encoding.UTF8,IsBodyHtml=false
        };
        mensaje.To.Add(new MailAddress(correo.Destinatario));
        using var smtp=new SmtpClient(c.Servidor,c.Puerto)
        { EnableSsl=true,UseDefaultCredentials=false,Credentials=new NetworkCredential(c.Usuario,c.Password),Timeout=30000 };
        await smtp.SendMailAsync(mensaje,cancellationToken);
    }
}
