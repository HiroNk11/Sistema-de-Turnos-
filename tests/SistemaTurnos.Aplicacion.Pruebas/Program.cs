using SistemaTurnos.Aplicacion.Abstracciones;
using SistemaTurnos.Aplicacion.Modelos;
using SistemaTurnos.Aplicacion.Servicios;
using SistemaTurnos.Dominio.Entidades;
using SistemaTurnos.Infraestructura.SqlServer.Seguridad;

int pruebas=0;
void Exigir(bool condicion,string mensaje)
{ if (!condicion) throw new Exception(mensaje); pruebas++; Console.WriteLine("OK: "+mensaje); }
async Task Rechaza(Func<Task> accion,string mensaje)
{
    try { await accion(); }
    catch (OperacionException) { Exigir(true,mensaje); return; }
    throw new Exception("No se rechazó: "+mensaje);
}
var hash = new PasswordHasher();
string h1=hash.Hash("UnaClaveDePrueba123"), h2=hash.Hash("UnaClaveDePrueba123");
Exigir(h1!=h2 && hash.Verify("UnaClaveDePrueba123",h1),"Hash con sal aleatoria y verificación");
Exigir(!hash.Verify("OtraClave",h1) && !hash.Verify("clave","formato inválido"),"Contraseña incorrecta y hash inválido");
var store=new StoreFalso(); var sesion=new Sesion(); var scope=new ScopeFalso();
var acceso=new AccesoServicio(store,hash,sesion);
var gestion=new GestionServicio(store,scope,sesion,store);
await Rechaza(()=>gestion.ListarAsync(Catalogo.Clientes),"Acceso a gestión requiere sesión");
await Rechaza(()=>acceso.CrearAdministradorAsync("admin","corta","corta"),"Contraseña inicial débil rechazada");
await acceso.CrearAdministradorAsync("admin","UnaClaveDePrueba123","UnaClaveDePrueba123");
Exigir(store.Usuario is not null && sesion.UsuarioId is null,"Alta inicial no inicia sesión automáticamente");
await Rechaza(()=>acceso.CrearAdministradorAsync("otro","UnaClaveDePrueba123","UnaClaveDePrueba123"),"Solo un administrador inicial");
await Rechaza(()=>acceso.LoginAsync("admin","equivocada"),"Login incorrecto rechazado");
Exigir(sesion.UsuarioId is null && store.Fallos==1,"Login incorrecto no crea sesión");
await acceso.LoginAsync("admin","UnaClaveDePrueba123");
Exigir(sesion.UsuarioId==1,"Login válido inicia sesión");
store.Usuario=store.Usuario! with { BloqueadoHastaUtc=DateTime.UtcNow.AddMinutes(15) };
await Rechaza(()=>acceso.LoginAsync("admin","UnaClaveDePrueba123"),"Cuenta bloqueada no ingresa aunque la clave sea válida");
store.Usuario=store.Usuario with { BloqueadoHastaUtc=null };
await acceso.LoginAsync("admin","UnaClaveDePrueba123");
await Rechaza(()=>gestion.GuardarAsync(Catalogo.Servicios,new() { Nombre="Consulta",DuracionMinutos=0 }),"Servicio sin duración rechazado");
await Rechaza(()=>gestion.CrearTurnoAsync(1,null,2,3,new DateTime(2040,1,1,9,0,0),100),"Rubro con pacientes exige seleccionar uno");
await Rechaza(()=>gestion.CrearTurnoAsync(1,99,2,3,new DateTime(2040,1,1,9,0,0),100),"Paciente ajeno al cliente rechazado");
long id=await gestion.CrearTurnoAsync(1,4,2,3,new DateTime(2040,1,1,9,0,0),100);
Exigir(id==42 && scope.Confirmada && scope.Dispuesta,"Caso de uso confirma y dispone su transacción");
Exigir(scope.Turno?.InicioUtc==new DateTime(2040,1,1,12,0,0,DateTimeKind.Utc)
    && scope.Turno.FinUtc==scope.Turno.InicioUtc.AddMinutes(30),"Horario del negocio convertido a UTC y duración aplicada");
