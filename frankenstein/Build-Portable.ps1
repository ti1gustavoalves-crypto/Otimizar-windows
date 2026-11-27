param([Parameter(Mandatory=$true)][string]$AppFolder,[Parameter(Mandatory=$true)][string]$Destination,[Parameter(Mandatory=$true)][string]$Scratch)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$AppFolder=[IO.Path]::GetFullPath($AppFolder);$Destination=[IO.Path]::GetFullPath($Destination)
$stage=Join-Path ([IO.Path]::GetFullPath($Scratch)) ('portable-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
foreach($name in @('FrankensteinTI.exe','modules','integridade.json','LEIA-ME.txt')){Copy-Item -LiteralPath (Join-Path $AppFolder $name) -Destination $stage -Recurse}
Set-Content -LiteralPath (Join-Path $stage 'portable.mode') -Value 'Portable distribution: data stays alongside the executable.' -Encoding ASCII
if(Get-ChildItem -LiteralPath $stage -Recurse -Directory | Where-Object Name -in @('Dados','TestArtifacts','TestData')){throw 'Dados privados encontrados na origem portatil.'}
$version=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $stage 'FrankensteinTI.exe')).Version.ToString(3)
$archive=Join-Path $Destination ('FrankensteinTI-'+$version+'-Portatil.zip')
if(Test-Path -LiteralPath $archive){throw 'O pacote portatil ja existe; use um destino limpo.'}
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$archive)
((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash+'  '+[IO.Path]::GetFileName($archive)) | Add-Content -LiteralPath (Join-Path $Destination 'SHA256SUMS.txt') -Encoding ASCII
Write-Output ('PORTATIL: '+$archive)
