# Roles, cobros e informes

## Actualizar

Cerrar la versión anterior y ejecutar `database/005_RolesYCobros.sql` en SSMS sobre
`SistemaTurnos`, después de la migración 004. Se conserva el administrador existente:
al agregar roles por primera vez, el usuario activo de menor ID queda Administrador;
los demás quedan Recepcion. La migración no registra importes cobrados por reservas
anteriores: esos cobros se cargan explícitamente desde Caja.

La nueva tabla Cobro conserva importe, moneda, medio de pago, fecha UTC, usuario
que registró, identificador de solicitud y datos de anulación.

## Permisos iniciales

| Funcionalidad | Administrador | Recepcion | Caja | Profesional |
|---|---|---|---|---|
| Ver agenda | Todas | Todas | Todas | Solo propia |
| Reservar y gestionar clientes/pacientes | Sí | Sí | No | No |
| Gestionar profesionales, servicios y jornadas | Sí | No | No | No |
| Configuración SMTP/negocio y usuarios | Sí | No | No | No |
| Ver y registrar cobros | Sí | Sí | Sí | No |
| Anular cobros e informes de ingresos | Sí | No | Sí | No |

El usuario profesional debe estar vinculado a un profesional existente. Los permisos
se verifican en Aplicación y los cobros/usuarios vuelven a verificar al actor en SQL.
El menú oculta las funciones no permitidas. Un usuario profesional no puede consultar
la agenda de otro ni el padrón completo de clientes desde los casos de uso.

La actualización de seguridad 006 invalida sesiones al cambiar rol, contraseña,
estado o vínculo de la cuenta. Se revalidan en cada operación y cada 15 segundos
en la ventana principal. Hay vencimiento por 30 minutos sin actividad y a las ocho
horas de sesión; los correos automáticos no reinician la inactividad.
No se permite desactivar o degradar la propia cuenta de administrador ni eliminar
el último administrador activo. Cambiar la contraseña desde Usuarios también limpia
el bloqueo por intentos fallidos de esa cuenta.

## Cobros

En **Cobros y saldos**, seleccionar la fecha del turno y su reserva. Se muestran
precio acordado, cobrado vigente y saldo. Ingresar un importe parcial o total y
elegir efectivo, transferencia, tarjeta u otro. Confirmar el registro.

Se permite cobrar antes de la atención y después de ella. No se admiten importes
negativos, cero, mayores al saldo ni reservas canceladas. SQL serializa las operaciones
por turno: dos cajeros simultáneos no pueden sobrepasar el saldo. Reintentar la misma
solicitud no duplica el movimiento. Si la respuesta de red es incierta, actualizar la
cuenta y revisar los movimientos antes de iniciar una nueva operación.

La anulación exige motivo y se conserva en el historial con fecha y usuario.
Anular corrige el registro de caja; no ejecuta una devolución en un banco o tarjeta.
No se borran movimientos desde la interfaz.

## Ingresos por día, semana y mes

La fecha relevante es **cuándo se registró el cobro**, en la zona horaria del negocio,
no la fecha del turno. El informe muestra importes registrados, anulados y neto vigente,
agrupados por día y moneda. No se mezclan ARS, USD u otras monedas.

La semana comienza el lunes; el período mensual respeta la duración real de cada mes.
Las anulaciones se reflejan corrigiendo el neto del día original del cobro, por lo que
los informes históricos pueden cambiar al anular un movimiento.

Este módulo registra ingresos y saldos. No incluye egresos, facturación fiscal,
conciliación bancaria, impuestos, devolución de pagos ni contabilidad de doble partida.

## Calendario

Se fuerza un repintado completo al desplazar y se recortan los bloques debajo del
encabezado fijo. También se descartan las áreas de clic anteriores al movimiento.
Las pruebas de interfaz comparan encabezado y cuerpo al bajar, subir y regresar
a la posición original, además de construir los formularios nuevos.

## Validación

`tests/SistemaTurnos.Aplicacion.Pruebas` verifica permisos, restricciones de caja,
fechas de informes y funcionalidades previas con sustitutos sin SQL.

`tests/SistemaTurnos.Integracion` requiere la migración 005 y acceso a LocalDB.
Comprueba pagos parciales, idempotencia, sobrepago, concurrencia y anulación,
además de las reservas. Crea y elimina exclusivamente sus propios datos de prueba.
