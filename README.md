# Sistema de Turnos

Aplicación genérica de reservas para consultorios, odontología, psicología, veterinarias y peluquerías. C# / .NET 10, Windows Forms y SQL Server con ADO.NET. Incluye un modo cliente/servidor HTTPS para separar el acceso de los operadores del acceso a la base.

## Funcionalidades

- Configuración de rubro, etiquetas, zona horaria y moneda; clientes y pacientes humanos o animales.
- Profesionales, servicios, jornadas y calendario semanal con cliente asignado.
- Reserva por horario con transacción y bloqueo SQL por profesional para evitar superposiciones concurrentes.
- Roles Administrador, Recepción, Caja y Profesional; este último consulta su propia agenda.
- Cobros parciales, saldo, idempotencia, anulación con historial e ingresos por día, semana y mes separados por moneda.
- Confirmaciones por correo mediante cola persistente y SMTP con STARTTLS; Gmail usa contraseña de aplicación configurada fuera del código.
- Sesiones con vencimiento, revocación y bloqueo de intentos; contraseñas PBKDF2 con sal aleatoria.

## Arquitectura

| Proyecto | Responsabilidad |
|---|---|
| `SistemaTurnos.Dominio` | Entidades sin dependencias de UI ni SQL. |
| `SistemaTurnos.Aplicacion` | Casos de uso, autorización, contratos e interfaces. |
| `SistemaTurnos.Infraestructura.SqlServer` | Repositorios ADO.NET, transacciones y correo. |
| `SistemaTurnos.Api` | Servidor HTTPS; ejecuta los casos de uso con una sesión privada por token. |
| `SistemaTurnos.Infraestructura.Http` | Implementación de los contratos para el cliente remoto. |
| `SistemaTurnos.WinForms` | Formularios y composición de dependencias; modo local o remoto. |

Los formularios dependen de interfaces de Aplicación. La composición local referencia SQL exclusivamente para desarrollo; en modo remoto la cadena SQL permanece en el servidor. Esta separación permite agregar otra presentación sin trasladar reglas de negocio a la interfaz.

## Requisitos

Windows, SDK .NET 10 y SQL Server / SQL Server Express / LocalDB. LocalDB corresponde al desarrollo bajo una cuenta Windows de confianza. Para operadores separados, usar el modo HTTPS con una identidad de servidor dedicada y permisos SQL restringidos.

## Inicio local de desarrollo

1. Abrir `SistemaTurnos.sln` o una terminal en la raíz del repositorio.
2. Crear una **base vacía** ejecutando `database/Instalar_LocalDB.sql` desde SQL Server Management Studio o `sqlcmd`. No ejecutarlo sobre una base existente.
3. Copiar `src/SistemaTurnos.WinForms/conexion.example.txt` a `conexion.txt` en la misma carpeta y ajustar la conexión. Este archivo está excluido de Git. También puede configurarse `SISTEMATURNOS_CONNECTION` en el entorno del proceso.
4. Ejecutar:

```powershell
dotnet restore SistemaTurnos.sln --locked-mode
dotnet build SistemaTurnos.sln -c Release --no-restore
.\Iniciar.ps1
```

El primer inicio permite crear al administrador. Después el acceso usa solamente usuario y contraseña. Configurar profesionales, servicios, jornadas y datos del negocio antes de reservar. La instalación no incluye cuentas, pacientes ni credenciales reales.

Para actualizar una base existente, hacer copia de respaldo y seguir las migraciones documentadas; las últimas son `006_ReforzarSeguridad.sql` y el rol opcional `007_RolServidorMinimo.sql`. Los archivos compuestos de actualización contienen partes repetidas: no ejecutar indiscriminadamente todos los SQL de la carpeta.

## Cliente/servidor HTTPS

Seguir [la guía del servidor](MODO-SERVIDOR-SEGURO.md) para preparar identidad Windows, certificado y permisos de SQL. No basta con publicar este código para transformar la instalación local en una instalación aislada.

```powershell
.\Iniciar-ClienteSeguro.ps1 -ServidorHttps https://nombre-del-servidor:7443
```

El servidor requiere `SISTEMATURNOS_CONNECTION`; fuera de Development también exige un certificado explícito. No se distribuyen cadenas SQL a los clientes y se rechazan conexiones HTTP o certificados inválidos. No hay un endpoint público para crear administradores.

## Pruebas y seguridad

```powershell
.\Validar-Seguridad.ps1
python -m unittest discover -s tools -p 'test_*.py'
# Base nueva de pruebas: crea y elimina solo una base con nombre aleatorio.
dotnet run --project tests/SistemaTurnos.Integracion -- --aislada database
# TLS real: CA efímera en memoria, sin instalar certificados.
.\tools\Probar-Https.ps1
```

GitHub Actions restaura versiones bloqueadas con auditoría NuGet obligatoria, compila y ejecuta pruebas de Aplicación, HTTP, SQL real y HTTPS. Los resultados efectivos se consultan en la pestaña Actions; la configuración del flujo no equivale a una ejecución aprobada.

La verificación local aprobó 81 pruebas de aplicación, 23 HTTP en memoria y siete del comparador de avisos, además del smoke de diez formularios. La cuenta aislada no puede acceder a LocalDB ni al almacén de certificados de Windows del usuario. La prueba SQL temporal y TLS se ejecutan también en el runner Windows para resolver esas limitaciones. Consultar [el informe de seguridad](AUDITORIA-SEGURIDAD.md) y [las pruebas](PRUEBAS.md).

No subir contraseñas, certificados privados, bases, respaldos, logs ni archivos de conexión. Si se expuso una credencial, revocarla en su proveedor; borrarla del código no la vuelve segura. La aplicación no incluye MFA ni una configuración para exposición directa a Internet.

## Documentación

- [Instalación y frontera del servidor](MODO-SERVIDOR-SEGURO.md)
- [Auditoría y correcciones de seguridad](AUDITORIA-SEGURIDAD.md)
- [Instantánea de avisos de dependencias](AUDITORIA-DEPENDENCIAS.json)
- [Pruebas e integración SQL](PRUEBAS.md)
- [Agenda semanal](AGENDA-SEMANAL.md)
- [Horarios y Gmail](HORARIOS-Y-GMAIL.md)
- [Roles y cobros](ROLES-Y-COBROS.md)
- [Formularios](FORMULARIOS.md)

El diseño toma como referencia la organización por capas del [Sistema de Gimnasios](https://github.com/HiroNk11/Sistema-de-Gimnasios-C---ADO.NET), con contratos independientes y una frontera de servidor opcional.

## Publicación desde esta copia local

El remoto es `https://github.com/HiroNk11/Sistema-de-Turnos-.git`. Desde una sesión
Windows autenticada en GitHub ejecutar `./Publicar-GitHub.ps1`. Publica únicamente
los commits revisados de `main`, requiere un árbol limpio y no fuerza el remoto.
Si la autenticación falla, completarla en Git Credential Manager o habilitar escritura
en la integración; nunca compartir tokens ni contraseñas por chat.

## Privacidad del código

Ver [revisión de credenciales, datos privados e historial](AUDITORIA-PRIVACIDAD.md).
Se eliminaron rutas del inventario y se preparó el historial sin correo personal.
Los valores de configuración documentados son nombres, no credenciales incorporadas.
