# Auditoría y refuerzo de seguridad

Fecha: 27/10/2026. Alcance: revisión del código local de la solución, autenticación,
autorización, ADO.NET, correo, reservas, cobros y migraciones. No es una prueba de
penetración de una instalación en producción ni una certificación de seguridad.

## Hallazgos y correcciones

| Prioridad | Hallazgo confirmado | Cambio |
|---|---|---|
| Alta | Desactivar una cuenta o cambiar su rol no revocaba sesiones abiertas. | Token de seguridad por cuenta; trigger que lo rota ante cambios relevantes; revalidación asíncrona en los casos de uso. El formulario principal también comprueba la sesión cada 15 segundos. |
| Alta | El login verificaba un hash y luego podía iniciar sesión con datos de una cuenta modificada durante esa verificación. | El registro del intento exige el token observado. Se revalida la identidad antes de completar el login; fallos de acceso usan un mensaje genérico. |
| Alta | Cambiar el servidor SMTP con envío desactivado conservaba una contraseña que después podía enviarse al nuevo destino. | La contraseña solo se conserva para el mismo servidor, puerto y usuario; habilitar un nuevo destino exige credenciales nuevas. |
| Media | Las sesiones no vencían y el trabajo automático podía mantenerlas activas. | Caducidad de 30 minutos de inactividad y ocho horas absolutas; verificaciones y correos de fondo no reinician la inactividad. |
| Media | Un procesador antiguo de correo podía marcar como finalizado el reclamo de otro proceso. | Token por reclamo, validación de propietario y vencimiento antes de enviar y finalizar. Se revisan turnos cancelados o ya iniciados. Lectura de cola compatible con READ_COMMITTED_SNAPSHOT. |
| Media | Un cambio directo de importe/moneda podía volver incoherente el historial financiero. | Triggers impiden editar datos originales de un cobro, alterar una anulación existente, cambiar moneda con movimientos o bajar el precio por debajo de lo cobrado. |
| Media | Los registros técnicos imprimían excepciones completas; los records podían imprimir hashes o contraseñas. | Diagnóstico con identificador, tipo, HResult y pila, sin Message/Data/ToString; rotación por tamaño. DTO de credenciales con representación redactada. |
| Media | La cadena local sin cifrado podía reutilizarse con un servidor remoto. | Fuera de LocalDB se exige cifrado y validación de certificado. Se desactiva PersistSecurityInfo y se limita el tiempo de conexión. |

También se acotan entradas de contraseña/hash, se rechazan contraseñas de solo
espacios y permisos fuera del enum, se limita el tamaño del archivo SMTP y se usan
archivos temporales únicos para guardarlo. La configuración sigue cifrada con DPAPI
del usuario actual; se limpian los buffers temporales de bytes en claro. Esto no
garantiza borrar de memoria los strings administrados que contienen contraseñas.

Los SQL revisados utilizan parámetros. Los identificadores concatenados de catálogos
provienen de un switch cerrado por enum, no de texto de usuario. Se conservaron
las transacciones, el bloqueo por profesional, los controles de sobrepago y la
idempotencia de cobros. Los procedimientos de caja validan actor, rol y token dentro
de la transacción, manteniendo bloqueada la cuenta durante la operación.

## Riesgo importante que permanece

**El acceso propietario a LocalDB está fuera de la protección de los roles de la
aplicación.** Una persona que controle la cuenta Windows propietaria puede consultar
tablas, leer tokens, modificar datos, usar otra versión del cliente o deshabilitar
triggers. La separación N-Capas no crea una frontera de seguridad frente a esa persona.

La versión actual sirve como aplicación local bajo una cuenta Windows de confianza.
Antes de distribuirla entre operadores no confiables o equipos distintos, hace falta
una frontera de servidor: identidades y permisos SQL restringidos y, preferentemente,
el modo API implementado en esta etapa. No se cambiaron permisos de tu instancia ni
se expusieron puertos. No se afirma que el cliente WinForms proteja frente al dueño
de la base o a código ejecutado bajo su misma cuenta Windows.

## Validación realizada

- Compilación completa: cero errores y cero advertencias.
- 81 pruebas de aplicación aprobadas sin SQL ni correos reales. Incluyen revocación,
  cambio de contraseña durante login, logout durante una consulta, inactividad,
  vencimiento absoluto, conservación de credenciales por destino SMTP, propiedad
  de reclamos y rechazo de conexiones remotas inseguras.
- Construcción de los diez formularios, navegación, transiciones de login y
  comparación visual del calendario al bajar y subir.
- Todos los scripts SQL pasan el parser Microsoft ScriptDom T-SQL 160. Esto comprueba
  sintaxis, no resolución de columnas, permisos, bloqueo ni ejecución real.
