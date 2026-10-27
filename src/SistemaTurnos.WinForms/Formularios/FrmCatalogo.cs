using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Aplicacion.Servicios;

namespace SistemaTurnos.WinForms.Formularios;

public sealed class FrmCatalogo : FormularioBase
{
    private readonly DataGridView grilla=new()
    {
        ReadOnly=true, AllowUserToAddRows=false, AllowUserToDeleteRows=false, AutoGenerateColumns=false,
        MultiSelect=false, SelectionMode=DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill, Height=180, RowHeadersVisible=false,
        BackgroundColor=Color.White
    };
    private readonly TextBox nombre=new() { MaxLength=100 }, apellido=new() { MaxLength=100 },
        telefono=new() { MaxLength=40 }, email=new() { MaxLength=254 }, especie=new() { MaxLength=60 };
    private readonly CheckBox activo=new() { Text="Activo", Checked=true, AutoSize=true };
    private readonly ComboBox responsable=Combo(), tipoPaciente=Combo();
    private readonly NumericUpDown duracion=new() { Minimum=1, Maximum=1440, Value=30 },
        precio=new() { Maximum=9999999999999999.99m, DecimalPlaces=2, ThousandsSeparator=true };
    private readonly CheckedListBox servicios=new() { Height=110, CheckOnClick=true, DisplayMember=nameof(RegistroCatalogo.Texto) };
    private RegistroCatalogo actual=new();
    private bool vinculando;
    private readonly Catalogo catalogo;
    internal void MostrarRegistro(RegistroCatalogo r)
    {
        actual=r; nombre.Text=r.Nombre; apellido.Text=r.Apellido; telefono.Text=r.Telefono; email.Text=r.Email;
        activo.Checked=r.Activo; duracion.Value=Math.Clamp(r.DuracionMinutos,1,1440); precio.Value=Math.Clamp(r.Precio,0,precio.Maximum);
        if (catalogo == Catalogo.Pacientes)
        {
            especie.Text=r.Especie; tipoPaciente.SelectedIndex=r.TipoPaciente=="A"?1:0;
            responsable.SelectedIndex=-1;
            if (r.ResponsableId.HasValue) responsable.SelectedValue=r.ResponsableId.Value;
        }
        for (int i=0;i<servicios.Items.Count;i++)
            servicios.SetItemChecked(i,r.ServiciosIds.Contains(((RegistroCatalogo)servicios.Items[i]).Id));
    }
    public FrmCatalogo(IGestionServicio gestion, Catalogo tipo)
    {
        catalogo=tipo;
        Presentacion(tipo.ToString(),"Seleccioná un registro para editarlo o elegí Nuevo para agregar uno.");
        Size=new Size(960,860); MinimumSize=new Size(760,650);
        foreach (string propiedad in new[] { "Id","Nombre","Apellido","Activo" })
            if (propiedad != "Apellido" || tipo != Catalogo.Servicios)
                grilla.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName=propiedad, HeaderText=propiedad });
        Campo("Registros",grilla); Campo("Nombre",nombre);
        if (tipo != Catalogo.Servicios) Campo("Apellido",apellido);
        if (tipo is Catalogo.Clientes or Catalogo.Profesionales)
        { Campo("Teléfono",telefono); Campo("Correo",email); }
        if (tipo == Catalogo.Pacientes) Campo("Correo del paciente",email);
        if (tipo == Catalogo.Servicios) { Campo("Duración (minutos)",duracion); Campo("Precio base",precio); }
        if (tipo == Catalogo.Profesionales) Campo("Servicios que ofrece",servicios);
        if (tipo == Catalogo.Pacientes)
        {
            Campo("Responsable",responsable);
            tipoPaciente.Items.AddRange(["Persona","Animal"]); tipoPaciente.SelectedIndex=0;
            Campo("Tipo",tipoPaciente); Campo("Especie (animal)",especie);
            especie.Enabled=false;
            tipoPaciente.SelectedIndexChanged += (_,_) => especie.Enabled=tipoPaciente.SelectedIndex==1;
        }
        Campo("Estado",activo);
        var botones=new FlowLayoutPanel { AutoSize=true };
        var nuevo=Boton("Nuevo"); var guardar=Boton("Guardar"); var recargar=Boton("Actualizar");
        botones.Controls.AddRange([nuevo,guardar,recargar]); Campo("",botones);
        async Task Cargar()
        {
            vinculando=true;
            try
            {
                if (tipo == Catalogo.Pacientes) Opciones(responsable,await gestion.ListarAsync(Catalogo.Clientes));
                if (tipo == Catalogo.Profesionales)
                { servicios.Items.Clear(); foreach (var s in await gestion.ListarAsync(Catalogo.Servicios)) servicios.Items.Add(s); }
                grilla.DataSource=(await gestion.ListarAsync(tipo)).ToList();
                grilla.ClearSelection(); MostrarRegistro(new());
            }
            finally { vinculando=false; }
        }
        grilla.SelectionChanged += (_,_) =>
        {
            if (!vinculando && grilla.SelectedRows.Count>0
                && grilla.SelectedRows[0].DataBoundItem is RegistroCatalogo r) MostrarRegistro(r);
        };
        nuevo.Click += (_,_) => { grilla.ClearSelection(); MostrarRegistro(new()); nombre.Focus(); };
        recargar.Click += async (_,_) => await EjecutarAsync(Cargar);
        Shown += async (_,_) => await EjecutarAsync(Cargar);
        guardar.Click += async (_,_) => await EjecutarAsync(async () =>
        {
            await gestion.GuardarAsync(tipo, actual with
            {
                Nombre=nombre.Text, Apellido=apellido.Text, Telefono=telefono.Text, Email=email.Text,
                Activo=activo.Checked, DuracionMinutos=(int)duracion.Value, Precio=precio.Value,
                ResponsableId=(responsable.SelectedItem as RegistroCatalogo)?.Id,
                TipoPaciente=tipoPaciente.SelectedIndex==1?"A":"P", Especie=especie.Text,
                ServiciosIds=servicios.CheckedItems.Cast<RegistroCatalogo>().Select(x=>x.Id).ToList()
            });
            await Cargar();
            MessageBox.Show(this,"Registro guardado.","Gestión");
        });
    }
}