sesion.Cerrar();
await Rechaza(()=>gestion.ConfiguracionAsync(),"Cerrar sesión revoca acceso a gestión");
// Disponibilidad: jornadas partidas, duración, límite de cierre y solapamientos.
DateTime fecha=new(2040,1,2);
int semana=((int)fecha.DayOfWeek+6)%7;
Jornada[] franjas=[new(semana,TimeSpan.FromHours(9),TimeSpan.FromHours(12),15),new(semana,TimeSpan.FromHours(14),TimeSpan.FromHours(17),15)];
var zona=TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");
var libres=CalculadorHorarios.Calcular(fecha,zona,30,franjas,[],DateTime.UnixEpoch);
Exigir(libres.Count==22 && libres.All(x=>x.InicioLocal.Hour<12 || x.InicioLocal.Hour>=14),"Jornadas partidas respetan pausa de mediodía");
Exigir(libres.Max(x=>x.FinLocal)<=fecha.AddHours(17) && libres.All(x=>x.FinLocal-x.InicioLocal==TimeSpan.FromMinutes(30)),"La duración completa cabe en la jornada");
var ocupado=new TurnoListado(1,new DateTime(2040,1,2,12,0,0,DateTimeKind.Utc),new DateTime(2040,1,2,12,30,0,DateTimeKind.Utc),"","","","","Confirmado",100,"ARS",2,"CONFIRMADO");
var filtrados=CalculadorHorarios.Calcular(fecha,zona,30,franjas,[ocupado],DateTime.UnixEpoch);
Exigir(!filtrados.Any(x=>x.InicioLocal.TimeOfDay==TimeSpan.FromHours(9) || x.InicioLocal.TimeOfDay==TimeSpan.FromHours(9.25))
    && filtrados.Any(x=>x.InicioLocal.TimeOfDay==TimeSpan.FromHours(9.5)),"Ocupados bloquean superposiciones y permiten turnos consecutivos");
Exigir(CalculadorHorarios.Calcular(fecha,zona,30,franjas,[ocupado with {CodigoEstado="CANCELADO"}],DateTime.UnixEpoch).Count==22,"Cancelados liberan horarios por código de estado");
Exigir(CalculadorHorarios.Calcular(fecha,zona,30,[],[],DateTime.UnixEpoch).Count==0,"Sin jornada no se ofrecen reservas");
Exigir(CalculadorHorarios.Calcular(fecha,zona,181,franjas,[],DateTime.UnixEpoch).Count==0,"Servicio que no cabe no ofrece horarios");
Exigir(CalculadorHorarios.Calcular(fecha,zona,30,franjas,[],new DateTime(2040,1,2,15,0,0,DateTimeKind.Utc))
    .All(x=>TimeZoneInfo.ConvertTimeToUtc(x.InicioLocal,zona)>new DateTime(2040,1,2,15,0,0,DateTimeKind.Utc)),"No se ofrecen horas pasadas");
await acceso.LoginAsync("admin","UnaClaveDePrueba123");
await Rechaza(()=>gestion.GuardarJornadasAsync(2,[franjas[0],franjas[0]]),"Se rechazan jornadas superpuestas");
await Rechaza(()=>gestion.CrearTurnoAsync(1,4,2,3,new DateTime(2040,1,1,9,7,0),100),"Reserva rechazada si no coincide con los inicios de la jornada");
var cola=new ColaFalsa(); var transporte=new TransporteFalso(); var correos=new CorreoServicio(cola,transporte,sesion);
Exigir(await correos.ProcesarAsync()==0 && cola.Reclamados==0,"SMTP deshabilitado conserva la cola");
transporte.Habilitado=true; transporte.Fallar=true;
Exigir(await correos.ProcesarAsync()==0 && cola.Estado=="Error","Fallo SMTP queda registrado para reintento");
await correos.ReintentarAsync(1); transporte.Fallar=false;
Exigir(await correos.ProcesarAsync()==1 && cola.Estado=="Enviado","Reintento exitoso marca correo enviado");
Exigir(await correos.ProcesarAsync()==0,"Correo ya enviado no se vuelve a reclamar");
sesion.Cerrar();
Exigir(SemanaAgenda.Lunes(new DateTime(2027,1,3))==new DateTime(2026,12,28),"Semana de domingo comienza el lunes anterior y cruza el año");
Exigir(SemanaAgenda.Lunes(new DateTime(2026,10,26,18,45,0))==new DateTime(2026,10,26),"El lunes se conserva sin componente horario");
await acceso.LoginAsync("admin","UnaClaveDePrueba123");
store.TurnosPrueba=[ocupado,ocupado with {Id=2,ProfesionalId=3}];
var semanaFiltrada=await gestion.AgendaSemanalAsync(2,fecha);
Exigir(semanaFiltrada.Count==1 && semanaFiltrada[0].ProfesionalId==2,"La agenda semanal muestra solo el profesional seleccionado");
DateTime lunesPrueba=SemanaAgenda.Lunes(fecha);
Exigir(store.UltimoDesde==TimeZoneInfo.ConvertTimeToUtc(lunesPrueba,zona)
    && store.UltimoHasta==TimeZoneInfo.ConvertTimeToUtc(lunesPrueba.AddDays(7),zona),"Consulta semanal usa límites UTC de lunes a lunes");
