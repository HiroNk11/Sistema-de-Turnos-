using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.WinForms.Formularios;

public class FormularioBase : Form
{
    protected readonly TableLayoutPanel Contenido = new()
    { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(26), BackColor=Color.White };
    private readonly Label cabecera = new() { Dock=DockStyle.Top, Height=42, ForeColor=Color.White,
        Font=new Font("Segoe UI",20,FontStyle.Bold), Padding=new Padding(26,8,0,0) };
    private readonly Label descripcion = new() { Dock=DockStyle.Fill, ForeColor=Color.FromArgb(198,214,237),
        Padding=new Padding(28,6,20,0) };
    private bool ocupado;
    protected Control? Superficie;
    internal bool EnOperacion => ocupado;
    internal bool VistaPrevia;
    protected override bool ShowWithoutActivation => VistaPrevia;
    protected FormularioBase()
    {
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(240,244,249);
        ForeColor = Color.FromArgb(34,49,70);
        AutoScaleMode=AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(520, 430);
        Size = new Size(650, 650);
        Contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        Contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var scroll=new Panel { Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(22) };
        scroll.Controls.Add(Contenido);
        var header=new Panel { Dock=DockStyle.Top,Height=108,BackColor=Color.FromArgb(25,43,70) };
        header.Controls.Add(descripcion); header.Controls.Add(cabecera);
        Controls.Add(scroll); Controls.Add(header);
        TextChanged += (_,_) => cabecera.Text=Text;
        descripcion.Text="Organizá tu atención, de forma simple.";
        FormClosing += (_, e) => { if (ocupado) e.Cancel = true; };
    }
    protected void Presentacion(string titulo,string detalle)
    { Text=titulo; cabecera.Text=titulo; descripcion.Text=detalle; }
    protected void MontarVista(Control vista)
    {
        var panel=(Panel)Contenido.Parent!;
        panel.Controls.Remove(Contenido); panel.Padding=Padding.Empty; panel.AutoScroll=false;
        vista.Dock=DockStyle.Fill; panel.Controls.Add(vista); Superficie=vista;
    }
    protected void VisibilidadCampo(Control control,bool visible)
    {
        int fila=Contenido.GetRow(control);
        foreach (Control item in Contenido.Controls)
            if (Contenido.GetRow(item)==fila) item.Visible=visible;
        Contenido.RowStyles[fila].SizeType=visible?SizeType.AutoSize:SizeType.Absolute;
        Contenido.RowStyles[fila].Height=0;
    }
    internal bool FilaOculta(Control control) => Contenido.RowStyles[Contenido.GetRow(control)].SizeType==SizeType.Absolute;
    protected void Campo(string etiqueta, Control control)
    {
        int fila = Contenido.RowCount++;
        Contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label { Text=etiqueta, AutoSize=true, ForeColor=Color.FromArgb(75,91,113),
            Font=new Font(Font,FontStyle.Bold), Margin=new Padding(0,11,12,14) };
        control.Dock = DockStyle.Top;
        control.Margin = new Padding(0,6,0,8);
        Contenido.Controls.Add(label,0,fila);
        Contenido.Controls.Add(control,1,fila);
        if (control is TextBox texto) { texto.BorderStyle=BorderStyle.FixedSingle; texto.MinimumSize=new Size(0,32); }
        if (control is ComboBox combo) { combo.FlatStyle=FlatStyle.Flat; combo.BackColor=Color.FromArgb(241,245,250); }
        if (control is DataGridView grid)
        {
            grid.BorderStyle=BorderStyle.None; grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor=Color.FromArgb(230,236,243); grid.EnableHeadersVisualStyles=false;
            grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(230,237,246);
            grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.FromArgb(36,54,80);
            grid.ColumnHeadersDefaultCellStyle.Font=new Font(Font,FontStyle.Bold);
            grid.ColumnHeadersHeight=38; grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(218,234,255);
            grid.DefaultCellStyle.SelectionForeColor=Color.FromArgb(25,43,70);
            grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(247,249,252);
            grid.RowTemplate.Height=34;
            // La tabla utiliza todo el ancho de la tarjeta.
            Contenido.Controls.Remove(control);
            int filaTabla=Contenido.RowCount++;
            Contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Contenido.Controls.Add(control,0,filaTabla); Contenido.SetColumnSpan(control,2);
        }
        else if (string.IsNullOrEmpty(etiqueta))
        { Contenido.Controls.Remove(label); Contenido.SetColumn(control,0); Contenido.SetColumnSpan(control,2); }
    }
    protected static Button Boton(string texto) => new()
    { Text=texto, AutoSize=true, MinimumSize=new Size(115,40), Margin=new Padding(0,5,10,5),
      Padding=new Padding(14,7,14,7), BackColor=Color.FromArgb(36,99,206),ForeColor=Color.White,
      FlatStyle=FlatStyle.Flat, Cursor=Cursors.Hand, FlatAppearance={BorderSize=0} };
    protected static ComboBox Combo() => new() { DropDownStyle=ComboBoxStyle.DropDownList };
    protected static void Opciones(ComboBox combo, IEnumerable<RegistroCatalogo> datos)
    { combo.DisplayMember=nameof(RegistroCatalogo.Texto); combo.ValueMember=nameof(RegistroCatalogo.Id); combo.DataSource=datos.ToList(); }
    protected async Task EjecutarAsync(Func<Task> accion)
    {
        if (ocupado || VistaPrevia) return;
        ocupado = true; (Superficie??Contenido).Enabled=false; UseWaitCursor=true;
        try { await accion(); }
        catch (OperacionException ex) { MessageBox.Show(this,ex.Message,"Revisá los datos",MessageBoxButtons.OK,MessageBoxIcon.Information); }
        catch (Exception ex)
        {
            string id=DiagnosticoSeguro.Registrar(ex);
            MessageBox.Show(this,$"No se pudo completar la operación. Código de diagnóstico: {id}. Revisá la conexión y las migraciones SQL. El registro técnico está en %LOCALAPPDATA%\\SistemaTurnos\\errores-seguros.log.",
                "Error",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
        finally { ocupado=false; (Superficie??Contenido).Enabled=true; UseWaitCursor=false; }
    }
}

