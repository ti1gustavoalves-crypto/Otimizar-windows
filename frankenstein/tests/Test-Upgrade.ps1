param([string]$OldInstaller,[string]$NewInstaller,[string]$TestsAssembly,[string]$Portable)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$fixture=Join-Path (Join-Path $PSScriptRoot '..\build') ('upgrade-040-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
function Payload([string]$Exe,[string]$Name){
 $a=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($Exe));$expected=$a.GetType('FrankensteinSetup.PackageInfo').GetField('Sha256').GetRawConstantValue()
 $path=Join-Path $fixture ($Name+'.zip');$resource=$a.GetManifestResourceStream('Payload');$stream=[IO.File]::Create($path)
 try{$resource.CopyTo($stream)}finally{$resource.Dispose();$stream.Dispose()}
 if((Get-FileHash -LiteralPath $path).Hash -ne $expected){throw 'Payload corrompido'}
 [pscustomobject]@{Path=$path;Hash=$expected;Assembly=$a}
}
$old=Payload $OldInstaller 'old';$new=Payload $NewInstaller 'new'
$test=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($TestsAssembly));$deployment=$test.GetType('FrankensteinSetup.Deployment')
$root=Join-Path $fixture 'installed';$install=$deployment.GetMethod('Install')
$app=$install.Invoke($null,[object[]]@([string]$root,[string]$old.Path,[string]$old.Hash,[bool]$false))
if([Reflection.AssemblyName]::GetAssemblyName((Join-Path $app 'FrankensteinTI.exe')).Version.ToString() -ne '0.3.3.0'){throw 'Origem nao e 0.3.3'}
New-Item -ItemType Directory -Path (Join-Path $root 'Dados') -Force | Out-Null
$preferences=Join-Path $root 'Dados\interface.json';[IO.File]::WriteAllText($preferences,'{"Theme":"Minecraft"}')
$app=$install.Invoke($null,[object[]]@([string]$root,[string]$new.Path,[string]$new.Hash,[bool]$false));$deployment.GetMethod('Verify').Invoke($null,[object[]]@([string]$app))
if([Reflection.AssemblyName]::GetAssemblyName((Join-Path $app 'FrankensteinTI.exe')).Version.ToString() -ne '0.4.0.0'){throw 'Destino nao e 0.4.0'}
if([IO.File]::ReadAllText($preferences) -ne '{"Theme":"Minecraft"}'){throw 'Preferencias alteradas'}
if(@(Get-ChildItem -LiteralPath $root -Directory -Filter 'previous-*').Count -ne 1){throw 'Versao anterior ausente'}
$rejected=$false;try{$install.Invoke($null,[object[]]@([string]$root,[string]$old.Path,[string]$old.Hash,[bool]$false)) | Out-Null}catch{$rejected=$true}
if(!$rejected){throw 'Downgrade foi aceito'}
$new.Assembly.GetType('FrankensteinSetup.Setup').GetMethod('Shortcut').Invoke($null,[object[]]@([string]$fixture,[bool]$true))
if(!(Test-Path -LiteralPath (Join-Path $fixture 'Frankenstein TI.lnk'))){throw 'Atalho ausente'}
$new.Assembly.GetType('FrankensteinSetup.Setup').GetMethod('Shortcut').Invoke($null,[object[]]@([string]$fixture,[bool]$false))
$zip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Portable))
try{
 $names=@($zip.Entries | ForEach-Object FullName)
 if($names -notcontains 'portable.mode' -or $names -contains 'installed.mode' -or @($names | Where-Object {$_ -match '(^|[/\\])(Dados|TestArtifacts|TestData)([/\\]|$)'}).Count){throw 'Conteudo portatil invalido'}
}finally{$zip.Dispose()}
$portableRoot=Join-Path $fixture 'portable';[IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($Portable),$portableRoot)
if((Get-FileHash -LiteralPath (Join-Path $portableRoot 'FrankensteinTI.exe')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $app 'FrankensteinTI.exe')).Hash){throw 'EXE portatil diverge do instalado'}
Write-Output 'OK: atualizacao real de payload 0.3.3 para 0.4.0, dados/tema preservados, backup anterior, downgrade bloqueado e atalhos COM.'
Write-Output 'OK: ZIP portatil sem dados privados/testes; executavel identico ao instalado.'
Write-Output ('FIXTURE: '+$fixture)
