using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Aplicacion.Servicios;

namespace SistemaTurnos.WinForms.Formularios;

public sealed class FrmTurno : FormularioBase
{
    private readonly ComboBox cliente=Combo(),paciente=Combo(),profesional=Combo(),servicio=Combo(),hora=Combo();
    private readonly DateTimePicker dia=new() { Format=DateTimePickerFormat.Short };
    private readonly NumericUpDown precio=new() { DecimalPlaces=2,Maximum=9999999999999999.99m,ThousandsSeparator=true };
    private readonly Label resumen=new() { AutoSize=true,MaximumSize=new Size(460,0) };
    private readonly Button guardar=Boton("Confirmar turno");
    private IReadOnlyList<RegistroCatalogo> pacientes=[],servicios=[];
    private bool cargando=true,usaPacientes;
    public FrmTurno(IGestionServicio gestion,int? profesionalInicial=null,DateTime? fechaInicial=null)
    {
        Presentacion("Reservar un turno","Elegí la fecha y uno de los horarios disponibles."); Size=new Size(830,900);
        Campo("Cliente / responsable",cliente); Campo("Paciente",paciente); Campo("Profesional",profesional); Campo("Servicio",servicio);
        Campo("Fecha",dia); Campo("Horarios disponibles",hora); Campo("Precio acordado",precio); Campo("Detalle",resumen);
        var actualizar=Boton("Actualizar horarios"); Campo("",actualizar); Campo("",guardar); AcceptButton=guardar; guardar.Enabled=false;
        void FiltrarPacientes()
        {
            int id=(cliente.SelectedItem as RegistroCatalogo)?.Id??0;
            Opciones(paciente,new[] { new RegistroCatalogo { Id=0,Nombre="Sin paciente" } }.Concat(pacientes.Where(x=>x.Activo && x.ResponsableId==id)));
        }
        void FiltrarServicios()
        {
            var p=profesional.SelectedItem as RegistroCatalogo; cargando=true;
            try { Opciones(servicio,servicios.Where(x=>x.Activo && p?.ServiciosIds.Contains(x.Id)==true)); }
            finally { cargando=false; }
            if (servicio.SelectedItem is RegistroCatalogo s) precio.Value=s.Precio;
        }
        string Destino()
        {
            var p=paciente.SelectedItem as RegistroCatalogo;
            return usaPacientes && p?.TipoPaciente=="P" && !string.IsNullOrWhiteSpace(p.Email)?p.Email:(cliente.SelectedItem as RegistroCatalogo)?.Email??"";
        }
        void Detalle()
        {
            var s=servicio.SelectedItem as RegistroCatalogo; string destino=Destino();
            resumen.Text=(s is null?"Asigná servicios al profesional. ":$"Duración: {s.DuracionMinutos} minutos. ")
                +(hora.Items.Count==0?"Sin horarios libres: revisá la fecha o configurá las jornadas. ":"")
                +(string.IsNullOrWhiteSpace(destino)?"Sin email registrado: se reservará sin aviso.":$"Confirmación por correo a: {destino}.");
        }
        async Task Actualizar()
        {
            guardar.Enabled=false; hora.DataSource=null;
            if (profesional.SelectedItem is RegistroCatalogo p && servicio.SelectedItem is RegistroCatalogo s)
            {
                var horarios=await gestion.HorariosAsync(p.Id,s.Id,dia.Value.Date);
                hora.DisplayMember=nameof(HorarioLibre.Texto); hora.DataSource=horarios.ToList(); guardar.Enabled=horarios.Count>0;
                if (fechaInicial.HasValue)
                {
                    int indice=horarios.ToList().FindIndex(x=>x.InicioLocal==fechaInicial.Value);
                    if (indice>=0) hora.SelectedIndex=indice;
                    fechaInicial=null;
                }
            }
            Detalle();
        }
        cliente.SelectedIndexChanged+=(_,_)=> { if (!cargando) { FiltrarPacientes(); Detalle(); } };
        paciente.SelectedIndexChanged+=(_,_)=> { if (!cargando) Detalle(); };
        profesional.SelectedIndexChanged+=async (_,_)=> { if (!cargando) await EjecutarAsync(async ()=> { FiltrarServicios(); await Actualizar(); }); };
        servicio.SelectedIndexChanged+=async (_,_)=>
        { if (!cargando) await EjecutarAsync(async ()=> { if (servicio.SelectedItem is RegistroCatalogo s) precio.Value=s.Precio; await Actualizar(); }); };
        dia.ValueChanged+=async (_,_)=> { if (!cargando) await EjecutarAsync(Actualizar); };
        actualizar.Click+=async (_,_)=>await EjecutarAsync(Actualizar);
        Shown+=async (_,_)=>await EjecutarAsync(async ()=>
        {
            var c=await gestion.ConfiguracionAsync(); usaPacientes=c.UsaPacientes;
            Contenido.GetControlFromPosition(0,0)!.Text=c.EtiquetaCliente+" / responsable";
            Contenido.GetControlFromPosition(0,1)!.Text=c.EtiquetaPaciente; VisibilidadCampo(paciente,usaPacientes);
            dia.Value=fechaInicial?.Date??TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById(c.ZonaHoraria)).Date;
            pacientes=await gestion.ListarAsync(Catalogo.Pacientes); servicios=await gestion.ListarAsync(Catalogo.Servicios);
            Opciones(cliente,(await gestion.ListarAsync(Catalogo.Clientes)).Where(x=>x.Activo));
            Opciones(profesional,(await gestion.ListarAsync(Catalogo.Profesionales)).Where(x=>x.Activo));
            if (profesionalInicial.HasValue) profesional.SelectedValue=profesionalInicial.Value;
            FiltrarPacientes(); FiltrarServicios(); cargando=false;
            Presentacion("Reservar un turno",$"Horarios de {c.ZonaHoraria} · Precios en {c.Moneda}"); await Actualizar();
        });
        guardar.Click+=async (_,_)=>
        {
            bool creado=false;
            await EjecutarAsync(async ()=>
            {
                if (hora.SelectedItem is not HorarioLibre h) throw new OperacionException("Seleccioná un horario disponible.");
                int Id(ComboBox combo)=>(combo.SelectedItem as RegistroCatalogo)?.Id??0;
                long id=await gestion.CrearTurnoAsync(Id(cliente),usaPacientes && Id(paciente)>0?Id(paciente):null,Id(profesional),Id(servicio),h.InicioLocal,precio.Value);
                MessageBox.Show(this,$"Turno #{id} reservado para {h.InicioLocal:dd/MM/yyyy HH:mm}.\n"
                    +(string.IsNullOrWhiteSpace(Destino())?"No hay una dirección de correo registrada.":"La confirmación quedó en la cola de correo. Revisá su estado en Correos."),"Reserva confirmada"); creado=true;
            });
            if (creado) { DialogResult=DialogResult.OK; Close(); } else await EjecutarAsync(Actualizar);
        };
    }
}

