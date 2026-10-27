param([string]$CadenaConexion = 'Server=(localdb)\MSSQLLocalDB;Database=SistemaTurnos;Integrated Security=true;Encrypt=false;Connect Timeout=10')
$ErrorActionPreference = 'Stop'
$proyecto = Join-Path $PSScriptRoot 'tests\SistemaTurnos.Integracion\SistemaTurnos.Integracion.csproj'
& dotnet run --project $proyecto -- $CadenaConexion
if ($LASTEXITCODE -ne 0) { throw 'Las pruebas no finalizaron correctamente. Revisá el mensaje anterior.' }
