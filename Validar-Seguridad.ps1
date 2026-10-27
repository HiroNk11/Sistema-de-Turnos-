$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    & dotnet restore SistemaTurnos.sln --locked-mode -p:SecurityAudit=true
    if($LASTEXITCODE -ne 0){throw 'No se pudo completar la auditoría de dependencias. No se considera validada la entrega.'}
    & dotnet build SistemaTurnos.sln -c Release --no-restore
    if($LASTEXITCODE -ne 0){throw 'Falló la compilación.'}
    & dotnet run --project tests/SistemaTurnos.Aplicacion.Pruebas -c Release --no-build
    if($LASTEXITCODE -ne 0){throw 'Fallaron las pruebas de aplicación.'}
    & dotnet run --project tests/SistemaTurnos.Api.Pruebas -c Release --no-build
    if($LASTEXITCODE -ne 0){throw 'Fallaron las pruebas HTTP.'}
} finally {Pop-Location}
