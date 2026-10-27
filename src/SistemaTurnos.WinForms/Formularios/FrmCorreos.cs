using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Aplicacion.Servicios;

namespace SistemaTurnos.WinForms.Formularios;

public sealed class FrmCorreos : FormularioBase
{
    public FrmCorreos(ICorreoServicio correo)
    {
        Presentacion("Confirmaciones por Gmail","Usá una contraseña de aplicación de Google. Los avisos se reintentan con la aplicación abierta."); Size=new Size(1100,1000);
        var habilitado=new CheckBox { Text="Enviar confirmaciones automáticamente",AutoSize=true };
        var servidor=new TextBox { Text="smtp.gmail.com" }; var puerto=new NumericUpDown { Minimum=1,Maximum=65535,Value=587 };
        var remitente=new TextBox(); var nombre=new TextBox(); var usuario=new TextBox();
        var password=new TextBox { UseSystemPasswordChar=true,PlaceholderText="Vacío conserva la contraseña guardada" };
        var grilla=new DataGridView { Height=190,ReadOnly=true,AllowUserToAddRows=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false };
        Campo("",habilitado); Campo("Servidor SMTP",servidor); Campo("Puerto STARTTLS",puerto); Campo("Remitente (email)",remitente);
        Campo("Nombre visible",nombre); Campo("Usuario SMTP",usuario); Campo("Contraseña de aplicación",password);
        var guardar=Boton("Guardar configuración"); Campo("",guardar); Campo("Últimos 200 avisos",grilla);
        var acciones=new FlowLayoutPanel { AutoSize=true }; var actualizar=Boton("Actualizar"); var enviar=Boton("Procesar pendientes"); var reintentar=Boton("Reintentar seleccionado");
        acciones.Controls.AddRange([actualizar,enviar,reintentar]); Campo("",acciones); bool tienePassword=false;
        async Task Actualizar()=>grilla.DataSource=(await correo.ListarAsync()).ToList();
        Shown+=async (_,_)=>await EjecutarAsync(async ()=>
        {
            var c=await correo.ConfiguracionAsync(); habilitado.Checked=c.Habilitado; servidor.Text=c.Servidor; puerto.Value=c.Puerto;
            remitente.Text=c.Remitente; nombre.Text=c.NombreRemitente; usuario.Text=c.Usuario; tienePassword=c.TienePassword; await Actualizar();
        });
        guardar.Click+=async (_,_)=>await EjecutarAsync(async ()=>
        {
            await correo.GuardarAsync(new(habilitado.Checked,servidor.Text.Trim(),(int)puerto.Value,remitente.Text.Trim(),nombre.Text.Trim(),usuario.Text.Trim(),password.Text,tienePassword));
            tienePassword=(await correo.ConfiguracionAsync()).TienePassword; password.Clear(); MessageBox.Show(this,"Configuración guardada para tu cuenta de Windows.","Correo");
        });
        actualizar.Click+=async (_,_)=>await EjecutarAsync(Actualizar);
        enviar.Click+=async (_,_)=>await EjecutarAsync(async ()=>
        { int enviados=await correo.ProcesarAsync(); await Actualizar(); MessageBox.Show(this,$"{enviados} avisos aceptados por el servidor SMTP.","Correo"); });
        reintentar.Click+=async (_,_)=>await EjecutarAsync(async ()=>
        { if (grilla.SelectedRows.Count>0 && grilla.SelectedRows[0].DataBoundItem is CorreoEstado c) { await correo.ReintentarAsync(c.Id); await Actualizar(); } });
    }
}

