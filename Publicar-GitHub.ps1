# Ejecutar desde la sesión Windows que tiene acceso a GitHub.
# Publica el commit ya revisado: no agrega archivos ni fuerza la rama remota.
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath($PSScriptRoot).Replace('\','/')
function Invoke-GitSeguro {
    & git -c "safe.directory=$repo" -c http.sslBackend=openssl -C $repo @args
    if($LASTEXITCODE -ne 0){throw 'Git no pudo completar la operación. Revisá la autenticación o el error anterior.'}
}
$remoto=Invoke-GitSeguro remote get-url origin
if($remoto.Trim() -ne 'https://github.com/HiroNk11/Sistema-de-Turnos-.git'){throw 'El remoto no es el repositorio autorizado.'}
$rama=Invoke-GitSeguro branch --show-current
if($rama.Trim() -ne 'main'){throw 'La rama revisada para publicar es main.'}
$estado=Invoke-GitSeguro status --porcelain
if($estado){throw 'Hay cambios sin confirmar. Revisarlos y crear un commit antes de publicar.'}
Invoke-GitSeguro push --set-upstream origin main
Write-Output 'Publicación finalizada. Revisar los resultados de GitHub Actions.'