sesion.Cerrar();
await Rechaza(()=>gestion.AgendaSemanalAsync(2,fecha),"Agenda semanal requiere sesión");
foreach(var rol in Enum.GetValues<RolUsuario>())
{
    store.Usuario=store.Usuario! with {Rol=rol,ProfesionalId=rol==RolUsuario.Profesional?2:null};
    await acceso.LoginAsync("admin","UnaClaveDePrueba123");
    Exigir(sesion.Puede(Permiso.VerAgenda),$"{rol}: acceso a agenda");
    Exigir(sesion.Puede(Permiso.VerInformes)==(rol is RolUsuario.Administrador or RolUsuario.Caja),$"{rol}: permiso de informes");
    Exigir(sesion.Puede(Permiso.Reservar)==(rol is RolUsuario.Administrador or RolUsuario.Recepcion),$"{rol}: permiso de reservas");
}
await Rechaza(()=>gestion.AgendaSemanalAsync(3,fecha),"Profesional no consulta la agenda de otro profesional");
await Rechaza(()=>gestion.ListarAsync(Catalogo.Clientes),"Profesional no consulta el padrón completo de clientes");
await Rechaza(()=>gestion.GuardarAsync(Catalogo.Servicios,new(){Nombre="Servicio"}),"Profesional no modifica servicios");
var cajaFalsa=new CajaFalsa(); var cajaServicio=new CajaServicio(cajaFalsa,store,sesion);
await Rechaza(()=>cajaServicio.RegistrarAsync(1,50,MedioCobro.Efectivo,Guid.NewGuid()),"Profesional no registra cobros");
store.Usuario=store.Usuario! with {Rol=RolUsuario.Recepcion,ProfesionalId=null}; await acceso.LoginAsync("admin","UnaClaveDePrueba123");
Exigir(await cajaServicio.RegistrarAsync(1,50,MedioCobro.Efectivo,Guid.NewGuid())==10,"Recepción registra cobros");
await Rechaza(()=>cajaServicio.InformeAsync(fecha,PeriodoInforme.Mes),"Recepción no accede al informe financiero");
await Rechaza(()=>cajaServicio.AnularAsync(10,"Error"),"Recepción no anula cobros");
store.Usuario=store.Usuario! with {Rol=RolUsuario.Caja}; await acceso.LoginAsync("admin","UnaClaveDePrueba123");
await Rechaza(()=>cajaServicio.RegistrarAsync(1,0,MedioCobro.Tarjeta,Guid.NewGuid()),"Se rechazan cobros de importe cero");
await Rechaza(()=>cajaServicio.RegistrarAsync(1,1.001m,MedioCobro.Tarjeta,Guid.NewGuid()),"Se rechazan importes con más de dos decimales");
await Rechaza(()=>cajaServicio.AnularAsync(10," "),"Anulación exige motivo");
await cajaServicio.AnularAsync(10,"Importe incorrecto"); Exigir(cajaFalsa.Anulado,"Caja puede anular con motivo");
var mes=PeriodosCaja.Limites(new DateTime(2028,2,20),PeriodoInforme.Mes);
Exigir(mes.Desde==new DateTime(2028,2,1)&&mes.Hasta==new DateTime(2028,3,1),"Informe mensual respeta febrero bisiesto");
var semanaCaja=PeriodosCaja.Limites(new DateTime(2027,1,3),PeriodoInforme.Semana);
Exigir(semanaCaja.Desde==new DateTime(2026,12,28)&&semanaCaja.Hasta==new DateTime(2027,1,4),"Informe semanal cruza año con fin exclusivo");
await cajaServicio.InformeAsync(fecha,PeriodoInforme.Dia);
Exigir(cajaFalsa.Hasta-cajaFalsa.Desde==TimeSpan.FromDays(1)&&cajaFalsa.Zona=="Argentina Standard Time","Informe usa fechas de caja en zona del negocio");
store.Usuario=store.Usuario! with {Rol=RolUsuario.Administrador}; await acceso.LoginAsync("admin","UnaClaveDePrueba123");
var usuariosServicio=new UsuariosServicio(new UsuariosFalsos(),hash,sesion);
await Rechaza(()=>usuariosServicio.GuardarAsync(new(1,"admin",RolUsuario.Recepcion,true,null,[]),""),"Administrador no puede quitarse su propio rol");
await Rechaza(()=>usuariosServicio.GuardarAsync(new(0,"doctor",RolUsuario.Profesional,true,null,[]),"UnaClaveDePrueba123"),"Usuario profesional exige vínculo");
sesion.Cerrar();
// Regresiones de seguridad: revocación, carreras de login, inactividad y secretos.
store.Usuario=store.Usuario! with {Activo=true,Rol=RolUsuario.Administrador,ProfesionalId=null,TokenSesion=Guid.NewGuid()};
await acceso.LoginAsync("admin","UnaClaveDePrueba123");
store.Usuario=store.Usuario with {Activo=false};
await Rechaza(()=>gestion.ConfiguracionAsync(),"Cuenta desactivada pierde acceso con la sesión ya abierta");
Exigir(sesion.UsuarioId is null,"La revocación elimina la identidad de la sesión");
store.Usuario=store.Usuario with {Activo=true,TokenSesion=Guid.NewGuid()}; await acceso.LoginAsync("admin","UnaClaveDePrueba123");
store.Usuario=store.Usuario with {Rol=RolUsuario.Recepcion};
await Rechaza(()=>usuariosServicio.ListarAsync(),"Cambio de rol invalida permisos administrativos previos");
store.Usuario=store.Usuario with {Rol=RolUsuario.Administrador,TokenSesion=Guid.NewGuid()}; await acceso.LoginAsync("admin","UnaClaveDePrueba123");
store.Usuario=store.Usuario with {TokenSesion=Guid.NewGuid()};
await Rechaza(()=>gestion.ConfiguracionAsync(),"Cambio de contraseña o token revoca la sesión anterior");
store.AlIntentar=()=>store.Usuario=store.Usuario! with {TokenSesion=Guid.NewGuid()};
await Rechaza(()=>acceso.LoginAsync("admin","UnaClaveDePrueba123"),"La cuenta cambiada durante la verificación no obtiene una sesión");
store.AlIntentar=null; await acceso.LoginAsync("admin","UnaClaveDePrueba123");
var estadoAnterior=new EstadoSesion(store.Usuario.Activo,store.Usuario.Rol,store.Usuario.ProfesionalId,store.Usuario.TokenSesion);
store.ConsultaDiferida=new(TaskCreationOptions.RunContinuationsAsynchronously);
var consultaPendiente=gestion.ConfiguracionAsync(); sesion.Cerrar(); store.ConsultaDiferida.SetResult(estadoAnterior);
await Rechaza(()=>consultaPendiente,"Logout durante una consulta no restaura una sesión anterior"); store.ConsultaDiferida=null;
var relojManual=new RelojPrueba(); var sesionTemporal=new Sesion(relojManual);
var accesoTemporal=new AccesoServicio(store,hash,sesionTemporal); var gestionTemporal=new GestionServicio(store,scope,sesionTemporal,store);
await accesoTemporal.LoginAsync("admin","UnaClaveDePrueba123"); relojManual.Avanzar(TimeSpan.FromMinutes(29));
var correoTemporal=new CorreoServicio(new ColaFalsa(),new TransporteFalso(),sesionTemporal); await correoTemporal.ProcesarAsync();
relojManual.Avanzar(TimeSpan.FromMinutes(2));
await Rechaza(()=>gestionTemporal.ConfiguracionAsync(),"El envío automático no evita el cierre por 30 minutos de inactividad");
await accesoTemporal.LoginAsync("admin","UnaClaveDePrueba123");
for(int i=0;i<16;i++){ relojManual.Avanzar(TimeSpan.FromMinutes(29)); await gestionTemporal.ConfiguracionAsync(); }
relojManual.Avanzar(TimeSpan.FromMinutes(29));
await Rechaza(()=>gestionTemporal.ConfiguracionAsync(),"La sesión vence a las ocho horas aun con actividad");
await acceso.LoginAsync("admin","UnaClaveDePrueba123");
Exigir(!sesion.Puede((Permiso)999),"Permisos desconocidos se deniegan incluso al administrador");
var configuracionConSecreto=new ConfiguracionCorreo(true,"smtp.gmail.com",587,"a@example.invalid","Negocio","a@example.invalid","SECRETO_SINTETICO",true);
Exigir(!configuracionConSecreto.ToString().Contains("SECRETO_SINTETICO")&&!store.Usuario.ToString().Contains(store.Usuario.Hash),"Los DTO de credenciales no imprimen secretos");
var cambiado=SistemaTurnos.Infraestructura.SqlServer.Correo.PoliticaCredencialesSmtp.Resolver(
    configuracionConSecreto with {Servidor="otro.example.invalid",Password="",Habilitado=false},configuracionConSecreto);
