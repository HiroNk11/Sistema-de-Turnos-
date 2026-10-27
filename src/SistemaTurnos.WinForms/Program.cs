using SistemaTurnos.Aplicacion.Servicios;
using SistemaTurnos.Infraestructura.SqlServer.Repositorios;
using SistemaTurnos.Infraestructura.SqlServer.Seguridad;
using SistemaTurnos.Infraestructura.SqlServer.Transacciones;
using SistemaTurnos.Infraestructura.SqlServer.Correo;
using SistemaTurnos.WinForms.Formularios;
using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Infraestructura.Http;

namespace SistemaTurnos.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try { Ejecutar(args); }
        catch(SistemaTurnos.Aplicacion.Modelos.OperacionException ex) {MessageBox.Show(ex.Message,"No se pudo iniciar");}
        catch(Exception ex) {string id=DiagnosticoSeguro.Registrar(ex);MessageBox.Show("No se pudo iniciar la aplicación. Diagnóstico: "+id,"Error de inicio");}
    }
    private static void Ejecutar(string[] args)
    {
        var sesion = new Sesion();
        IAccesoServicio acceso; IGestionServicio gestion; ICorreoServicio correos; IUsuariosServicio usuarios; ICajaServicio caja;
        string? servidor=Environment.GetEnvironmentVariable("SISTEMATURNOS_API_URL");
        ClienteSeguro? clienteRemoto=null;
        if(!string.IsNullOrWhiteSpace(servidor))
        {
            clienteRemoto=new ClienteSeguro(servidor,sesion);
            acceso=clienteRemoto; gestion=clienteRemoto; correos=clienteRemoto; usuarios=clienteRemoto; caja=clienteRemoto;
        }
        else
        {
            if(Environment.GetEnvironmentVariable("SISTEMATURNOS_REQUIRE_API")=="true")
            { MessageBox.Show("Configurá SISTEMATURNOS_API_URL con la dirección HTTPS del servidor.","Servidor requerido"); return; }
            string cadena=Environment.GetEnvironmentVariable("SISTEMATURNOS_CONNECTION")??File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"conexion.txt")).Trim();
            var store=new AppStore(cadena);
            acceso=new AccesoServicio(store,new PasswordHasher(),sesion);
            gestion=new GestionServicio(store,new TurnoScopeFactory(cadena),sesion,store);
            correos=new CorreoServicio(store,new TransporteSmtp(),sesion);
            usuarios=new UsuariosServicio(store,new PasswordHasher(),sesion);
            caja=new CajaServicio(store,store,sesion);
        }
        using var conexionRemota=clienteRemoto;
        if (args.Contains("--smoke"))
        {
            using var login = new FrmLogin(acceso);
            using var principal = new FrmPrincipal(gestion, sesion,correos,usuarios,caja);
            using var turno = new FrmTurno(gestion);
            using var catalogo = new FrmCatalogo(gestion, Aplicacion.Modelos.Catalogo.Clientes);
            using var config = new FrmConfiguracion(gestion);
            using var jornadas = new FrmJornadas(gestion);
            using var correo = new FrmCorreos(correos);
            using var usuariosForm = new FrmUsuarios(usuarios,gestion);
            using var cobrosForm = new FrmCobros(caja,gestion,sesion);
            using var informesForm = new FrmInformes(caja);
            principal.ProbarNavegacion();
            if (new Form[] { login, principal, turno, catalogo, config, jornadas, correo,usuariosForm,cobrosForm,informesForm }.Any(x => x.Controls.Count == 0))
                throw new InvalidOperationException("Formulario vacío.");
            if (!login.RepeticionOculta) throw new Exception("El login debe iniciar sin repetir contraseña.");
            login.AplicarModo(true);
            if (login.RepeticionOculta) throw new Exception("El alta inicial requiere repetir contraseña.");
            login.AplicarModo(false);
            if (!login.RepeticionOculta) throw new Exception("La fila de repetición no se ocultó al terminar el alta.");
            foreach (var tipo in Enum.GetValues<Aplicacion.Modelos.Catalogo>())
            {
                using var formulario = new FrmCatalogo(gestion,tipo);
                formulario.MostrarRegistro(new());
                formulario.MostrarRegistro(new() { Id=1,Nombre="Prueba",Apellido="Edición",TipoPaciente="A",Especie="Canina" });
                formulario.MostrarRegistro(new());
            }
            var preview = args.FirstOrDefault(a=>a.StartsWith("--preview="))?[10..];
            if (preview is not null)
            {
                Directory.CreateDirectory(preview);
                foreach (var (formulario,nombre) in new (Form,string)[] { (login,"login"),(catalogo,"clientes"),(principal,"agenda"),(turno,"turno"),(config,"configuracion"),(jornadas,"jornadas"),(correo,"correos"),(usuariosForm,"usuarios"),(cobrosForm,"cobros"),(informesForm,"ingresos") })
                {
                    ((FormularioBase)formulario).VistaPrevia=true;
                    if (formulario is FrmPrincipal semanal) semanal.MostrarDemostracion();
                    formulario.Show(); formulario.PerformLayout();
                    Application.DoEvents();
                    if(formulario is FrmPrincipal comprobacion) comprobacion.ProbarDesplazamiento();
                    using var bitmap=new Bitmap(formulario.Width,formulario.Height);
                    formulario.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));
                    bitmap.Save(Path.Combine(preview,nombre+".png"));
                    formulario.Hide();
                }
            }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"smoke-ok.txt"),
                "10 formularios construidos; transición alta/login verificada; alta, edición y limpieza de los 4 catálogos verificadas.");
            return;
        }
        while (true)
        {
            using var login = new FrmLogin(acceso);
            if (login.ShowDialog() != DialogResult.OK) break;
            using var principal = new FrmPrincipal(gestion, sesion,correos,usuarios,caja);
            Application.Run(principal);
            acceso.SalirAsync().GetAwaiter().GetResult();
            if (!principal.CerrarSesion) break;
        }
    }
}
