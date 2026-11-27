param(
    [string]$AppFolder,
    [string]$Destination = (Join-Path $PSScriptRoot 'releases'),
    [string]$Scratch = (Join-Path $PSScriptRoot 'build\installer'),
    $SigningConfiguration=$null
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Signing.ps1')
if($SigningConfiguration) {
    $appSignature=Get-AuthenticodeSignature -LiteralPath (Join-Path $AppFolder 'FrankensteinTI.exe')
    if($appSignature.Status -ne 'Valid' -or !$appSignature.SignerCertificate -or $appSignature.SignerCertificate.Thumbprint -ne $SigningConfiguration.Thumbprint -or !$appSignature.TimeStamperCertificate) { throw 'O aplicativo deve estar assinado e verificado antes de gerar o payload.' }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$AppFolder=[IO.Path]::GetFullPath($AppFolder)
$Destination=[IO.Path]::GetFullPath($Destination)
$Scratch=[IO.Path]::GetFullPath($Scratch)
$payloadRoot=Join-Path $Scratch ('payload-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $Destination,$payloadRoot | Out-Null
foreach($name in @('modules','FrankensteinTI.exe','integridade.json','LEIA-ME.txt')){Copy-Item -LiteralPath (Join-Path $AppFolder $name) -Destination $payloadRoot -Recurse}
# Apenas arquivos de distribuição: nunca inclua dados ou executáveis de teste.
$testExe=Join-Path $payloadRoot 'modules\optimizer\OptimizerSelfTest.exe'
if(Test-Path -LiteralPath $testExe){Remove-Item -LiteralPath $testExe -Force}
if(Get-ChildItem -LiteralPath $payloadRoot -Directory -Recurse | Where-Object Name -in @('Dados','Dados do Otimizador','TestData','TestArtifacts')){throw 'A origem contém dados de execução. Use um build limpo para gerar o instalador.'}
Set-Content -LiteralPath (Join-Path $payloadRoot 'installed.mode') -Value 'FrankensteinTI per-user installation' -Encoding ASCII
$hashes=[ordered]@{}
Get-ChildItem -LiteralPath $payloadRoot -File -Recurse | Sort-Object FullName | ForEach-Object {$hashes[$_.FullName.Substring($payloadRoot.Length+1)]=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
$hashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $payloadRoot 'package-files.json') -Encoding UTF8
$zip=Join-Path $Scratch ('payload-'+[guid]::NewGuid().ToString('N')+'.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($payloadRoot,$zip)
$digest=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
$meta=Join-Path $Scratch 'PackageInfo.cs'
[IO.File]::WriteAllText($meta,'namespace FrankensteinSetup { internal static class PackageInfo { public const string Sha256 = "'+$digest+'"; } }')
$manifest=Join-Path $PSScriptRoot 'installer\app.manifest'
$csc='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$installer=Join-Path $Destination 'InstalarFrankensteinTI.exe'
$refs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') | ForEach-Object {'/reference:'+$_}
& $csc /nologo /target:winexe "/win32icon:$(Join-Path $PSScriptRoot 'assets\frankenstein.ico')" /warn:4 /optimize+ /platform:x64 "/win32manifest:$manifest" @refs "/resource:$zip,Payload" "/out:$installer" $meta (Join-Path $PSScriptRoot 'installer\Installer.cs') (Join-Path $PSScriptRoot 'installer\Deployment.cs') (Join-Path $PSScriptRoot 'installer\AppShutdown.cs')
if($LASTEXITCODE){throw 'Falha ao compilar o instalador.'}
Invoke-FrankensteinSigning -Path $installer -Configuration $SigningConfiguration
$version=[Reflection.AssemblyName]::GetAssemblyName($installer).Version.ToString()
$hash=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
$update=[ordered]@{Product='FrankensteinTI';Version=$version;InstallerUrl=('https://github.com/ti1gustavoalves-crypto/Otimizar-windows/releases/download/frankenstein-v'+([Version]$version).ToString(3)+'/InstalarFrankensteinTI.exe');Sha256=$hash;Size=(Get-Item -LiteralPath $installer).Length;Notes='Diagnóstico geral, fila de tarefas, recuperação e PDF. Reparos exigem ponto de restauração confirmado. Proxy por seleção manual, com backup específico. Downloads com tentativas limitadas e validação de redirecionamentos. Versão portátil disponível no GitHub.'}
$update | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Destination 'update-manifest.json') -Encoding UTF8
($hash+'  InstalarFrankensteinTI.exe') | Set-Content -LiteralPath (Join-Path $Destination 'SHA256SUMS.txt') -Encoding ASCII
Write-Output ('Instalador '+$version+' criado: '+$installer)
