# Ejecutar en una cuenta de pruebas. Instala un certificado temporal en CurrentUser
# para comprobar TLS real y lo elimina por huella exacta en finally.
param([string]$ApiDll=(Join-Path $PSScriptRoot '..\src\SistemaTurnos.Api\bin\Release\net10.0\SistemaTurnos.Api.dll'))
$ErrorActionPreference='Stop'
if(-not $IsWindows){throw 'Esta prueba requiere Windows y PowerShell 7.'}
$ApiDll=(Resolve-Path -LiteralPath $ApiDll).Path
$carpeta=Join-Path ([IO.Path]::GetTempPath()) ('SistemaTurnos_TLS_'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $carpeta | Out-Null
$certificado=$null; $proceso=$null
$variables=@('SISTEMATURNOS_CONNECTION','SISTEMATURNOS_HTTPS_BIND','Kestrel__Certificates__Default__Path','Kestrel__Certificates__Default__Password','ASPNETCORE_ENVIRONMENT','ASPNETCORE_FORWARDEDHEADERS_ENABLED')
$anteriores=@{}; foreach($v in $variables){$anteriores[$v]=[Environment]::GetEnvironmentVariable($v,'Process')}
try {
    $certificado=New-SelfSignedCertificate -DnsName localhost -CertStoreLocation Cert:\CurrentUser\My -Type SSLServerAuthentication -NotAfter (Get-Date).AddHours(2) -FriendlyName 'SistemaTurnos prueba TLS temporal'
    $raices=[Security.Cryptography.X509Certificates.X509Store]::new('Root','CurrentUser')
    try {$raices.Open('ReadWrite');$raices.Add($certificado)} finally {$raices.Close()}
    $clave=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $pfx=Join-Path $carpeta 'prueba.pfx'
    Export-PfxCertificate -Cert $certificado -FilePath $pfx -Password (ConvertTo-SecureString $clave -AsPlainText -Force) | Out-Null
    $socket=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    $socket.Start();$puerto=$socket.LocalEndpoint.Port;$socket.Stop()
    $env:SISTEMATURNOS_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=SistemaTurnos;Integrated Security=true;Encrypt=false'
    $env:SISTEMATURNOS_HTTPS_BIND="https://localhost:$puerto"
    $env:Kestrel__Certificates__Default__Path=$pfx
    $env:Kestrel__Certificates__Default__Password=$clave
    $env:ASPNETCORE_ENVIRONMENT='Production'
    $env:ASPNETCORE_FORWARDEDHEADERS_ENABLED='false'
    $proceso=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @('"'+$ApiDll+'"') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $carpeta 'salida.log') -RedirectStandardError (Join-Path $carpeta 'error.log')
    $respuesta=$null
    for($i=0;$i -lt 40;$i++) {
        if($proceso.HasExited){throw 'La API terminó antes de responder a la prueba TLS.'}
        try {$respuesta=Invoke-WebRequest "$env:SISTEMATURNOS_HTTPS_BIND/api/acceso/sesion" -SkipHttpErrorCheck -TimeoutSec 2;break}
        catch {Start-Sleep -Milliseconds 250}
    }
    if($null -eq $respuesta -or $respuesta.StatusCode -ne 401){throw 'No se obtuvo un rechazo 401 mediante una conexión TLS validada.'}
    if($respuesta.Headers['Cache-Control'] -notcontains 'no-store'){throw 'Falta no-store.'}
    Write-Output 'OK: TLS real con certificado confiable y comprobación de nombre; petición sin sesión rechazada (401).'
} finally {
    if($null -ne $proceso -and -not $proceso.HasExited){Stop-Process -Id $proceso.Id -Force;$proceso.WaitForExit()}
    foreach($v in $variables){[Environment]::SetEnvironmentVariable($v,$anteriores[$v],'Process')}
    if($null -ne $certificado){
        $raices=[Security.Cryptography.X509Certificates.X509Store]::new('Root','CurrentUser')
        try {$raices.Open('ReadWrite');$raices.Remove($certificado)} finally {$raices.Close()}
        Remove-Item -LiteralPath ('Cert:\CurrentUser\My\'+$certificado.Thumbprint) -ErrorAction SilentlyContinue
    }
    # Solo la carpeta temporal de esta ejecución, comprobada antes del borrado.
    $destino=[IO.Path]::GetFullPath($carpeta)
    $prefijo=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\SistemaTurnos_TLS_'
    if($destino.StartsWith($prefijo,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path $destino -Leaf) -match '^SistemaTurnos_TLS_[a-f0-9]{32}$') {Remove-Item -LiteralPath $destino -Recurse -Force}
}
