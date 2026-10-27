# Interfaz Windows Forms

Actualización: para reservas por hora y correo Gmail, aplicar también la migración 004
y seguir `HORARIOS-Y-GMAIL.md`. Esa guía describe el comportamiento actual de disponibilidad y avisos.
La navegación actual por pestañas y el calendario semanal se describen en `AGENDA-SEMANAL.md`.
Los permisos y el módulo de caja requieren la migración 005 y se describen en `ROLES-Y-COBROS.md`.

## Preparación

1. Sobre la base existente `SistemaTurnos`, ejecutar en SSMS `database/003_ActualizarParaWinForms.sql`.
   Este archivo agrega usuarios/configuración y actualiza el procedimiento de reservas;
   no vuelve a crear las tablas iniciales ni carga contraseñas predeterminadas.
2. Abrir `SistemaTurnos.sln` y establecer `SistemaTurnos.WinForms` como proyecto de inicio,
   o ejecutar `dotnet run --project src/SistemaTurnos.WinForms` desde esta carpeta.
3. En la primera ejecución, crear el administrador con una contraseña nueva de al menos 15 caracteres Unicode (máximo 128 unidades UTF-16).
4. Ingresar, configurar el negocio y cargar clientes, servicios y profesionales.
   Al editar un profesional, marcar los servicios que ofrece.
5. Si el rubro usa pacientes, cargarlos vinculados a su cliente responsable.
6. Crear el turno desde la agenda. Los horarios ingresados corresponden a la zona del negocio.

La conexión predeterminada usa `(localdb)\MSSQLLocalDB`, autenticación Windows y `SistemaTurnos`.
Se puede editar `conexion.txt` junto al ejecutable o definir `SISTEMATURNOS_CONNECTION`.
El proceso debe ejecutarse con la cuenta propietaria de LocalDB.

## Pantallas y eventos

- Login y alta del primer administrador: Enter para ingresar, mostrar/ocultar contraseña,
  cinco intentos fallidos bloquean la cuenta por 15 minutos. Hash PBKDF2-SHA256 con sal aleatoria.
- Agenda: cambiar fecha o actualizar consulta los turnos; después de cerrar un alta se actualiza la lista.
- Nuevo turno: seleccionar cliente filtra pacientes; seleccionar profesional filtra servicios;
  cambiar servicio completa duración y precio; confirmar invoca el caso de uso transaccional.
- Catálogos: Nuevo limpia el editor, seleccionar una fila carga sus datos,
  Guardar persiste y recarga. Desmarcar Activo realiza una baja lógica.
  Las ediciones detectan conflictos mediante ROWVERSION.
- Configuración: cambiar rubro propone etiquetas y uso de pacientes; Guardar actualiza el negocio.
- Cerrar sesión vuelve al login. Cerrar la ventana principal termina la aplicación.
- Durante una operación se bloquean las acciones y el cierre para evitar dobles envíos
  o cerrar una pantalla antes de completar la transacción.

## Separación de capas

Los formularios dependen de las interfaces de servicios de Aplicación. Los eventos contienen
coordinación visual; validaciones, autenticación y creación de reservas viven en Aplicación.
IAppStore, IPasswordHasher e ITurnoScopeFactory son interfaces de Aplicación.
ADO.NET, el hash y las transacciones se implementan en Infraestructura.
Solo Program.cs referencia Infraestructura en el proyecto WinForms para componer dependencias.
Los errores técnicos se registran en `%LOCALAPPDATA%\SistemaTurnos\errores.log`.

## Alcance de esta versión

Incluye autenticación inicial, agenda de consulta, creación de reservas, ABM por baja lógica
y configuración del negocio. El acceso se diferencia por roles y el profesional consulta su propia agenda.
Incluye administración de usuarios/roles. No incluye recuperación de contraseña,
reprogramación ni cancelación desde la interfaz. La actualización 004 agrega jornadas y disponibilidad.
Los formularios se construyen por código; no contienen archivos Designer.

## Verificación

`dotnet run --project tests/SistemaTurnos.Aplicacion.Pruebas` prueba autenticación y casos de uso
con repositorios sustitutos, sin acceder a SQL Server.

`dotnet run --project tests/SistemaTurnos.Integracion` realiza las pruebas reales de concurrencia.
Estas requieren LocalDB accesible desde la cuenta que ejecute el comando.

