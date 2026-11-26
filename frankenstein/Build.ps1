param([string]$OptimizerSource=(Join-Path $PSScriptRoot '..\src'),[string]$OriginalTools=(Join-Path $PSScriptRoot 'Originais'),[string]$Destination=(Join-Path $PSScriptRoot 'build\Unified'),[switch]$SkipTests)
$ErrorActionPreference='Stop'
$Destination=[IO.Path]::GetFullPath($Destination);$OptimizerSource=[IO.Path]::GetFullPath($OptimizerSource)
$csc='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$generated=Join-Path (Split-Path $Destination) ('sources-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $Destination,(Join-Path $Destination 'modules'),(Join-Path $Destination 'TestArtifacts') | Out-Null
& (Join-Path $PSScriptRoot 'Prepare-Integrated.ps1') -OptimizerSource $OptimizerSource -Destination $generated
& (Join-Path $PSScriptRoot 'Build-Icon.ps1')
$refs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Management.dll','System.Web.Extensions.dll','Microsoft.VisualBasic.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') | ForEach-Object {'/reference:'+$_}
$embedded=@('/resource:'+(Join-Path $PSScriptRoot 'assets\catalog.png')+',CentralInstalacaoTI.icons.png')
$names=@('painel','manutencao','atualizacoes','sistema','ajustes');$ids=@('NavigationPanelPng','NavigationMaintenancePng','NavigationUpdatesPng','NavigationSystemPng','NavigationSettingsPng')
for($i=0;$i -lt $names.Count;$i++){$embedded+=('/resource:'+(Join-Path $OptimizerSource ('assets\navigation\'+$names[$i]+'.png'))+','+$ids[$i])}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src'),(Join-Path $PSScriptRoot 'integrated'),$generated -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$manifest=Join-Path $PSScriptRoot 'installer\app.manifest';$icon=Join-Path $PSScriptRoot 'assets\frankenstein.ico'
& $csc /nologo /target:winexe /main:FrankensteinTI.Program /warn:4 /optimize+ /platform:x64 "/win32manifest:$manifest" "/win32icon:$icon" @refs @embedded "/out:$(Join-Path $Destination 'FrankensteinTI.exe')" @sources
if($LASTEXITCODE){throw 'Falha na compilacao integrada.'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'scripts\Central.ps1'),(Join-Path $PSScriptRoot 'scripts\Apps.ps1') -Destination (Join-Path $Destination 'modules') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LEIA-ME.txt') -Destination $Destination -Force
$hashes=[ordered]@{};foreach($name in @('Central.ps1','Apps.ps1')){$hashes['modules\'+$name]=(Get-FileHash -LiteralPath (Join-Path $Destination ('modules\'+$name)) -Algorithm SHA256).Hash}
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Destination 'integridade.json') -Encoding UTF8
if(-not $SkipTests){
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests\Test-Modules.ps1') -ModulePath (Join-Path $Destination 'modules\Central.ps1')
    if($LASTEXITCODE){throw 'Falha nos testes PowerShell.'}
    $core=Join-Path $Destination 'TestArtifacts\CoreSelfTest.exe'
    & $csc /nologo /target:exe /platform:x64 @refs "/out:$core" (Join-Path $PSScriptRoot 'src\Core.cs') (Join-Path $PSScriptRoot 'tests\SelfTest.cs')
    if($LASTEXITCODE){throw 'Falha ao compilar testes nativos.'}
    & $core $Destination
    if($LASTEXITCODE){throw 'Falha nos testes nativos.'}
    $test=Join-Path $Destination 'TestArtifacts\DistributionTests.exe'
    & $csc /nologo /target:exe /platform:x64 /main:DistributionTests @refs @embedded "/out:$test" @sources (Join-Path $PSScriptRoot 'installer\Deployment.cs') (Join-Path $PSScriptRoot 'installer\AppShutdown.cs') (Join-Path $PSScriptRoot 'tests\DistributionTests.cs')
    if($LASTEXITCODE){throw 'Falha ao compilar testes.'}
    & $test (Join-Path $Destination 'TestArtifacts\Distribution')
    if($LASTEXITCODE){throw 'Falha nos testes integrados.'}
    $upstream=@(Get-ChildItem -LiteralPath $OptimizerSource -Filter '*.cs' | Where-Object Name -ne 'Installer.cs' | Select-Object -ExpandProperty FullName)
    $test=Join-Path $Destination 'TestArtifacts\OptimizerSelfTest.exe'
    & $csc /nologo /target:exe /platform:x64 /define:SELF_TEST /main:CodexPerformanceOptimizer.V2SelfTest @refs "/out:$test" @upstream
    if($LASTEXITCODE){throw 'Falha ao compilar testes do motor.'}
    & $test
    if($LASTEXITCODE){throw 'Falha nos testes do motor.'}
}
Write-Output "APP INTEGRADO: $Destination"
