using System.Text;
using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Aplicacion.Servicios;

public static class PoliticaPassword
{
    // Lista básica local: no envía contraseñas a terceros ni pretende cubrir todas las filtraciones.
    private static readonly HashSet<string> comunes=new(StringComparer.OrdinalIgnoreCase)
    { "123456789012345","1234567890123456","12345678901234567890","passwordpassword","password123456789",
      "qwertyuiopasdfgh","qwertyuiop123456","administrador123","adminadminadmin","sistematurnos123" };
    public static void ValidarNueva(string password,string usuario)
    {
        if(string.IsNullOrWhiteSpace(password)||password.Length>128||password.EnumerateRunes().Count()<15||password.Any(char.IsControl))
            throw new OperacionException("Usá una contraseña o frase de al menos 15 caracteres, con un máximo de 128 unidades de texto.");
        if(comunes.Contains(password)||password.Equals(usuario,StringComparison.OrdinalIgnoreCase)
            ||password.Equals(usuario+"123456789",StringComparison.OrdinalIgnoreCase))
            throw new OperacionException("La contraseña es demasiado predecible. Elegí una frase larga y única.");
    }
}