Exigir(cambiado.Password==""&&!cambiado.TienePassword,"Cambiar SMTP deshabilitado no arrastra la contraseña anterior");
bool rechazoSmtp=false;
try { SistemaTurnos.Infraestructura.SqlServer.Correo.PoliticaCredencialesSmtp.Resolver(cambiado with {Habilitado=true},configuracionConSecreto); }
catch(OperacionException){rechazoSmtp=true;}
Exigir(rechazoSmtp,"No se habilita un destino SMTP nuevo con credenciales retenidas");
var colaPropietario=new ColaFalsa(); var reclamado=await colaPropietario.ReclamarAsync();
Exigir(!await colaPropietario.FinalizarAsync(reclamado!.Id,Guid.NewGuid(),true)&&colaPropietario.Estado=="Procesando","Un procesador de correo ajeno no confirma el reclamo");
Exigir(await colaPropietario.FinalizarAsync(reclamado.Id,reclamado.TokenReclamo,true),"El propietario del reclamo sí puede finalizarlo");
await Rechaza(()=>acceso.LoginAsync(null!,"clave"),"Login con usuario nulo se rechaza de forma controlada");
Exigir(!hash.Verify("clave",new string('A',10000)),"Hash de longitud excesiva se rechaza sin derivación");
await Rechaza(()=>
{ SistemaTurnos.Infraestructura.SqlServer.Seguridad.ConexionSegura.Validar("Server=db.example.invalid;Database=Turnos;Encrypt=false"); return Task.CompletedTask; },"SQL remoto sin cifrado se rechaza");
await Rechaza(()=>
{ SistemaTurnos.Infraestructura.SqlServer.Seguridad.ConexionSegura.Validar("Server=db.example.invalid;Database=Turnos;Encrypt=true;TrustServerCertificate=true"); return Task.CompletedTask; },"SQL remoto no omite la validación del certificado");
Exigir(SistemaTurnos.Infraestructura.SqlServer.Seguridad.ConexionSegura.Validar(@"Server=(localdb)\MSSQLLocalDB;Database=SistemaTurnos;Encrypt=false;Integrated Security=true").Length>0,
    "La conexión local existente continúa permitida");
sesion.Cerrar();
Console.WriteLine($"RESULTADO: {pruebas} pruebas aprobadas sin base de datos ni envío real de emails.");


