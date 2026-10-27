using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Aplicacion.Servicios;
using SistemaTurnos.WinForms.Controles;

namespace SistemaTurnos.WinForms.Formularios;

public sealed class FrmPrincipal : FormularioBase
{
    private readonly IGestionServicio gestion;
    private readonly ICorreoServicio correo;
    private readonly TabControl vistas=new() { Dock=DockStyle.Fill,Padding=new Point(18,8) };
    private readonly TabPage agendaTab=new("Agenda semanal") { BackColor=Color.White,Padding=new Padding(12) };
    private readonly ComboBox profesional=Combo();
    private readonly DateTimePicker fecha=new() { Format=DateTimePickerFormat.Short,Width=135 };
    private readonly CheckBox cancelados=new() { Text="Mostrar cancelados",AutoSize=true,Margin=new Padding(10,10,5,5) };
    private readonly CalendarioSemanal calendario=new() { Dock=DockStyle.Fill };
    private readonly Label periodo=new() { AutoSize=true,Font=new Font("Segoe UI",12,FontStyle.Bold),Margin=new Padding(4,10,0,8) };
    private readonly Label detalle=new() { Dock=DockStyle.Fill,BackColor=Color.FromArgb(237,243,252),Padding=new Padding(14),Text="Seleccioná un turno para ver sus datos. Doble clic en un espacio libre para iniciar una reserva." };
    private readonly Label estadoCorreo=new() { Dock=DockStyle.Bottom,Height=48,Padding=new Padding(12,6,12,4),ForeColor=Color.FromArgb(199,214,237),AutoEllipsis=true };
    private readonly Button cerrarVista=Boton("Cerrar pestaña");
    private readonly Dictionary<string,(TabPage Pagina,Form Formulario)> abiertas=[];
    private readonly System.Windows.Forms.Timer reloj=new() { Interval=30000 };
    private readonly CancellationTokenSource cancelacion=new();
    private bool vinculando=true,cerrando;
    public bool CerrarSesion { get; private set; }

