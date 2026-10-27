namespace SistemaTurnos.WinForms;

internal static class DiagnosticoSeguro
{
    private static readonly object bloqueo=new();
    public static string Registrar(Exception error)
    {
        string id=Guid.NewGuid().ToString("N")[..12];
        try
        {
            lock(bloqueo)
            {
                string directorio=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SistemaTurnos");
                Directory.CreateDirectory(directorio); string ruta=Path.Combine(directorio,"errores-seguros.log");
                if(File.Exists(ruta)&&new FileInfo(ruta).Length>1048576) File.Move(ruta,ruta+".anterior",true);
                // Nunca registrar Message, Data, ToString, parámetros ni contenido de correos.
                File.AppendAllText(ruta,$"{DateTime.UtcNow:O} ID={id} Tipo={error.GetType().FullName} HResult={error.HResult}\n{error.StackTrace}\n");
            }
        }
        catch { }
        return id;
    }
}
