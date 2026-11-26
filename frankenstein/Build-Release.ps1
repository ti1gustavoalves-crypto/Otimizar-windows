param(
    [string]$OptimizerSource=(Join-Path $PSScriptRoot '..\src'),
    [string]$OriginalTools=(Join-Path $PSScriptRoot 'Originais'),
    [string]$Destination=(Join-Path $PSScriptRoot 'releases')
)
$ErrorActionPreference='Stop'
$build=Join-Path $PSScriptRoot ('build\release-'+[guid]::NewGuid().ToString('N'))
$app=Join-Path $build 'App'
& (Join-Path $PSScriptRoot 'Build.ps1') -OptimizerSource $OptimizerSource -OriginalTools $OriginalTools -Destination $app
$csc='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') | ForEach-Object {'/reference:'+$_}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$test=Join-Path $build 'DistributionTests.exe'
& $csc /nologo /target:exe /platform:x64 /main:DistributionTests @refs "/out:$test" @sources (Join-Path $PSScriptRoot 'installer\Deployment.cs') (Join-Path $PSScriptRoot 'tests\DistributionTests.cs')
if($LASTEXITCODE){throw 'Compilação dos testes de distribuição falhou.'}
& $test (Join-Path $build 'DistributionTests')
if($LASTEXITCODE){throw 'Testes de distribuição falharam.'}
& (Join-Path $PSScriptRoot 'Build-Installer.ps1') -AppFolder $app -Destination $Destination -Scratch (Join-Path $build 'Installer')
Write-Output ('RELEASE VALIDADA: '+[IO.Path]::GetFullPath($Destination))
