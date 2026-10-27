using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Aplicacion.Servicios;

namespace SistemaTurnos.WinForms.Formularios;

public sealed class FrmUsuarios : FormularioBase
{
    public FrmUsuarios(IUsuariosServicio usuarios,IGestionServicio gestion)
    {
        Presentacion("Usuarios y permisos","Los cambios de permisos o contraseña invalidan las sesiones abiertas de esa cuenta."); Size=new Size(980,900);
        var grid=new DataGridView { ReadOnly=true,AutoGenerateColumns=false,AllowUserToAddRows=false,Height=200,MultiSelect=false,
            SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false };
        foreach(string p in new[]{"Id","Nombre","Rol","Activo","ProfesionalId"}) grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName=p,HeaderText=p });
        var nombre=new TextBox { MaxLength=50 }; var rol=Combo(); rol.DataSource=Enum.GetValues<RolUsuario>();
        var profesional=Combo(); var password=new TextBox { UseSystemPasswordChar=true,MaxLength=128,PlaceholderText="Vacío conserva la contraseña actual" };
        rol.SelectedItem=RolUsuario.Recepcion; profesional.Enabled=false;
        var activo=new CheckBox { Text="Cuenta activa",AutoSize=true,Checked=true };
        Campo("Cuentas",grid); Campo("Usuario",nombre); Campo("Rol",rol); Campo("Profesional vinculado",profesional);
        Campo("Nueva contraseña",password); Campo("",activo);
        var botones=new FlowLayoutPanel { AutoSize=true }; var nuevo=Boton("Nuevo"); var guardar=Boton("Guardar usuario"); var actualizar=Boton("Actualizar");
        botones.Controls.AddRange([nuevo,guardar,actualizar]); Campo("",botones);
        UsuarioGestion registro=new(0,"",RolUsuario.Recepcion,true,null,[]); bool vinculando=false;
        void Mostrar(UsuarioGestion u)
        { registro=u; nombre.Text=u.Nombre; rol.SelectedItem=u.Rol; activo.Checked=u.Activo; password.Clear(); profesional.SelectedValue=u.ProfesionalId??0; }
        async Task Cargar()
        {
            vinculando=true;
            try
            {
                Opciones(profesional,new[] {new RegistroCatalogo { Id=0,Nombre="Sin vínculo" }}.Concat(await gestion.ListarAsync(Catalogo.Profesionales)));
                grid.DataSource=(await usuarios.ListarAsync()).ToList(); grid.ClearSelection(); Mostrar(new(0,"",RolUsuario.Recepcion,true,null,[]));
            }
            finally { vinculando=false; }
        }
        rol.SelectedIndexChanged+=(_,_)=>profesional.Enabled=rol.SelectedItem is RolUsuario.Profesional;
        grid.SelectionChanged+=(_,_)=> { if(!vinculando&&grid.SelectedRows.Count>0&&grid.SelectedRows[0].DataBoundItem is UsuarioGestion u) Mostrar(u); };
        nuevo.Click+=(_,_)=> { grid.ClearSelection(); Mostrar(new(0,"",RolUsuario.Recepcion,true,null,[])); nombre.Focus(); };
        guardar.Click+=async (_,_)=>await EjecutarAsync(async ()=>
        {
            var r=(RolUsuario)rol.SelectedItem!; int? p=(profesional.SelectedItem as RegistroCatalogo)?.Id;
            await usuarios.GuardarAsync(registro with { Nombre=nombre.Text,Rol=r,Activo=activo.Checked,ProfesionalId=p>0?p:null },password.Text);
            MessageBox.Show(this,"Usuario guardado. Si cambiaste sus permisos o contraseña, deberá volver a ingresar.","Usuarios");
            if(!usuarios.EsCuentaActual(registro.Id)) await Cargar();
        });
        actualizar.Click+=async (_,_)=>await EjecutarAsync(Cargar); Shown+=async (_,_)=>await EjecutarAsync(Cargar);
    }
}

