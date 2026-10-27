using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Aplicacion.Servicios;

namespace SistemaTurnos.WinForms.Formularios;

public sealed class FrmCobros : FormularioBase
{
    private sealed record Opcion(long Id,string Texto);
    public FrmCobros(ICajaServicio caja,IGestionServicio gestion,Sesion sesion)
    {
        Presentacion("Cobros y saldos","Registrá pagos completos o parciales. Cada movimiento conserva su responsable."); Size=new Size(1080,980);
        var fecha=new DateTimePicker { Format=DateTimePickerFormat.Short }; var turno=Combo();
        var saldo=new Label { AutoSize=true,Font=new Font(Font,FontStyle.Bold) };
        var importe=new NumericUpDown { DecimalPlaces=2,Minimum=0,Maximum=9999999999999999.99m,ThousandsSeparator=true };
        var medio=Combo(); medio.DataSource=Enum.GetValues<MedioCobro>();
        var grid=new DataGridView { Height=200,ReadOnly=true,AllowUserToAddRows=false,MultiSelect=false,RowHeadersVisible=false,
            AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect };
        Campo("Fecha del turno",fecha); Campo("Reserva",turno); Campo("Cuenta",saldo); Campo("Importe a cobrar",importe); Campo("Medio de pago",medio);
        var acciones=new FlowLayoutPanel { AutoSize=true }; var registrar=Boton("Registrar cobro"); var actualizar=Boton("Actualizar cuenta");
        acciones.Controls.AddRange([registrar,actualizar]); Campo("",acciones); Campo("Movimientos de la reserva",grid);
        var motivo=new TextBox { MaxLength=500 }; var anular=Boton("Anular seleccionado");
        Campo("Motivo de anulación",motivo); Campo("",anular);
        VisibilidadCampo(motivo,sesion.Puede(Permiso.AnularCobros)); VisibilidadCampo(anular,sesion.Puede(Permiso.AnularCobros));
        bool cargando=true; Guid solicitud=Guid.NewGuid(); TimeZoneInfo zona=TimeZoneInfo.Local;
        async Task Cuenta()
        {
            registrar.Enabled=false; grid.DataSource=null; saldo.Text="Seleccioná una reserva.";
            if(turno.SelectedItem is not Opcion t) return;
            var c=await caja.CuentaAsync(t.Id);
            saldo.Text=$"Total: {c.Precio:N2} {c.Moneda} · Cobrado: {c.Cobrado:N2} · Saldo: {c.Saldo:N2}";
            importe.Value=Math.Max(0,c.Saldo); registrar.Enabled=c.Saldo>0&&c.Estado!="CANCELADO";
            grid.DataSource=(await caja.CobrosAsync(t.Id)).Select(x=>new { x.Id,Fecha=TimeZoneInfo.ConvertTimeFromUtc(x.FechaUtc,zona),x.Importe,x.Moneda,x.Medio,x.Usuario,x.Anulado,x.Motivo }).ToList();
        }
        async Task Cargar()
        {
            cargando=true;
            try
            {
                var turnos=await gestion.AgendaAsync(fecha.Value.Date);
                turno.DisplayMember=nameof(Opcion.Texto); turno.DataSource=turnos.Select(t=>new Opcion(t.Id,$"#{t.Id} · {TimeZoneInfo.ConvertTimeFromUtc(t.InicioUtc,zona):HH:mm} · {t.Cliente} · {t.Servicio}")).ToList();
                solicitud=Guid.NewGuid(); await Cuenta();
            }
            finally { cargando=false; }
        }
        Shown+=async (_,_)=>await EjecutarAsync(async ()=>
        { var c=await gestion.ConfiguracionAsync(); zona=TimeZoneInfo.FindSystemTimeZoneById(c.ZonaHoraria); fecha.Value=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,zona).Date; await Cargar(); });
        fecha.ValueChanged+=async (_,_)=> { if(!cargando) await EjecutarAsync(Cargar); };
        turno.SelectedIndexChanged+=async (_,_)=> { if(!cargando) await EjecutarAsync(async ()=> { solicitud=Guid.NewGuid(); await Cuenta(); }); };
        actualizar.Click+=async (_,_)=>await EjecutarAsync(async ()=> { await Cuenta(); solicitud=Guid.NewGuid(); });
        registrar.Click+=async (_,_)=>await EjecutarAsync(async ()=>
        {
            if(turno.SelectedItem is not Opcion t) throw new OperacionException("Seleccioná una reserva.");
            if(MessageBox.Show(this,$"¿Registrar un cobro de {importe.Value:N2} para la reserva #{t.Id}?","Confirmar cobro",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
            long id=await caja.RegistrarAsync(t.Id,importe.Value,(MedioCobro)medio.SelectedItem!,solicitud);
            solicitud=Guid.NewGuid(); await Cuenta(); MessageBox.Show(this,$"Cobro #{id} registrado.","Caja");
        });
        anular.Click+=async (_,_)=>await EjecutarAsync(async ()=>
        {
            if(grid.SelectedRows.Count==0||grid.SelectedRows[0].Cells["Id"].Value is not long id) throw new OperacionException("Seleccioná un cobro.");
            if(MessageBox.Show(this,$"¿Anular el cobro #{id}? Se conservará en el historial.","Confirmar anulación",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
            await caja.AnularAsync(id,motivo.Text); motivo.Clear(); await Cuenta();
        });
    }
}

