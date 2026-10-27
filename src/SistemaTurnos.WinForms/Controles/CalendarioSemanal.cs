using SistemaTurnos.Aplicacion.Modelos;

namespace SistemaTurnos.WinForms.Controles;

// Control de presentación: no consulta repositorios ni contiene reglas de reserva.
public sealed class CalendarioSemanal : ScrollableControl
{
    private DateTime lunes;
    private TimeZoneInfo zona=TimeZoneInfo.Utc;
    private IReadOnlyList<TurnoListado> turnos=[];
    private IReadOnlyList<Jornada> jornadas=[];
    private readonly List<(Rectangle Area,TurnoListado Turno)> tarjetas=[];
    private readonly ToolTip ayuda=new();
    private long? seleccionado;
    private int primera=8,ultima=20;
    private const int Eje=64,Cabecera=52;
    private float escala=1.5f;
    public event Action<TurnoListado>? TurnoSeleccionado;
    public event Action<DateTime>? HorarioSolicitado;
    public CalendarioSemanal()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        DoubleBuffered=true; AutoScroll=true; BackColor=Color.White;
        Resize+=(_,_)=> { Ajustar(); Invalidate(); };
        MouseMove+=(_,e)=>
        {
            var tarjeta=tarjetas.LastOrDefault(x=>x.Area.Contains(e.Location));
            string texto=tarjeta.Turno is null?"Doble clic en un espacio libre para iniciar una reserva."
                :$"{tarjeta.Turno.Cliente}\n{tarjeta.Turno.Paciente}\n{tarjeta.Turno.Servicio}\n{tarjeta.Turno.Estado}";
            if (ayuda.GetToolTip(this)!=texto) ayuda.SetToolTip(this,texto);
        };
    }
    public void Cargar(DateTime inicio,TimeZoneInfo zonaNegocio,IReadOnlyList<TurnoListado> datos,IReadOnlyList<Jornada> atencion)
    {
        lunes=inicio.Date; zona=zonaNegocio; turnos=datos; jornadas=atencion; seleccionado=null;
        var horas=datos.SelectMany(t=>new[] { TimeZoneInfo.ConvertTimeFromUtc(t.InicioUtc,zona).Hour,
            Math.Min(24,TimeZoneInfo.ConvertTimeFromUtc(t.FinUtc,zona).Hour+1) }).Concat(atencion.SelectMany(j=>new[] { j.Desde.Hours,j.Hasta.Hours+1 })).ToList();
        primera=Math.Max(0,Math.Min(8,horas.DefaultIfEmpty(8).Min()));
        ultima=Math.Min(24,Math.Max(19,horas.DefaultIfEmpty(19).Max()));
        AutoScrollPosition=Point.Empty; Ajustar(); Invalidate();
    }
    private void Ajustar()
    { escala=1.5f*DeviceDpi/96f; AutoScrollMinSize=new Size(0,Cabecera+(int)((ultima-primera)*60*escala)+20); }
    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se); tarjetas.Clear(); ayuda.RemoveAll();
        // El desplazamiento nativo mueve también los píxeles del encabezado fijo.
        // Invalidar todo evita restos y áreas de clic de la posición anterior.
        Invalidate();
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    { base.OnMouseWheel(e); tarjetas.Clear(); Invalidate(); }
    private static Color ColorEstado(string codigo)=>codigo switch
    { "CONFIRMADO"=>Color.FromArgb(30,125,100),"EN_ATENCION"=>Color.FromArgb(120,76,180),
      "CANCELADO"=>Color.FromArgb(115,125,140),"AUSENTE"=>Color.FromArgb(170,107,32),
      "FINALIZADO"=>Color.FromArgb(54,113,130),_=>Color.FromArgb(45,101,192) };
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); tarjetas.Clear(); var g=e.Graphics;
        g.Clear(BackColor);
        var estadoGrafico=g.Save();
        g.SetClip(new Rectangle(0,Cabecera,ClientSize.Width,Math.Max(0,ClientSize.Height-Cabecera)));
        float ancho=Math.Max(90,(ClientSize.Width-Eje-SystemInformation.VerticalScrollBarWidth)/7f);
        int scroll=AutoScrollPosition.Y;
        using var linea=new Pen(Color.FromArgb(227,233,242));
        using var media=new Pen(Color.FromArgb(242,245,249));
        using var texto=new SolidBrush(Color.FromArgb(71,88,111));
        using var pequena=new Font("Segoe UI",8.5f);
        using var fuerte=new Font("Segoe UI",9,FontStyle.Bold);
        for (int d=0;d<7;d++)
        {
            float x=Eje+d*ancho;
            using var fondo=new SolidBrush(lunes.AddDays(d).Date==TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,zona).Date
                ?Color.FromArgb(246,250,255):Color.FromArgb(249,250,252));
            g.FillRectangle(fondo,x,Cabecera,ancho,ClientSize.Height);
            foreach (var j in jornadas.Where(j=>j.DiaSemana==d))
            {
                float y=Cabecera+(float)(j.Desde.TotalMinutes-primera*60)*escala+scroll;
                g.FillRectangle(Brushes.White,x,y,ancho,(float)(j.Hasta-j.Desde).TotalMinutes*escala);
            }
            g.DrawLine(linea,x,Cabecera,x,ClientSize.Height);
        }
        for (int m=primera*60;m<=ultima*60;m+=30)
        {
            float y=Cabecera+(m-primera*60)*escala+scroll;
            if (y<Cabecera) continue;
            g.DrawLine(m%60==0?linea:media,Eje,y,ClientSize.Width,y);
            if (m%60==0) g.DrawString($"{m/60:00}:00",pequena,texto,7,y+2);
        }
        for (int d=0;d<7;d++)
        {
            DateTime dia=lunes.AddDays(d);
            var eventos=turnos.Select(t=>(Turno:t,Inicio:TimeZoneInfo.ConvertTimeFromUtc(t.InicioUtc,zona),Fin:TimeZoneInfo.ConvertTimeFromUtc(t.FinUtc,zona)))
                .Where(t=>t.Inicio<dia.AddDays(1)&&t.Fin>dia).OrderBy(t=>t.Inicio).ToList();
            var finales=new List<DateTime>(); var asignados=new List<(TurnoListado Turno,DateTime Inicio,DateTime Fin,int Columna)>();
            foreach (var evento in eventos)
            {
                int col=finales.FindIndex(f=>f<=evento.Inicio);
                if (col<0) { col=finales.Count; finales.Add(evento.Fin); } else finales[col]=evento.Fin;
                asignados.Add((evento.Turno,evento.Inicio,evento.Fin,col));
            }
            foreach (var item in asignados)
            {
                DateTime desde=item.Inicio<dia?dia:item.Inicio, hasta=item.Fin>dia.AddDays(1)?dia.AddDays(1):item.Fin;
                float w=ancho/Math.Max(1,finales.Count);
                var rect=new Rectangle((int)(Eje+d*ancho+item.Columna*w+3),
                    (int)(Cabecera+(desde-dia).TotalMinutes*escala-primera*60*escala+scroll),
                    (int)w-6,Math.Max(4,(int)((hasta-desde).TotalMinutes*escala)-3));
                if (rect.Bottom<Cabecera||rect.Top>ClientSize.Height) continue;
                using var color=new SolidBrush(ColorEstado(item.Turno.CodigoEstado)); g.FillRectangle(color,rect);
                if (seleccionado==item.Turno.Id) { using var borde=new Pen(Color.FromArgb(20,35,60),3); g.DrawRectangle(borde,rect); }
                var interior=Rectangle.Inflate(rect,-5,-3);
                if (rect.Height>=22)
                {
                    string persona=string.IsNullOrWhiteSpace(item.Turno.Paciente)?item.Turno.Cliente:item.Turno.Paciente;
                    string textoTarjeta=$"{item.Inicio:HH:mm}–{item.Fin:HH:mm}\n{persona}\n{item.Turno.Servicio}";
                    TextRenderer.DrawText(g,textoTarjeta,fuerte,interior,Color.White,
                        TextFormatFlags.Top|TextFormatFlags.Left|TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.PreserveGraphicsClipping);
                }
                tarjetas.Add((rect,item.Turno));
            }
        }
        // Encabezados fijos al desplazar las horas.
        g.Restore(estadoGrafico);
        using var encabezado=new SolidBrush(Color.FromArgb(232,239,249)); g.FillRectangle(encabezado,0,0,ClientSize.Width,Cabecera);
        string[] dias=["LUN","MAR","MIÉ","JUE","VIE","SÁB","DOM"];
        for (int d=0;d<7;d++)
        {
            var rect=new Rectangle((int)(Eje+d*ancho),6,(int)ancho,Cabecera-8);
            TextRenderer.DrawText(g,$"{dias[d]}\n{lunes.AddDays(d):dd/MM}",fuerte,rect,Color.FromArgb(35,55,84),TextFormatFlags.HorizontalCenter|TextFormatFlags.NoPrefix);
        }
    }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e); if (e.Y<Cabecera) return;
        var item=tarjetas.LastOrDefault(t=>t.Area.Contains(e.Location));
        if (item.Turno is null) return;
        seleccionado=item.Turno.Id; Invalidate(); TurnoSeleccionado?.Invoke(item.Turno);
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e); if (e.Y<Cabecera||e.X<Eje||tarjetas.Any(t=>t.Area.Contains(e.Location))) return;
        float ancho=Math.Max(90,(ClientSize.Width-Eje-SystemInformation.VerticalScrollBarWidth)/7f);
        int dia=(int)((e.X-Eje)/ancho); if (dia is <0 or >6) return;
        int minutos=primera*60+(int)((e.Y-Cabecera-AutoScrollPosition.Y)/escala);
        minutos=minutos/15*15; if (minutos is <0 or >=1440) return;
        HorarioSolicitado?.Invoke(lunes.AddDays(dia).AddMinutes(minutos));
    }
    protected override void Dispose(bool disposing)
    { if (disposing) ayuda.Dispose(); base.Dispose(disposing); }
    internal void ProbarDesplazamiento()
    {
        if(ClientSize.Width<100||ClientSize.Height<100) throw new Exception("El calendario no tiene tamaño para verificar el desplazamiento.");
        Bitmap Capturar()
        { var imagen=new Bitmap(Width,Height); DrawToBitmap(imagen,new Rectangle(0,0,Width,Height)); return imagen; }
        AutoScrollPosition=Point.Empty; Refresh(); using var inicial=Capturar();
        OnMouseWheel(new MouseEventArgs(MouseButtons.None,0,100,100,-120));
        AutoScrollPosition=new Point(0,240); Refresh(); using var abajo=Capturar();
        for(int x=0;x<ClientSize.Width-SystemInformation.VerticalScrollBarWidth;x+=3)
            for(int y=0;y<Cabecera;y+=2)
                if(inicial.GetPixel(x,y)!=abajo.GetPixel(x,y)) throw new Exception("El encabezado del calendario se desplazó o quedó con restos.");
        OnMouseWheel(new MouseEventArgs(MouseButtons.None,0,100,100,120));
        AutoScrollPosition=Point.Empty; Refresh(); using var regreso=Capturar();
        for(int x=0;x<ClientSize.Width-SystemInformation.VerticalScrollBarWidth;x+=3)
            for(int y=Cabecera;y<ClientSize.Height;y+=3)
                if(inicial.GetPixel(x,y)!=regreso.GetPixel(x,y)) throw new Exception("El calendario no se repintó correctamente al regresar.");
    }
}
