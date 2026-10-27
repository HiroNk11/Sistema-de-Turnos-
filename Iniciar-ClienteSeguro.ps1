param([Parameter(Mandatory=$true)][string]$ServidorHttps)
$ErrorActionPreference='Stop'
$direccion=[Uri]$ServidorHttps
if(-not $direccion.IsAbsoluteUri -or $direccion.Scheme -ne 'https' -or $direccion.UserInfo -ne '' -or $direccion.Query -ne '' -or $direccion.Fragment -ne '' -or $direccion.AbsolutePath -ne '/') {
    throw 'Ingresá una URL HTTPS de servidor sin credenciales, ruta ni parámetros.'
}
$anteriorApi=$env:SISTEMATURNOS_API_URL
$anteriorModo=$env:SISTEMATURNOS_REQUIRE_API
try {
    $env:SISTEMATURNOS_API_URL=$direccion.AbsoluteUri
    $env:SISTEMATURNOS_REQUIRE_API='true'
    & dotnet run --project (Join-Path $PSScriptRoot 'src\SistemaTurnos.WinForms')
    if($LASTEXITCODE -ne 0){throw 'El cliente seguro no pudo iniciarse.'}
} finally {
    $env:SISTEMATURNOS_API_URL=$anteriorApi
    $env:SISTEMATURNOS_REQUIRE_API=$anteriorModo
}
