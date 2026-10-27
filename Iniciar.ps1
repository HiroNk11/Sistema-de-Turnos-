$ErrorActionPreference='Stop'
$proyecto=Join-Path $PSScriptRoot 'src\SistemaTurnos.WinForms\SistemaTurnos.WinForms.csproj'
& dotnet run --project $proyecto
if ($LASTEXITCODE -ne 0) { throw 'La aplicación no pudo iniciarse.' }
