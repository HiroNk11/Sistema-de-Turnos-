using System.Net.Mail;
using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Aplicacion.Servicios;

public sealed class CorreoServicio(IColaCorreo cola,ITransporteCorreo transporte,Sesion sesion) : ICorreoServicio
{
    private readonly SemaphoreSlim mutex=new(1,1);
    public async Task<ConfiguracionCorreo> ConfiguracionAsync()
    { await sesion.ExigirAsync(Permiso.Configurar); return await transporte.ConfiguracionAsync(); }
    public async Task<IReadOnlyList<CorreoEstado>> ListarAsync()
    { await sesion.ExigirAsync(Permiso.Configurar); return await cola.ListarCorreosAsync(); }
    public async Task ReintentarAsync(long id)
    { await sesion.ExigirAsync(Permiso.Configurar); await cola.ReintentarAsync(id); }
    public async Task GuardarAsync(ConfiguracionCorreo c)
    {
        await sesion.ExigirAsync(Permiso.Configurar);
        if(c.Servidor.Length>253||c.Usuario.Length>254||c.NombreRemitente.Length>100||c.Password.Length>1024
            || c.NombreRemitente.Any(char.IsControl)||c.Servidor.Any(char.IsControl)||c.Usuario.Any(char.IsControl))
            throw new OperacionException("Revisá los datos del servidor y remitente SMTP.");
        if (c.Servidor.Equals("smtp.gmail.com",StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(c.Password))
        {
            c=c with { Password=c.Password.Replace(" ","") };
            if (c.Password.Length!=16)
                throw new OperacionException("Gmail requiere la contraseña de aplicación de 16 caracteres, no la contraseña habitual de Google.");
        }
        if (c.Habilitado && (string.IsNullOrWhiteSpace(c.Servidor)||c.Puerto is < 1 or > 65535
            ||!MailAddress.TryCreate(c.Remitente,out var direccion)||direccion.Address!=c.Remitente
            ||string.IsNullOrWhiteSpace(c.Usuario)||string.IsNullOrEmpty(c.Password)&&!c.TienePassword))
            throw new OperacionException("Completá servidor, puerto, remitente, usuario y contraseña SMTP.");
        if (c.Habilitado && c.Puerto==465)
            throw new OperacionException("Esta versión usa STARTTLS. Configurá el puerto 587 o el puerto STARTTLS de tu proveedor.");
        await transporte.GuardarConfiguracionAsync(c);
    }
    public async Task<int> ProcesarAsync(CancellationToken token=default,int limite=10)
    {
        await sesion.ExigirAsync(Permiso.ProcesarCorreos,actividad:false);
        if (!await mutex.WaitAsync(0,token)) return 0;
        int enviados=0;
        try
        {
            if (!(await transporte.ConfiguracionAsync()).Habilitado) return 0;
            for (int i=0;i<Math.Clamp(limite,1,10) && !token.IsCancellationRequested;i++)
            {
                await sesion.ExigirAsync(Permiso.ProcesarCorreos,actividad:false);
                var correo=await cola.ReclamarAsync(); if (correo is null) break;
                await sesion.ExigirAsync(Permiso.ProcesarCorreos,actividad:false);
                if(!await cola.ReclamoVigenteAsync(correo.Id,correo.TokenReclamo)) continue;
                try
                {
                    using var plazo=CancellationTokenSource.CreateLinkedTokenSource(token);
                    plazo.CancelAfter(TimeSpan.FromSeconds(30));
                    await transporte.EnviarAsync(correo,plazo.Token);
                }
                catch (Exception ex) when (ex is SmtpException or OperationCanceledException or IOException or InvalidOperationException or FormatException or ArgumentException)
                { await cola.FinalizarAsync(correo.Id,correo.TokenReclamo,false); continue; }
                if(await cola.FinalizarAsync(correo.Id,correo.TokenReclamo,true)) enviados++;
            }
            return enviados;
        }
        finally { mutex.Release(); }
    }
}

