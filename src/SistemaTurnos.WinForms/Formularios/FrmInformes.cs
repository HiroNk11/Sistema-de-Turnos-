using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Aplicacion.Servicios;

namespace SistemaTurnos.WinForms.Formularios;

public sealed class FrmInformes : FormularioBase
{
    public FrmInformes(ICajaServicio caja)
    {
        Presentacion("Ingresos cobrados","Totales por fecha de cobro. Las reservas pendientes de pago no cuentan como ingreso."); Size=new Size(1100,850);
        var fecha=new DateTimePicker { Format=DateTimePickerFormat.Short }; var periodo=Combo(); periodo.DataSource=Enum.GetValues<PeriodoInforme>();
        var total=new Label { AutoSize=true,MaximumSize=new Size(750,0),Font=new Font(Font.FontFamily,13,FontStyle.Bold) };
        var rango=new Label { AutoSize=true };
        var grid=new DataGridView { Height=300,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false };
        Campo("Fecha de referencia",fecha); Campo("Período",periodo); Campo("Rango",rango); Campo("Totales por moneda",total); Campo("Detalle por día",grid);
        var actualizar=Boton("Consultar ingresos"); Campo("",actualizar);
        async Task Consultar()
        {
            var tipo=(PeriodoInforme)periodo.SelectedItem!; var limites=PeriodosCaja.Limites(fecha.Value,tipo);
            var datos=await caja.InformeAsync(fecha.Value,tipo);
            rango.Text=$"{limites.Desde:dd/MM/yyyy} al {limites.Hasta.AddDays(-1):dd/MM/yyyy}";
            total.Text=datos.Count==0?"Sin cobros en este período.":string.Join("\n",datos.GroupBy(x=>x.Moneda).Select(g=>$"{g.Key} {g.Sum(x=>x.Neto):N2} · {g.Sum(x=>x.Cantidad)} cobros vigentes"));
            grid.DataSource=datos.Select(x=>new { Dia=x.Dia.ToString("dd/MM/yyyy"),x.Moneda,x.Registrado,x.Anulado,CobradoNeto=x.Neto,Cobros=x.Cantidad }).ToList();
        }
        actualizar.Click+=async (_,_)=>await EjecutarAsync(Consultar);
        Shown+=async (_,_)=>await EjecutarAsync(async ()=> { fecha.Value=await caja.HoyAsync(); await Consultar(); });
    }
}

