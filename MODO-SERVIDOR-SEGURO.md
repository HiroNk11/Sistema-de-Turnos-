# Modo servidor HTTPS

Implementado y probado en memoria el 27/10/2026. No está desplegado: no se crearon identidades Windows, certificados, logins SQL, servicios ni reglas de firewall.

## Frontera de seguridad

WinForms puede usar una API HTTPS que ejecuta los casos de uso, comprueba roles y accede a SQL. El cliente conserva únicamente un token aleatorio en memoria. Cambiar la interfaz o fabricar una petición no concede permisos de administrador.

Para que esta frontera sea efectiva, ejecutá la API bajo una identidad Windows dedicada y mantené SQL fuera del alcance de los operadores. La cuenta de mantenimiento debe ser otra. No concedas `db_owner`, ni distribuyas credenciales SQL a los clientes. El propietario de LocalDB conserva control total sobre su base; el modo local sigue disponible para desarrollo y aparece identificado en el título.

## Preparar el servidor

1. Respaldar la base y aplicar las migraciones hasta `006_ReforzarSeguridad.sql`. No ejecutar el instalador de base vacía sobre datos existentes.
2. Un administrador de SQL debe revisar y ejecutar `database/007_RolServidorMinimo.sql`. Solo crea un rol: no crea cuentas ni cambia al propietario. Crear el login y usuario de la identidad Windows dedicada y agregar exclusivamente ese usuario al rol `SistemaTurnos_ApiRuntime`. Verificar que no herede permisos elevados de otros roles o grupos.
3. Usar SQL Server o Express bajo una identidad de servicio separada para varios operadores. LocalDB bajo la misma cuenta que opera WinForms no resuelve el hallazgo. No se verificaron aquí permisos efectivos ni la ejecución de los procedimientos con este rol.
4. Configurar `SISTEMATURNOS_CONNECTION` exclusivamente en el entorno protegido del servicio. Preferir autenticación integrada; para SQL remoto se exige cifrado y certificado válido.
5. Configurar un certificado HTTPS válido para el nombre del servidor y confiable en los clientes mediante `Kestrel__Certificates__Default__Path` y, si corresponde, su contraseña desde el gestor de secretos del servicio. Restringir el archivo a la identidad de servicio. Fuera de Development se exige la ruta explícita. No guardar secretos en el repositorio, argumentos o scripts compartidos.
6. Por defecto escucha en `https://localhost:7443`. `SISTEMATURNOS_HTTPS_BIND` permite una dirección HTTPS explícita. La habilitación de acceso remoto y firewall queda a cargo del administrador. No se admite HTTP ni se omite la validación del certificado. No hay configuración de proxy de confianza: no habilitar encabezados reenviados automáticamente.

Con ese entorno, iniciar el servidor compilado:

```powershell
dotnet src/SistemaTurnos.Api/bin/Servidor/Release/net10.0/SistemaTurnos.Api.dll
```

Si todavía no existe ningún usuario, crear el administrador desde la consola del servidor con el mismo comando y `--crear-administrador`. Solicita la contraseña sin mostrarla. No existe un endpoint público para crear al administrador inicial. Las contraseñas nuevas o modificadas requieren al menos 15 caracteres Unicode; los hashes existentes siguen siendo compatibles.

## Conectar WinForms

```powershell
.\Iniciar-ClienteSeguro.ps1 -ServidorHttps https://nombre-del-servidor:7443
```

El script activa `SISTEMATURNOS_API_URL` y `SISTEMATURNOS_REQUIRE_API=true` durante la ejecución. Si falta la URL en ese modo, la aplicación falla sin recurrir a SQL local. No contiene ni transmite una cadena SQL. La confianza del certificado se configura en Windows; no hay una opción para ignorar errores TLS.

Las opciones SMTP se guardan ahora bajo la identidad del servidor usando DPAPI. Reconfigurarlas desde el panel de correo con un administrador; no copiar archivos DPAPI del cliente. Revocar cualquier contraseña de aplicación compartida previamente en el chat y usar una nueva por el formulario de configuración.

## Controles y límites

- Tokens de 256 bits, almacenados como huellas en el servidor, máximo tres sesiones por usuario y 2048 totales. Vencen a los 30 minutos de inactividad o a las ocho horas; reiniciar el servidor invalida las sesiones. No son sesiones distribuidas.
- Autorización por casos de uso, revalidación del usuario, cierre remoto y filtrado por profesional. Los sondeos y el correo automático no renuevan la actividad.
- Límite de acceso de 10 intentos por minuto por IP y general de 120 peticiones por minuto por IP, además del bloqueo de cuenta existente. Solicitudes de hasta 64 KiB, JSON estricto y errores sin detalles internos.
- Sin redirecciones automáticas, cookies, credenciales Windows automáticas ni reintentos de escrituras financieras en el cliente HTTP.
- El procesamiento de correo depende del cliente autenticado activo; todavía no es un trabajador autónomo. Se procesa un correo por petición para acotar su duración.

La prueba HTTP es en memoria: no verifica negociación TLS, certificados, firewall, permisos SQL, concurrencia real ni despliegue. Antes de habilitarlo para operadores, comprobar esos puntos con la identidad de servicio y una copia de la base. Esta versión no implementa MFA ni es una habilitación para publicarla directamente en Internet.

## Dependencias y validación

`Validar-Seguridad.ps1` restaura versiones bloqueadas y falla si NuGet no puede auditar o informa avisos. El flujo `.github/workflows/seguridad.yml` prepara esa validación para GitHub; no se ejecutó remotamente.

Se consultó además el catálogo oficial de NuGet mediante Python con TLS validado: 29 versiones resueltas sin avisos aplicables en la instantánea guardada en `AUDITORIA-DEPENDENCIAS.json`. Esto no garantiza ausencia de vulnerabilidades desconocidas. El script incluye un control positivo con una versión vulnerable conocida y pruebas de comparación de rangos.

Referencias: [auditoría NuGet](https://learn.microsoft.com/nuget/concepts/auditing-packages), [seguridad de Kestrel](https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel/security-considerations), [contraseñas NIST SP 800-63B-4](https://pages.nist.gov/800-63-4/sp800-63b.html). La lista local de contraseñas comunes es básica; no sustituye un catálogo completo de contraseñas comprometidas ni acredita cumplimiento integral de NIST.

## Prueba TLS aislada

El proyecto SistemaTurnos.Tls.Pruebas utiliza Kestrel real y una CA en memoria, con
confianza limitada a su cliente de prueba. También exige rechazo de una CA no confiable
y de un nombre incorrecto. No instala raíces en Windows ni reemplaza la comprobación
del certificado real que se utilizará al desplegar. La ejecución local sigue bloqueada
por SSPI del entorno aislado; revisar el resultado de CI antes de darla por aprobada.
