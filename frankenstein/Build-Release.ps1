param(
    [string]$OptimizerSource=(Join-Path $PSScriptRoot '..\src'),
    [string]$OriginalTools=(Join-Path $PSScriptRoot 'Originais'),
    [string]$Destination=(Join-Path $PSScriptRoot 'releases'),
    [string]$SigningThumbprint=$env:FRANKENSTEIN_SIGNING_THUMBPRINT,
    [string]$SignToolPath=$env:FRANKENSTEIN_SIGNTOOL,
    [string]$TimestampUrl='http://timestamp.digicert.com',
    [switch]$SigningMachineStore
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Signing.ps1')
# Validate before compiling or modifying distribution artifacts.
$signing=Get-FrankensteinSigningConfiguration -Thumbprint $SigningThumbprint -SignToolPath $SignToolPath -TimestampUrl $TimestampUrl -MachineStore:$SigningMachineStore
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests\Test-Signing.ps1')
if($LASTEXITCODE){throw 'Falha nos testes de suporte a assinatura.'}
$build=Join-Path $PSScriptRoot ('build\release-'+[guid]::NewGuid().ToString('N'))
$app=Join-Path $build 'App'
& (Join-Path $PSScriptRoot 'Build.ps1') -OptimizerSource $OptimizerSource -OriginalTools $OriginalTools -Destination $app
Invoke-FrankensteinSigning -Path (Join-Path $app 'FrankensteinTI.exe') -Configuration $signing
& (Join-Path $PSScriptRoot 'Build-Installer.ps1') -AppFolder $app -Destination $Destination -Scratch (Join-Path $build 'Installer') -SigningConfiguration $signing
& (Join-Path $PSScriptRoot 'tests\Test-Manifest.ps1') -Executables @((Join-Path $app 'FrankensteinTI.exe'),(Join-Path $Destination 'InstalarFrankensteinTI.exe'))
Write-Output ('RELEASE VALIDADA: '+[IO.Path]::GetFullPath($Destination))
