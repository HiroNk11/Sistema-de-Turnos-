using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Aplicacion.Servicios;

namespace SistemaTurnos.WinForms.Formularios;

public sealed class FrmJornadas : FormularioBase
{
    private readonly List<Jornada> jornadas=[];
    public FrmJornadas(IGestionServicio gestion)
    {
        Presentacion("Horarios de atención","Agregá las franjas de mañana y tarde que atiende cada profesional."); Size=new Size(920,900);
        var profesional=Combo(); var semana=Combo(); string[] dias=["Lunes","Martes","Miércoles","Jueves","Viernes","Sábado","Domingo"];
        semana.Items.AddRange(dias); semana.SelectedIndex=0;
        var desde=new DateTimePicker { Format=DateTimePickerFormat.Custom,CustomFormat="HH:mm",ShowUpDown=true,Value=DateTime.Today.AddHours(9) };
        var hasta=new DateTimePicker { Format=DateTimePickerFormat.Custom,CustomFormat="HH:mm",ShowUpDown=true,Value=DateTime.Today.AddHours(18) };
        var paso=new NumericUpDown { Minimum=5,Maximum=120,Value=15 };
        var grilla=new DataGridView { Height=190,ReadOnly=true,AllowUserToAddRows=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false };
        Campo("Profesional",profesional); Campo("Jornadas",grilla); Campo("Día",semana); Campo("Desde",desde); Campo("Hasta",hasta); Campo("Inicios cada (minutos)",paso);
        var botones=new FlowLayoutPanel { AutoSize=true,WrapContents=true };
        var agregar=Boton("Agregar franja"); var quitar=Boton("Quitar seleccionada"); var guardar=Boton("Guardar jornadas");
        botones.Controls.AddRange([agregar,quitar,guardar]); Campo("",botones); bool cargando=true;
        void Refrescar()=>grilla.DataSource=jornadas.Select((j,i)=>new { Indice=i,Dia=dias[j.DiaSemana],Desde=j.Desde.ToString(@"hh\:mm"),Hasta=j.Hasta.ToString(@"hh\:mm"),Paso=j.PasoMinutos }).ToList();
        async Task Cargar()
        { jornadas.Clear(); if (profesional.SelectedItem is RegistroCatalogo p) jornadas.AddRange(await gestion.JornadasAsync(p.Id)); Refrescar(); }
        Shown+=async (_,_)=>await EjecutarAsync(async ()=>
        { Opciones(profesional,(await gestion.ListarAsync(Catalogo.Profesionales)).Where(x=>x.Activo)); cargando=false; await Cargar(); });
        profesional.SelectedIndexChanged+=async (_,_)=> { if (!cargando) await EjecutarAsync(Cargar); };
        agregar.Click+=async (_,_)=>await EjecutarAsync(()=>
        {
            var d=new TimeSpan(desde.Value.Hour,desde.Value.Minute,0); var h=new TimeSpan(hasta.Value.Hour,hasta.Value.Minute,0);
            if (h<=d) throw new OperacionException("El cierre debe ser posterior a la apertura.");
            if (jornadas.Any(x=>x.DiaSemana==semana.SelectedIndex && x.Desde<h && x.Hasta>d)) throw new OperacionException("La franja se superpone con otra del mismo día.");
            jornadas.Add(new(semana.SelectedIndex,d,h,(int)paso.Value)); Refrescar(); return Task.CompletedTask;
        });
        quitar.Click+=(_,_)=> { if (grilla.SelectedRows.Count>0 && grilla.SelectedRows[0].Cells["Indice"].Value is int indice) { jornadas.RemoveAt(indice); Refrescar(); } };
        guardar.Click+=async (_,_)=>await EjecutarAsync(async ()=>
        { await gestion.GuardarJornadasAsync((profesional.SelectedItem as RegistroCatalogo)?.Id??0,jornadas); MessageBox.Show(this,"Horarios guardados.","Agenda"); });
    }
}

