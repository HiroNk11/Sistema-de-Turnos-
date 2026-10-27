using Microsoft.Data.SqlClient;
using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Infraestructura.SqlServer.Seguridad;

public static class ConexionSegura
{
    public static string Validar(string cadena)
    {
        SqlConnectionStringBuilder b;
        try { b=new(cadena); }
        catch(ArgumentException) { throw new OperacionException("La cadena de conexión no es válida."); }
        if(string.IsNullOrWhiteSpace(b.DataSource)||string.IsNullOrWhiteSpace(b.InitialCatalog))
            throw new OperacionException("Configurá servidor y base de datos.");
        bool localDb=b.DataSource.StartsWith(@"(localdb)\",StringComparison.OrdinalIgnoreCase);
        if(!localDb&&(b.Encrypt.Equals(SqlConnectionEncryptOption.Optional)||b.TrustServerCertificate))
            throw new OperacionException("Fuera de LocalDB se exige Encrypt=True y TrustServerCertificate=False con un certificado válido.");
        b.PersistSecurityInfo=false; b.ConnectTimeout=Math.Clamp(b.ConnectTimeout,1,30);
        return b.ConnectionString;
    }
}
