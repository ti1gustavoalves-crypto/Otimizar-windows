param(
    [string]$OptimizerSource=(Join-Path $PSScriptRoot '..\src'),
    [string]$OriginalTools=(Join-Path $PSScriptRoot 'Originais'),
    [string]$Destination=(Join-Path $PSScriptRoot 'releases')
)
$ErrorActionPreference='Stop'
$build=Join-Path $PSScriptRoot ('build\release-'+[guid]::NewGuid().ToString('N'))
$app=Join-Path $build 'App'
& (Join-Path $PSScriptRoot 'Build.ps1') -OptimizerSource $OptimizerSource -OriginalTools $OriginalTools -Destination $app
& (Join-Path $PSScriptRoot 'Build-Installer.ps1') -AppFolder $app -Destination $Destination -Scratch (Join-Path $build 'Installer')
& (Join-Path $PSScriptRoot 'tests\Test-Manifest.ps1') -Executables @((Join-Path $app 'FrankensteinTI.exe'),(Join-Path $Destination 'InstalarFrankensteinTI.exe'))
Write-Output ('RELEASE VALIDADA: '+[IO.Path]::GetFullPath($Destination))
