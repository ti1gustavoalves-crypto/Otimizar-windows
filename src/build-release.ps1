param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\outputs'),
    [string]$UpdateBaseUrl = 'https://raw.githubusercontent.com/ti1gustavoalves-crypto/Otimizar-windows/main/releases',
    [string]$CertificateThumbprint = '',
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { throw 'Compilador .NET Framework x64 não encontrado.' }

$output = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($output) | Out-Null
$app = Join-Path $output 'OtimizadorDeDesempenho.exe'
$installer = Join-Path $output 'InstalarOtimizadorDeDesempenho.exe'
$notes = Join-Path $PSScriptRoot 'release-notes.txt'
$localManifest = Join-Path $PSScriptRoot 'update-manifest.json'
$channel = Join-Path $PSScriptRoot 'release-channel.json'
$iconIco = Join-Path $PSScriptRoot 'assets\optimizer-icon.ico'
$iconPng = Join-Path $PSScriptRoot 'assets\optimizer-icon.png'
if (-not (Test-Path -LiteralPath $iconIco) -or -not (Test-Path -LiteralPath $iconPng)) { throw 'Arquivos do icone do aplicativo nao encontrados.' }
$sources = @(
    'PerformanceOptimizer.cs', 'PerformanceOptimizerV2.cs', 'MainForm.Diagnostics.cs', 'MainForm.Control.cs', 'OptimizerModels.cs', 'OptimizerEngine.cs',
    'AdvancedFeatures.cs', 'BenchmarkHistory.cs', 'QualityInfrastructure.cs', 'OptionalSensors.cs', 'WindowsMaintenance.cs', 'SystemCommand.cs'
) | ForEach-Object { Join-Path $PSScriptRoot $_ }
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Management.dll','System.Web.Extensions.dll') | ForEach-Object { '/reference:' + $_ }
$manifestPath = Join-Path $PSScriptRoot 'app.manifest'

$testExecutable = Join-Path ([IO.Path]::GetTempPath()) ("OtimizadorSelfTest-" + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $csc /nologo /target:exe /warn:4 /optimize+ /platform:x64 /main:CodexPerformanceOptimizer.V2SelfTest @references "/out:$testExecutable" @sources (Join-Path $PSScriptRoot 'V2SelfTest.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar os testes.' }
    & $testExecutable
    if ($LASTEXITCODE -ne 0) { throw 'A suíte de testes bloqueou a release.' }
}
finally {
    if (Test-Path -LiteralPath $testExecutable) { Remove-Item -LiteralPath $testExecutable -Force }
}

& $csc /nologo /target:winexe /warn:4 /optimize+ /platform:x64 "/win32manifest:$manifestPath" "/win32icon:$iconIco" "/resource:$iconPng,OptimizerIconPng" @references "/out:$app" @sources
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o aplicativo.' }

function Find-SignTool {
    $roots = @("${env:ProgramFiles(x86)}\Windows Kits\10\bin", "$env:ProgramFiles\Windows Kits\10\bin") | Where-Object { Test-Path -LiteralPath $_ }
    foreach ($root in $roots) {
        $candidate = Get-ChildItem -LiteralPath $root -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } | Sort-Object FullName -Descending | Select-Object -First 1
        if ($candidate) { return $candidate.FullName }
    }
    return $null
}

function Sign-Artifact([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) { return $false }
    $certificate = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Thumbprint -eq $CertificateThumbprint -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) } | Select-Object -First 1
    if (-not $certificate) { throw 'Certificado de assinatura válido não encontrado.' }
    $signtool = Find-SignTool
    if (-not $signtool) { throw 'signtool.exe não encontrado.' }
    & $signtool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $Path
    if ($LASTEXITCODE -ne 0) { throw "Falha ao assinar $Path" }
    & $signtool verify /pa /v $Path
    if ($LASTEXITCODE -ne 0) { throw "Falha ao verificar a assinatura de $Path" }
    return $true
}

$appSigned = Sign-Artifact $app
$version = [Reflection.AssemblyName]::GetAssemblyName($app).Version.ToString()
$channelObject = @{ ManifestUrl = if ([string]::IsNullOrWhiteSpace($UpdateBaseUrl)) { '' } else { $UpdateBaseUrl.TrimEnd('/') + '/update-manifest.public.json' } }
[IO.File]::WriteAllText($channel, ($channelObject | ConvertTo-Json -Compress), $utf8NoBom)
[IO.File]::WriteAllText($localManifest, (@{ Version=$version; InstallerUrl=''; Sha256=''; Notes='Versao instalada. Consulte as notas no aplicativo.' } | ConvertTo-Json -Compress), $utf8NoBom)

Copy-Item -LiteralPath $notes -Destination (Join-Path $output 'release-notes.txt') -Force
Copy-Item -LiteralPath $localManifest -Destination (Join-Path $output 'update-manifest.json') -Force
Copy-Item -LiteralPath $channel -Destination (Join-Path $output 'release-channel.json') -Force

& $csc /nologo /target:winexe /warn:4 /optimize+ /platform:x64 "/win32manifest:$manifestPath" "/win32icon:$iconIco" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$app,OptimizerBinary" "/resource:$notes,ReleaseNotes" "/resource:$localManifest,UpdateManifest" "/resource:$channel,ReleaseChannel" "/out:$installer" (Join-Path $PSScriptRoot 'Installer.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o instalador.' }
$installerSigned = Sign-Artifact $installer

$appHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $app).Hash
$installerHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $installer).Hash
$publicManifest = [ordered]@{
    Version = $version
    InstallerUrl = if ([string]::IsNullOrWhiteSpace($UpdateBaseUrl)) { '' } else { $UpdateBaseUrl.TrimEnd('/') + '/InstalarOtimizadorDeDesempenho.exe' }
    Sha256 = $installerHash
    Notes = 'Nova versao disponivel no GitHub. Download protegido por HTTPS e SHA-256.'
}
$publicManifestPath = Join-Path $output 'update-manifest.public.json'
[IO.File]::WriteAllText($publicManifestPath, ($publicManifest | ConvertTo-Json -Compress), $utf8NoBom)

$summary = @(
    "Versao: $version"
    "Aplicativo assinado: $appSigned"
    "Instalador assinado: $installerSigned"
    "SHA-256 do aplicativo: $appHash"
    "SHA-256 do instalador: $installerHash"
    "Canal: $($channelObject.ManifestUrl)"
) -join [Environment]::NewLine
[IO.File]::WriteAllText((Join-Path $output 'release-summary.txt'), $summary + [Environment]::NewLine, $utf8NoBom)

Write-Output "Release $version criada em $output"