    private readonly Sesion sesion;
    public FrmPrincipal(IGestionServicio gestion,Sesion sesion,ICorreoServicio correo,IUsuariosServicio usuarios,ICajaServicio caja)
    {
        this.gestion=gestion; this.correo=correo; this.sesion=sesion;
        var verificadorSesion=new System.Windows.Forms.Timer {Interval=15000};
        bool verificandoSesion=false;
        verificadorSesion.Tick+=async (_,_)=>
        {
            if(VistaPrevia||cerrando||verificandoSesion) return;
            verificandoSesion=true;
            try { await sesion.ExigirAccesoAsync(actividad:false); }
            catch(OperacionException)
            {
                if(!sesion.UsuarioId.HasValue&&!EnOperacion&&!abiertas.Values.Any(x=>x.Formulario is FormularioBase f&&f.EnOperacion))
                { CerrarSesion=true; Close(); }
            }
            catch(Exception ex) { DiagnosticoSeguro.Registrar(ex); }
            finally { verificandoSesion=false; }
        };
        Shown+=(_,_)=> { if(!VistaPrevia) verificadorSesion.Start(); };
        FormClosed+=(_,_)=> { verificadorSesion.Stop(); verificadorSesion.Dispose(); };
        Presentacion("Tu espacio de trabajo","Agenda semanal por profesional · Atención de personas, clientes y mascotas.");
        Size=new Size(1480,960); MinimumSize=new Size(1200,760);
        var raiz=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Color.FromArgb(240,244,249) };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,214)); raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var lateral=new Panel { Dock=DockStyle.Fill,BackColor=Color.FromArgb(25,43,70) };
        var menu=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(12,8,8,12) };
        lateral.Controls.Add(menu); lateral.Controls.Add(estadoCorreo);
        void Grupo(string texto)=>menu.Controls.Add(new Label { Text=texto,Width=180,Height=29,ForeColor=Color.FromArgb(169,190,220),Font=new Font(Font.FontFamily,8.5f,FontStyle.Bold),Padding=new Padding(4,10,0,0) });
        void Item(string texto,Action accion)
        {
            var boton=Boton(texto); boton.Width=182; boton.Height=43; boton.AutoSize=false;
            boton.TextAlign=ContentAlignment.MiddleLeft; boton.BackColor=Color.FromArgb(38,59,90);
            boton.FlatAppearance.MouseOverBackColor=Color.FromArgb(51,84,131); boton.Click+=(_,_)=>accion(); menu.Controls.Add(boton);
        }
        Grupo("AGENDA");
        Item("Agenda semanal",()=>vistas.SelectedTab=agendaTab);
        if(sesion.Puede(Permiso.Reservar)) Item("+  Reservar turno",()=>Reservar());
        if(sesion.Puede(Permiso.GestionarCatalogos)) Item("Horarios de atención",()=>Abrir("jornadas","Horarios",()=>new FrmJornadas(gestion)));
        Grupo("PERSONAS Y SERVICIOS");
        foreach (var tipo in Enum.GetValues<Catalogo>())
            if(sesion.Puede(tipo is Catalogo.Clientes or Catalogo.Pacientes?Permiso.GestionarPersonas:Permiso.GestionarCatalogos))
                Item(tipo.ToString(),()=>Abrir(tipo.ToString(),tipo.ToString(),()=>new FrmCatalogo(gestion,tipo)));
        if(sesion.Puede(Permiso.VerCobros))
        { Grupo("CAJA"); Item("Cobros y saldos",()=>Abrir("cobros","Cobros",()=>new FrmCobros(caja,gestion,sesion))); }
        if(sesion.Puede(Permiso.VerInformes)) Item("Ingresos e informes",()=>Abrir("informes","Ingresos",()=>new FrmInformes(caja)));
        Grupo("NEGOCIO");
        if(sesion.Puede(Permiso.Configurar))
        {
            Item("Correos",()=>Abrir("correos","Correos",()=>new FrmCorreos(correo)));
            Item("Configuración",()=>Abrir("config","Configuración",()=>new FrmConfiguracion(gestion)));
        }
        if(sesion.Puede(Permiso.AdministrarUsuarios)) Item("Usuarios y roles",()=>Abrir("usuarios","Usuarios",()=>new FrmUsuarios(usuarios,gestion)));
        Grupo("SESIÓN · "+sesion.Rol);
        Item("Cerrar sesión",()=> { CerrarSesion=true; Close(); });
        var espacio=new Panel { Dock=DockStyle.Fill,Padding=new Padding(16,12,16,10) };
        var herramientas=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft };
        herramientas.Controls.Add(cerrarVista); cerrarVista.Enabled=false;
        cerrarVista.Click+=(_,_)=>
        { var abierto=abiertas.Values.FirstOrDefault(x=>x.Pagina==vistas.SelectedTab); abierto.Formulario?.Close(); };
        espacio.Controls.Add(vistas); espacio.Controls.Add(herramientas);
        raiz.Controls.Add(lateral,0,0); raiz.Controls.Add(espacio,1,0); MontarVista(raiz);

        var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,104));
        var filtros=new FlowLayoutPanel { AutoSize=true,Dock=DockStyle.Top,WrapContents=true };
        profesional.Width=230; profesional.Margin=new Padding(6,8,12,5);
        var anterior=Boton("‹ Anterior"); var siguiente=Boton("Siguiente ›"); var hoy=Boton("Hoy"); var actualizar=Boton("Actualizar");
        filtros.Controls.AddRange([new Label { Text="Profesional",AutoSize=true,Margin=new Padding(0,12,0,0) },profesional,fecha,anterior,hoy,siguiente,actualizar,cancelados]);
        layout.Controls.Add(filtros,0,0); layout.Controls.Add(periodo,0,1); layout.Controls.Add(calendario,0,2); layout.Controls.Add(detalle,0,3);
        agendaTab.Controls.Add(layout); vistas.TabPages.Add(agendaTab);
        profesional.SelectedIndexChanged+=async (_,_)=> { if (!vinculando) await EjecutarAsync(()=>ActualizarAsync(false)); };
        fecha.ValueChanged+=async (_,_)=> { if (!vinculando) await EjecutarAsync(()=>ActualizarAsync(false)); };
        cancelados.CheckedChanged+=async (_,_)=> { if (!vinculando) await EjecutarAsync(()=>ActualizarAsync(false)); };
        anterior.Click+=(_,_)=>fecha.Value=fecha.Value.AddDays(-7);
        siguiente.Click+=(_,_)=>fecha.Value=fecha.Value.AddDays(7);
        hoy.Click+=async (_,_)=>await EjecutarAsync(async ()=>
        {
            var c=await gestion.ConfiguracionAsync(); vinculando=true;
            fecha.Value=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(c.ZonaHoraria)).Date;
            vinculando=false; await ActualizarAsync(false);
        });
        actualizar.Click+=async (_,_)=>await EjecutarAsync(()=>ActualizarAsync(true));
        calendario.TurnoSeleccionado+=t=>
        {
            detalle.Text=$"Reserva #{t.Id} · {t.Estado}\nCliente / responsable: {t.Cliente}"
                +(string.IsNullOrWhiteSpace(t.Paciente)?"":$"   |   Paciente: {t.Paciente}")
                +$"\nProfesional: {t.Profesional}   |   Servicio: {t.Servicio}   |   Duración: {(t.FinUtc-t.InicioUtc).TotalMinutes:0} minutos";
        };
        calendario.HorarioSolicitado+=hora=> { if(sesion.Puede(Permiso.Reservar)) Reservar(hora); };
        vistas.SelectedIndexChanged+=async (_,_)=>
        { cerrarVista.Enabled=vistas.SelectedTab!=agendaTab; if (!vinculando && vistas.SelectedTab==agendaTab) await EjecutarAsync(()=>ActualizarAsync(true)); };
        Shown+=async (_,_)=>await EjecutarAsync(async ()=>
        {
            var c=await gestion.ConfiguracionAsync();
            fecha.Value=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(c.ZonaHoraria)).Date;
            await ActualizarAsync(true); reloj.Start(); await ProcesarCorreosAsync();
        });
        reloj.Tick+=async (_,_)=>await ProcesarCorreosAsync();
        FormClosing+=(_,e)=>
        {
            if (abiertas.Values.Any(x=>x.Formulario is FormularioBase b && b.EnOperacion))
            { e.Cancel=true; return; }
            if (!e.Cancel) cerrando=true;
        };
        FormClosed+=(_,_)=> { reloj.Stop(); reloj.Dispose(); cancelacion.Cancel(); };
        KeyPreview=true;
        KeyDown+=async (_,e)=>
        {
            if (e.KeyCode==Keys.F5) { e.Handled=true; await EjecutarAsync(()=>ActualizarAsync(true)); }
            if (e.Control && e.KeyCode==Keys.N) { e.Handled=true; Reservar(); }
        };
    }

    private void Reservar(DateTime? inicio=null)
    {
        if(!sesion.Puede(Permiso.Reservar) && !VistaPrevia) return;
        int? id=(profesional.SelectedItem as RegistroCatalogo)?.Id;
        Abrir("reserva","Nuevo turno",()=>new FrmTurno(gestion,id,inicio??fecha.Value.Date));
    }
    private void Abrir(string clave,string nombre,Func<Form> crear)
    {
        if (abiertas.TryGetValue(clave,out var existente)) { vistas.SelectedTab=existente.Pagina; return; }
        var formulario=crear(); formulario.TopLevel=false; formulario.FormBorderStyle=FormBorderStyle.None;
        if (formulario is FormularioBase baseFormulario) baseFormulario.VistaPrevia=VistaPrevia;
        formulario.MinimumSize=Size.Empty; formulario.Dock=DockStyle.Fill;
        var pagina=new TabPage(nombre) { BackColor=Color.White };
        pagina.Controls.Add(formulario); abiertas.Add(clave,(pagina,formulario));
        formulario.FormClosed+=async (_,_)=>
        {
            abiertas.Remove(clave); vistas.TabPages.Remove(pagina); pagina.Dispose();
            if (!cerrando && !IsDisposed && !Disposing)
            { vistas.SelectedTab=agendaTab; await EjecutarAsync(()=>ActualizarAsync(true)); await ProcesarCorreosAsync(); }
        };
        vistas.TabPages.Add(pagina); vistas.SelectedTab=pagina; formulario.Show();
    }
    private async Task ActualizarAsync(bool recargarProfesionales)
    {
        var c=await gestion.ConfiguracionAsync(); Presentacion(c.Nombre+(sesion.ModoRemoto?" · Servidor HTTPS":" · Local de desarrollo"),"Agenda semanal por profesional · Reservas, personas y servicios en un mismo lugar.");
        if (recargarProfesionales)
        {
            int? id=(profesional.SelectedItem as RegistroCatalogo)?.Id; vinculando=true;
            try
            {
                Opciones(profesional,(await gestion.ListarAsync(Catalogo.Profesionales)).Where(x=>x.Activo));
                if (id.HasValue && ((List<RegistroCatalogo>)profesional.DataSource!).Any(x=>x.Id==id)) profesional.SelectedValue=id.Value;
            }
            finally { vinculando=false; }
        }
        int profesionalId=(profesional.SelectedItem as RegistroCatalogo)?.Id??0;
        DateTime lunes=SemanaAgenda.Lunes(fecha.Value);
        var turnos=await gestion.AgendaSemanalAsync(profesionalId,fecha.Value);
        var visibles=turnos.Where(t=>cancelados.Checked||t.CodigoEstado!="CANCELADO").ToList();
        var jornadas=profesionalId>0?await gestion.JornadasAsync(profesionalId):[];
        calendario.Cargar(lunes,TimeZoneInfo.FindSystemTimeZoneById(c.ZonaHoraria),visibles,jornadas);
        periodo.Text=$"{lunes:dd/MM} — {lunes.AddDays(6):dd/MM/yyyy}   ·   {visibles.Count} turnos   ·   {c.ZonaHoraria}";
        detalle.Text=profesionalId==0?"Cargá un profesional para comenzar a organizar la agenda.":
            "Seleccioná un turno para ver el cliente, paciente y servicio. Doble clic en un espacio libre para iniciar una reserva.\nBlanco: jornada de atención · Gris claro: fuera de atención · Color: turno reservado.";
    }
    private async Task ProcesarCorreosAsync()
    {
        if (VistaPrevia||cerrando||!sesion.Puede(Permiso.ProcesarCorreos)) return;
        try { int n=await correo.ProcesarAsync(cancelacion.Token); if (!IsDisposed) estadoCorreo.Text=$"Correo · {n} enviados en la última revisión"; }
        catch (OperationCanceledException) { }
        catch (Exception) { if (!IsDisposed) estadoCorreo.Text="Correo · revisá la configuración"; }
    }
    internal void MostrarDemostracion()
    {
        var lunes=new DateTime(2026,10,26); var zona=TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");
        Opciones(profesional,[new RegistroCatalogo { Id=1,Nombre="Profesional de ejemplo" }]); fecha.Value=lunes;
        List<TurnoListado> datos=[];
        for (int d=0;d<5;d++)
            for (int h=9;h<=15;h+=3)
            {
                DateTime local=lunes.AddDays(d).AddHours(h); DateTime utc=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local,DateTimeKind.Unspecified),zona);
                datos.Add(new(datos.Count+1,utc,utc.AddMinutes(50),$"Cliente de ejemplo {datos.Count+1}","","Profesional de ejemplo","Sesión individual",h==9?"Confirmado":"Pendiente",0,"ARS",1,h==9?"CONFIRMADO":"PENDIENTE"));
            }
        calendario.Cargar(lunes,zona,datos,Enumerable.Range(0,5).Select(d=>new Jornada(d,TimeSpan.FromHours(9),TimeSpan.FromHours(18),15)).ToList());
        periodo.Text="26/10 — 01/11/2026 · 15 turnos · Vista de demostración";
    }
    internal void ProbarNavegacion()
    {
        VistaPrevia=true;
        Abrir("clientes-prueba","Clientes",()=>new FrmCatalogo(gestion,Catalogo.Clientes));
        Abrir("clientes-prueba","Clientes",()=>throw new Exception("Se duplicó la pestaña."));
        if (abiertas.Count!=1 || vistas.TabCount!=2) throw new Exception("Navegación duplicada.");
        abiertas["clientes-prueba"].Formulario.Close();
        if (abiertas.Count!=0 || vistas.TabCount!=1) throw new Exception("La pestaña no se cerró.");
    }
    internal void ProbarDesplazamiento()=>calendario.ProbarDesplazamiento();
}

