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

`tools/Probar-Https.ps1` ejecuta tres pruebas TLS reales con Kestrel y una CA
efímera en memoria: cliente de prueba confiable recibe 401/no-store, cliente normal
rechaza esa CA y un nombre incorrecto también se rechaza. La confianza personalizada
solo existe en ese HttpClient de prueba. No instala certificados ni cambia el cliente
de producción. No demuestra que un certificado de despliegue esté bien instalado.
La cuenta aislada de ejecución local tiene un fallo SSPI de Windows; no se declara
aprobada esta prueba hasta obtener una ejecución satisfactoria en un entorno válido.

## Compatibilidad TLS con Windows

La ejecución de GitHub asociada al commit de privacidad aprobó secretos, privacidad,
restauración auditada, compilación, las 81 pruebas de Aplicación, las 23 HTTP, siete
Python y las 22 SQL reales. Falló al negociar TLS con el certificado de prueba.

La clave obtenida mediante CopyWithPrivateKey es efímera. Schannel requiere una clave
compatible con su proveedor Windows: se reimporta el PFX desde un buffer, con una
contraseña aleatoria y UserKeySet. No se utiliza PersistKeySet y .NET gestiona la
vida del contenedor temporal. El buffer PFX se borra después de importar; no se crea
un archivo PFX ni se agrega una raíz confiable a Windows. En otros sistemas se
mantiene EphemeralKeySet.

La validación del nombre, la cadena personalizada del cliente de prueba y los dos
rechazos esperados permanecen activos. El cliente de producción sigue usando su
validación normal de certificados. Esta corrección necesita una nueva ejecución
aprobada en Windows; compilar en el entorno aislado no demuestra éxito TLS.

Referencia técnica: https://github.com/dotnet/runtime/issues/23749 y
https://github.com/dotnet/runtime/issues/101090.
