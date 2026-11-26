param(
    [string]$OptimizerSource = (Join-Path $PSScriptRoot '..\src'),
    [string]$OriginalTools = (Join-Path $PSScriptRoot 'Originais'),
    [string]$Destination = (Join-Path $PSScriptRoot '..\build\Frankenstein-TI'),
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$Destination = [IO.Path]::GetFullPath($Destination)
$OptimizerSource = [IO.Path]::GetFullPath($OptimizerSource)
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { throw 'Compilador .NET Framework x64 não encontrado.' }
New-Item -ItemType Directory -Force -Path $Destination,(Join-Path $Destination 'modules\optimizer'),(Join-Path $Destination 'modules\legacy'),(Join-Path $Destination 'Referencias'),(Join-Path $Destination 'TestArtifacts') | Out-Null
$optimizerOutput = Join-Path $Destination 'modules\optimizer\Otimizador-Portatil.exe'
$excluded = @('Installer.cs','V2SelfTest.cs')
$optimizerFiles = @(Get-ChildItem -LiteralPath $OptimizerSource -Filter '*.cs' | Where-Object { $excluded -notcontains $_.Name } | Sort-Object Name | Select-Object -ExpandProperty FullName)
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Management.dll','System.Web.Extensions.dll','Microsoft.VisualBasic.dll') | ForEach-Object { '/reference:' + $_ }
$resources = @('painel','manutencao','atualizacoes','sistema','ajustes')
$resourceNames = @('NavigationPanelPng','NavigationMaintenancePng','NavigationUpdatesPng','NavigationSystemPng','NavigationSettingsPng')
$embedded = for ($i=0;$i -lt $resources.Count;$i++) { '/resource:' + (Join-Path $OptimizerSource ('assets\navigation\' + $resources[$i] + '.png')) + ',' + $resourceNames[$i] }
$manifest = Join-Path $OptimizerSource 'app.manifest'
$icon = Join-Path $OptimizerSource 'assets\optimizer-icon.ico'
& $csc /nologo /target:winexe /warn:4 /optimize+ /platform:x64 "/win32manifest:$manifest" "/win32icon:$icon" @embedded @refs "/out:$optimizerOutput" @optimizerFiles
if ($LASTEXITCODE) { throw 'Falha na compilação do otimizador.' }
Set-Content -LiteralPath (Join-Path $Destination 'modules\optimizer\portable.mode') -Value 'portable' -Encoding ASCII
Set-Content -LiteralPath (Join-Path $Destination 'modules\optimizer\release-channel.json') -Value '{"ManifestUrl":""}' -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $OptimizerSource 'release-notes.txt') -Destination (Join-Path $Destination 'modules\optimizer\release-notes.txt') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'scripts\Central.ps1') -Destination (Join-Path $Destination 'modules\Central.ps1') -Force
foreach ($file in @('CentralDeInstalacaoTI.exe','InstaladorProgramas.exe','Diagnostico-e-Reparo-de-Apps.exe','NavegadorSOS-Professional-3.0.exe','Reparar-Navegadores.ps1')) {
    Copy-Item -LiteralPath (Join-Path $OriginalTools $file) -Destination (Join-Path $Destination 'modules\legacy') -Force
}
Get-ChildItem -LiteralPath $OriginalTools -File | Copy-Item -Destination (Join-Path $Destination 'Referencias') -Force
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
& $csc /nologo /target:winexe /warn:4 /optimize+ /platform:x64 "/win32manifest:$manifest" @refs "/out:$(Join-Path $Destination 'FrankensteinTI.exe')" @sources
if ($LASTEXITCODE) { throw 'Falha na compilação da central.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LEIA-ME.txt') -Destination $Destination -Force
$hashes = [ordered]@{}
foreach ($relative in @('modules\Central.ps1','modules\optimizer\Otimizador-Portatil.exe','modules\optimizer\portable.mode','modules\optimizer\release-channel.json','modules\optimizer\release-notes.txt','modules\legacy\CentralDeInstalacaoTI.exe','modules\legacy\InstaladorProgramas.exe','modules\legacy\Diagnostico-e-Reparo-de-Apps.exe','modules\legacy\NavegadorSOS-Professional-3.0.exe','modules\legacy\Reparar-Navegadores.ps1')) { $hashes[$relative] = (Get-FileHash -LiteralPath (Join-Path $Destination $relative) -Algorithm SHA256).Hash }
$hashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Destination 'integridade.json') -Encoding UTF8
if (-not $SkipTests) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests\Test-Modules.ps1') -ModulePath (Join-Path $Destination 'modules\Central.ps1')
    if ($LASTEXITCODE) { throw 'Testes PowerShell reprovados.' }
    $testExe = Join-Path $Destination 'TestArtifacts\CentralSelfTest.exe'
    & $csc /nologo /target:exe /warn:4 /platform:x64 @refs "/out:$testExe" (Join-Path $PSScriptRoot 'src\Core.cs') (Join-Path $PSScriptRoot 'tests\SelfTest.cs')
    if ($LASTEXITCODE) { throw 'Compilação dos testes da central falhou.' }
    & $testExe $Destination
    if ($LASTEXITCODE) { throw 'Testes da central reprovados.' }
    $upstreamTest = Join-Path $Destination 'modules\optimizer\OptimizerSelfTest.exe'
    & $csc /nologo /target:exe /warn:4 /optimize+ /platform:x64 /define:SELF_TEST /main:CodexPerformanceOptimizer.V2SelfTest @refs "/out:$upstreamTest" @optimizerFiles (Join-Path $OptimizerSource 'V2SelfTest.cs')
    if ($LASTEXITCODE) { throw 'Compilação dos testes do otimizador falhou.' }
    & $upstreamTest
    if ($LASTEXITCODE) { throw 'Autotestes do otimizador reprovados. Consulte o log.' }
}
Write-Output "COMPILAÇÃO CONCLUÍDA: $Destination"
