# Pruebas de integración con LocalDB

Requieren el SDK .NET 10 y ejecutar con la cuenta Windows propietaria de `(localdb)\MSSQLLocalDB`.
La base `SistemaTurnos` y `usp_Turno_Crear` deben estar instalados previamente.
La versión actual requiere también la migración `004_ActualizarHorariosYCorreos.sql`.
Para las pruebas de cobros, aplicar además `005_RolesYCobros.sql`.
Las pruebas de seguridad actuales requieren `006_ReforzarSeguridad.sql`.

Desde PowerShell, ubicado en esta carpeta:

```powershell
.\Probar-LocalDB.ps1
```

Alternativamente:

```powershell
dotnet run --project tests/SistemaTurnos.Integracion
```

Se esperan diecinueve mensajes `OK` y `RESULTADO: 19 pruebas aprobadas`:
creación, superposición parcial, duplicado, turnos consecutivos, cancelados,
rollback explícito, rollback por Dispose y dos conexiones concurrentes.
También se prueban idempotencia de cobros, pago parcial, rechazo de sobrepago,
cobros concurrentes y anulación con conservación del historial.
La actualización de seguridad agrega revocación de token, rechazo de cobros con
sesión revocada, protección de importe/moneda y propiedad de los reclamos de correo.

Cada ejecución crea su propio cliente, profesional y servicio con una marca GUID.
Finalmente elimina exclusivamente sus registros de prueba, incluidos los turnos.
No modifica registros preexistentes. Los contadores IDENTITY pueden avanzar.
Si el proceso se interrumpe abruptamente, puede quedar la fixture de esa ejecución.

Estado de verificación: compilación y ejecución desde el entorno aislado no equivalen
a una prueba satisfactoria contra la LocalDB del usuario. Solo la salida de las diecinueve
pruebas confirma el comportamiento con esa instancia.

## Base aislada para pruebas automatizadas

```powershell
dotnet run --project tests/SistemaTurnos.Integracion -- --aislada database
```

Crea una base `SistemaTurnos_Prueba_<GUID>`, instala el esquema y el rol 007 y ejecuta
las 19 pruebas más tres comprobaciones del rol con EXECUTE AS USER: permisos acotados,
rechazo de DML directo y creación mediante procedimiento. Elimina solamente esa base
al finalizar, validando su nombre. Si se interrumpe el proceso, puede quedar la base
temporal; nunca elimina la base `SistemaTurnos` del usuario.

GitHub Actions ejecuta este modo en su propia LocalDB. El resultado aprobado del
flujo es la evidencia de ejecución, no la mera presencia del script. La prueba del
rol usa un usuario SQL sin login; no demuestra el aislamiento de los grupos Windows
y permisos efectivos del servidor de producción.

`tools/Probar-Https.ps1` comprueba una conexión HTTPS real y un rechazo 401 usando
un certificado temporal para localhost. Lo agrega y elimina en CurrentUser, junto
con el proceso y archivo temporal. Ejecutarlo con una cuenta de pruebas. No instala
un certificado de producción ni desactiva la validación TLS.

