# Navegación y agenda semanal

La pantalla principal ahora tiene menú lateral permanente y pestañas de trabajo.
Clientes, pacientes, profesionales, servicios, jornadas y correo se abren dentro
de la misma ventana. Volver a elegir una sección activa su pestaña existente;
no duplica formularios. **Cerrar pestaña** cierra la sección seleccionada y vuelve
a la agenda. La agenda principal no se puede cerrar como una pestaña secundaria.
Cerrar sesión requiere que las operaciones de guardado en curso hayan terminado.

## Agenda por profesional

- Elegir un profesional y una fecha: se muestra su semana de lunes a domingo.
- Anterior/Siguiente desplazan siete días; Hoy usa la fecha en la zona del negocio.
- Los bloques ocupan su duración real. Se muestran hora, persona atendida y servicio.
- Seleccionar un bloque muestra el cliente/responsable, paciente, profesional,
  estado y duración. Pasar el cursor muestra información adicional.
- La jornada de atención tiene fondo blanco; fuera de atención, gris claro.
- Pendientes: azul. Confirmados: verde. En atención: violeta. Finalizados: azul verdoso.
  Ausentes: ocre. Cancelados: gris y ocultos salvo que se marque Mostrar cancelados.
- El calendario permite desplazar las horas y mantiene los días en el encabezado.
- Doble clic en un espacio vacío abre una reserva con profesional y fecha precargados.
  La hora se sugiere solo si corresponde a un intervalo realmente disponible para el
  servicio. La pantalla de reservas y SQL mantienen las validaciones de jornada y concurrencia.
- F5 actualiza la agenda; Ctrl+N abre una reserva.

Para psicología, crear por ejemplo el servicio Sesión individual con la duración
deseada, asignarlo al profesional y cargar sus jornadas. El calendario utiliza
esa duración sin reglas específicas del rubro. Esta versión no crea series recurrentes.

No hay nuevas tablas ni migraciones para esta actualización; utiliza el esquema
con jornadas y correo instalado en la migración 004.

La imagen `previews/agenda.png` muestra datos sintéticos de demostración. No se
insertan en la base ni se utilizan durante el funcionamiento normal.
