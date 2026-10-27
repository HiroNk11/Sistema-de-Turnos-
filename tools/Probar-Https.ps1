# Certificados efímeros en memoria; no instala raíces ni usa la base del usuario.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
& dotnet run --project (Join-Path $root 'tests\SistemaTurnos.Tls.Pruebas') -c Release --no-build
if($LASTEXITCODE -ne 0){throw 'Fallaron las comprobaciones TLS.'}
