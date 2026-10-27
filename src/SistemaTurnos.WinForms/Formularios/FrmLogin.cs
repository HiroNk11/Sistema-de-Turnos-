using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Servicios;

namespace SistemaTurnos.WinForms.Formularios;

public sealed class FrmLogin : FormularioBase
{
    private readonly TextBox usuario = new() { MaxLength=50 };
    private readonly TextBox password = new() { UseSystemPasswordChar=true, MaxLength=128 };
    private readonly TextBox repetir = new() { UseSystemPasswordChar=true, MaxLength=128 };
    private readonly Button ingresar = Boton("Ingresar");
    private readonly Label aviso = new() { AutoSize=true, MaximumSize=new Size(330,0) };
    private bool inicial;
    internal void AplicarModo(bool primerAdministrador)
    {
        inicial=primerAdministrador;
        VisibilidadCampo(repetir,inicial);
        repetir.Enabled=inicial;
        ingresar.Text=inicial?"Crear administrador":"Ingresar";
        Presentacion(inicial?"Creá tu administrador":"Bienvenido de nuevo",
            inicial?"Configurá tu acceso para comenzar a trabajar.":"Ingresá a la agenda de tu negocio.");
        aviso.Text=inicial?"Usá una frase de contraseña de al menos 15 caracteres.":"Cinco intentos fallidos bloquean el acceso durante 15 minutos.";
    }
    internal bool RepeticionOculta => FilaOculta(repetir);
    public FrmLogin(IAccesoServicio acceso)
    {
        Text="Sistema de Turnos · Acceso";
        ingresar.Enabled=false;
        MaximizeBox=false;
        Size=new Size(630,640); MinimumSize=new Size(600,570);
        Campo("",aviso);
        Campo("Usuario",usuario); Campo("Contraseña",password); Campo("Repetir contraseña",repetir);
        var mostrar = new CheckBox { Text="Mostrar contraseña", AutoSize=true };
        mostrar.CheckedChanged += (_,_) => password.UseSystemPasswordChar=repetir.UseSystemPasswordChar=!mostrar.Checked;
        Campo("",mostrar); Campo("",ingresar);
        AplicarModo(false);
        AcceptButton=ingresar;
        Shown += async (_,_) => await EjecutarAsync(async () =>
        {
            AplicarModo(!await acceso.HayUsuariosAsync());
            ingresar.Enabled=true;
            usuario.Focus();
        });
        ingresar.Click += async (_,_) =>
        {
            bool autenticado=false;
            await EjecutarAsync(async () =>
            {
                if (inicial)
                {
                    await acceso.CrearAdministradorAsync(usuario.Text,password.Text,repetir.Text);
                    AplicarModo(false);
                    aviso.Text="Administrador creado. Ingresá con tu contraseña.";
                }
                else { await acceso.LoginAsync(usuario.Text,password.Text); autenticado=true; }
                password.Clear(); repetir.Clear();
            });
            if (autenticado) { DialogResult=DialogResult.OK; Close(); }
        };
    }
}

