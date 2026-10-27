using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Aplicacion.Servicios;

namespace SistemaTurnos.WinForms.Formularios;

public sealed class FrmConfiguracion : FormularioBase
{
    public FrmConfiguracion(IGestionServicio gestion)
    {
        Presentacion("Tu negocio","Personalizá el rubro, las etiquetas y los horarios de la aplicación.");
        Size=new Size(820,800);
        var nombre=new TextBox { MaxLength=150 };
        var rubro=Combo(); rubro.Items.AddRange(["GENERAL","ODONTOLOGIA","PELUQUERIA","VETERINARIA"]);
        var cliente=new TextBox { MaxLength=40 }; var paciente=new TextBox { MaxLength=40 };
        var usa=new CheckBox { Text="Solicitar paciente al reservar", AutoSize=true };
        var zona=Combo();
        var zonas=TimeZoneInfo.GetSystemTimeZones().Select(x=>x.Id).ToList(); zona.DataSource=zonas;
        var moneda=new TextBox { MaxLength=3, CharacterCasing=CharacterCasing.Upper };
        Campo("Nombre del negocio",nombre); Campo("Rubro",rubro); Campo("Etiqueta de cliente",cliente);
        Campo("Etiqueta de paciente",paciente); Campo("",usa); Campo("Zona horaria",zona); Campo("Moneda",moneda);
        var guardar=Boton("Guardar configuración"); Campo("",guardar);
        bool cargando=true;
        rubro.SelectedIndexChanged += (_,_) =>
        {
            if (cargando) return;
            cliente.Text=rubro.Text=="VETERINARIA"?"Responsable":"Cliente";
            paciente.Text=rubro.Text=="VETERINARIA"?"Mascota":"Paciente";
            usa.Checked=rubro.Text is "VETERINARIA" or "ODONTOLOGIA";
        };
        Shown += async (_,_) => await EjecutarAsync(async () =>
        {
            var c=await gestion.ConfiguracionAsync();
            nombre.Text=c.Nombre; rubro.SelectedItem=c.Rubro; cliente.Text=c.EtiquetaCliente;
            paciente.Text=c.EtiquetaPaciente; usa.Checked=c.UsaPacientes; zona.SelectedItem=c.ZonaHoraria; moneda.Text=c.Moneda;
            cargando=false;
        });
        guardar.Click += async (_,_) =>
        {
            bool guardado=false;
            await EjecutarAsync(async () =>
            {
                await gestion.GuardarConfiguracionAsync(new Configuracion(nombre.Text.Trim(),rubro.Text,
                    cliente.Text.Trim(),paciente.Text.Trim(),usa.Checked,zona.Text,moneda.Text));
                guardado=true;
            });
            if (guardado) { DialogResult=DialogResult.OK; Close(); }
        };
    }
}