- Se ampliaron las pruebas de integración a 19 escenarios, incluidos tokens revocados,
  inmutabilidad de cobros/moneda y finalización de correo por propietario. **No se
  ejecutaron contra tu LocalDB**, inaccesible desde la cuenta aislada.

## Actualización del refuerzo: servidor y dependencias

Se implementó el modo API HTTPS con cliente WinForms, autorización en el servidor,
tokens opacos, límites de peticiones, rechazo de JSON desconocido y un rol SQL de
permisos acotados. Ver `MODO-SERVIDOR-SEGURO.md`. La API ya no es solo una propuesta;
su despliegue bajo una identidad separada sigue pendiente. El riesgo del propietario
de LocalDB permanece en la instalación local hasta hacer esa separación efectiva.

Las contraseñas nuevas requieren 15 caracteres Unicode y se rechazan valores comunes
básicos. Se mantiene la verificación de hashes anteriores para no bloquear cuentas.
Se incorporaron versiones bloqueadas y validación continua de dependencias con fallo
ante errores de auditoría. El flujo de GitHub está preparado, sin ejecución remota.

La consulta nativa de NuGet continúa fallando por TLS en este entorno. Se completó
el cruce por Python con TLS validado y el catálogo oficial: **29 versiones, sin avisos
aplicables en la instantánea del 27/10/2026**. Evidencia, fecha y huellas en
`AUDITORIA-DEPENDENCIAS.json`; no se desactivó la validación de certificados.

Validación adicional: compilación sin errores ni advertencias, 81 pruebas de aplicación,
23 pruebas HTTP en memoria y siete pruebas Python aprobadas. Los scripts SQL pasan
ScriptDom. El smoke de formularios se ejecutó correctamente. No se ejecutaron las
19 pruebas de integración contra la instancia del usuario ni una conexión HTTPS real.

## Aplicar en la base existente

1. Cerrar las versiones anteriores de la aplicación.
2. Conservar una copia de respaldo y ejecutar `database/006_ReforzarSeguridad.sql`
   después de la migración 005. No elimina cuentas ni movimientos y puede repetirse.
3. Ejecutar `database/006_VerificarSeguridad.sql`: solo devuelve cantidades de
   inconsistencias; no imprime nombres, direcciones, hashes ni contraseñas.
4. Abrir la compilación de Servidor y volver a iniciar sesión.
5. Ejecutar las pruebas reales desde la cuenta propietaria de LocalDB.

El instalador de base vacía se actualizó para incluir esta etapa. No debe usarse ese
instalador inicial para actualizar una base con datos. Los archivos de log anteriores
no se borraron: el nuevo registro seguro no elimina información escrita por versiones
anteriores.


## Estado de entrega y publicación

Se agregó README, política de reporte, exclusión de secretos y pruebas SQL en una base
GUID que puede eliminarse sin tocar la base del usuario. El flujo Windows también
incluye una prueba TLS real con un certificado temporal y limpieza por huella.

Estas dos comprobaciones siguen sin evidencia de ejecución aprobada: LocalDB no es
accesible para la cuenta aislada y CertEnroll no puede crear el certificado en su
perfil (0x80070002). No se sustituyó esa evidencia por una declaración de éxito.

La publicación está preparada en el repositorio Git local. La integración devolvió
403 Resource not accessible by integration y el push por consola no pudo acceder al
almacén Windows de credenciales. `Publicar-GitHub.ps1` permite publicar desde la
sesión Windows autenticada sin forzar la rama ni agregar archivos inadvertidos.
El despliegue requiere confirmar equipo, identidad y nombre HTTPS; no se abrieron
puertos ni se alteró la seguridad de la instalación existente.

## Revisión de privacidad y aislamiento de pruebas

Ver AUDITORIA-PRIVACIDAD.md. Se limpiaron las rutas del inventario y se preparó la
reescritura autorizada del correo de autor. Se añadieron controles continuos de secretos
y privacidad. La prueba TLS anterior dependía del almacén CurrentUser; se reemplazó
por una CA en memoria y tres comprobaciones acotadas a un servidor local de pruebas.
La confianza de Windows y el cliente HTTPS de producción quedan intactos. La compilación
pasa, pero el entorno aislado conserva un error SSPI: no se afirma éxito TLS local.

## Corrección de la prueba HTTPS en Windows

El error de CI del commit de privacidad ocurrió únicamente en la negociación TLS del
certificado sintético; secretos, privacidad y SQL real aprobaron. La prueba ahora
reimporta el certificado servidor para que Schannel pueda usar una clave temporal
Windows, sin instalar raíces ni omitir validaciones. Los detalles y límites están
en PRUEBAS.md. La corrección se considera preparada, pendiente de confirmación en CI.
