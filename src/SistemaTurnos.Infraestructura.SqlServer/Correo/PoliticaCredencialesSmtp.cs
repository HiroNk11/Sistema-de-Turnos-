using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.Infraestructura.SqlServer.Correo;

public static class PoliticaCredencialesSmtp
{
    public static ConfiguracionCorreo Resolver(ConfiguracionCorreo nueva,ConfiguracionCorreo anterior)
    {
        if(string.IsNullOrEmpty(nueva.Password))
        {
            bool mismo=nueva.Usuario.Equals(anterior.Usuario,StringComparison.Ordinal)
                &&nueva.Servidor.Equals(anterior.Servidor,StringComparison.OrdinalIgnoreCase)&&nueva.Puerto==anterior.Puerto;
            nueva=nueva with { Password=mismo?anterior.Password:"" };
        }
        if(nueva.Habilitado&&string.IsNullOrEmpty(nueva.Password))
            throw new OperacionException("Ingresá una contraseña SMTP para este servidor y usuario.");
        return nueva with {TienePassword=!string.IsNullOrEmpty(nueva.Password)};
    }
}
