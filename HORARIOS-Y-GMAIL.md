# Reservas por hora y confirmaciones Gmail

## Actualizar una instalación existente

1. Cerrar la aplicación.
2. En SSMS conectado a `(localdb)\MSSQLLocalDB`, ejecutar únicamente
   `database/004_ActualizarHorariosYCorreos.sql`, después de haber instalado la migración 003.
   Agrega jornadas, correo del paciente y cola de avisos, y actualiza `usp_Turno_Crear`.
   No borra clientes, usuarios ni reservas existentes. No inventa jornadas de atención.
3. Compilar y ejecutar `SistemaTurnos.WinForms`.
4. Abrir **Horarios de atención**, elegir un profesional y agregar sus jornadas.
   Día: lunes a domingo. Desde/hasta: dentro del mismo día. Inicios cada: 5 a 120 minutos.
   Se pueden cargar dos franjas separadas para contemplar un descanso.
   Presionar **Guardar jornadas** antes de cambiar de profesional o cerrar la pantalla.
5. En **Nuevo turno**, elegir cliente/paciente, profesional y servicio. Seleccionar
   una fecha y uno de los intervalos de **Horarios disponibles**.

No hay horarios disponibles hasta configurar las jornadas. La duración completa del
servicio debe caber en una franja. Las horas pasadas, ocupadas y los cambios horarios
ambiguos no se ofrecen. El procedimiento SQL vuelve a validar al reservar dentro de
la transacción y el bloqueo por profesional. Si otro usuario ocupa el horario,
se informa y se actualiza la lista. No se permite cambiar una jornada dejando
reservas futuras fuera de la nueva disponibilidad.

## Configurar Gmail

En Google, activar la verificación en dos pasos y generar una contraseña de aplicación.
La disponibilidad de esta opción depende de la cuenta y sus políticas.
Instrucciones oficiales: https://support.google.com/accounts/answer/185833?hl=es

En la pantalla **Correos**:

- Servidor: `smtp.gmail.com`.
- Puerto: `587`; conexión STARTTLS obligatoria.
- Usuario y remitente: la dirección Gmail utilizada para enviar.
- Nombre visible: el nombre del negocio.
- Contraseña: la contraseña de aplicación de 16 caracteres, no la contraseña normal.
- Activar **Enviar confirmaciones automáticamente** y guardar.

La configuración se cifra con DPAPI para el usuario actual de Windows en
`%LOCALAPPDATA%\SistemaTurnos\smtp.protegido`. La contraseña no está en SQL ni en
el código fuente. Dejar el campo vacío conserva la contraseña ya guardada, salvo
que se cambie el servidor o usuario. Desactivar el envío conserva la cola pendiente.

## Destinatario y estados

Para un paciente persona se prefiere su email si está cargado. Si no lo tiene,
se usa el del cliente responsable. Para una mascota se utiliza el del responsable.
El formulario informa a qué dirección se encolará el aviso. Sin email se permite
reservar, pero no se genera una confirmación por correo.

La reserva y su aviso se guardan en la misma transacción. El envío SMTP ocurre
después: un fallo del proveedor no revierte la reserva. La cola guarda el horario
original confirmado y los correos de reservas anteriores a esta actualización
no se generan retroactivamente.

La aplicación revisa pendientes al ingresar, después de reservar y cada 30 segundos
mientras está abierta y hay sesión. Cada revisión local procesa hasta diez avisos; en modo HTTPS procesa uno por petición.
Hay cinco intentos con espera creciente entre intentos. En **Correos** se pueden
consultar los últimos 200 estados, procesar pendientes o reintentar un error.
Los turnos cancelados o ya iniciados no generan avisos atrasados.

`Enviado` significa que SMTP aceptó el mensaje; no confirma lectura ni entrega
en la bandeja principal. El procesamiento es al menos una vez: una interrupción
entre la aceptación SMTP y el registro de éxito puede ocasionar un duplicado.
Si la aplicación está cerrada no se envían avisos; la cola permanece en SQL.

## Verificación

- `dotnet run --project tests/SistemaTurnos.Aplicacion.Pruebas`: horarios, validaciones,
  autenticación y fallos/reintentos de correo con sustitutos, sin enviar mensajes.
- `dotnet run --project tests/SistemaTurnos.Integracion`: requiere la migración 004
  y acceso a LocalDB desde la cuenta propietaria. Usa jornadas exclusivas de la prueba.
- La compilación y las pruebas sustitutas no reemplazan una prueba real en tu base
  ni un envío de confirmación desde tu Gmail configurado.

No se incluyen todavía feriados, ausencias excepcionales ni servicio de envío
en segundo plano con la aplicación cerrada.

En modo HTTPS, DPAPI y SMTP se ejecutan bajo la identidad del servidor; ver MODO-SERVIDOR-SEGURO.md.

