# Gitleaks oficial y versión fijada. No envía el código ni hallazgos a un servicio.
$ErrorActionPreference='Stop'
$raiz=(Split-Path $PSScriptRoot -Parent).Replace('\','/')
$temporal=Join-Path ([IO.Path]::GetTempPath()) ('GitleaksGym_'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporal | Out-Null
$variables=@('GIT_CONFIG_COUNT','GIT_CONFIG_KEY_0','GIT_CONFIG_VALUE_0');$anteriores=@{}
foreach($v in $variables){$anteriores[$v]=[Environment]::GetEnvironmentVariable($v,'Process')}
try {
    $zip=Join-Path $temporal 'gitleaks.zip'
    Invoke-WebRequest 'https://github.com/gitleaks/gitleaks/releases/download/v8.30.1/gitleaks_8.30.1_windows_x64.zip' -OutFile $zip -TimeoutSec 90
    if((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne 'd29144deff3a68aa93ced33dddf84b7fdc26070add4aa0f4513094c8332afc4e'){throw 'No coincide SHA256 de Gitleaks.'}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archivo=[IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $entrada=$archivo.GetEntry('gitleaks.exe')
        if($null -eq $entrada){throw 'El paquete no contiene gitleaks.exe.'}
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entrada,(Join-Path $temporal 'gitleaks.exe'))
    } finally {$archivo.Dispose()}
    $env:GIT_CONFIG_COUNT='1';$env:GIT_CONFIG_KEY_0='safe.directory';$env:GIT_CONFIG_VALUE_0=$raiz
    & (Join-Path $temporal 'gitleaks.exe') git $raiz --redact=100 --no-banner --log-opts=--all
    if($LASTEXITCODE -ne 0){throw 'Gitleaks no aprobó la revisión. No publicar los valores de los hallazgos.'}
} finally {
    foreach($v in $variables){[Environment]::SetEnvironmentVariable($v,$anteriores[$v],'Process')}
    $destino=[IO.Path]::GetFullPath($temporal)
    $prefijo=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\GitleaksGym_'
    if($destino.StartsWith($prefijo,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path $destino -Leaf) -match '^GitleaksGym_[a-f0-9]{32}$'){Remove-Item -LiteralPath $destino -Recurse -Force}
}
