param([string]$ModulePath)
$ErrorActionPreference = 'Stop'
$tokens = $null; $parseErrors = $null
[System.Management.Automation.Language.Parser]::ParseFile($ModulePath, [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
. $ModulePath -LibraryOnly
$passed = 0
function Assert([bool]$Condition, [string]$Name) { if (-not $Condition) { throw "FALHOU: $Name" }; $script:passed++; Write-Output "OK: $Name" }
Assert (Test-LicenseServer '28000@minduri.fiemg.com.br') 'servidor NX válido'
Assert (-not (Test-LicenseServer '0@servidor')) 'porta zero bloqueada'
Assert (-not (Test-LicenseServer '65536@servidor')) 'porta fora da faixa bloqueada'
Assert (-not (Test-LicenseServer '28000@host;calc')) 'entrada com comando bloqueada'
$plan = Get-GamingPendingPlan @('\??\C:\Windows\System32\gamingservicesproxy_13.dll.0','')
Assert ($plan.Removed.Count -eq 2 -and $plan.Remaining.Count -eq 0) 'pendência específica reconhecida'
$plan = Get-GamingPendingPlan @('\??\C:\Windows\System32\gamingservicesproxy_13.dll.0','','\??\C:\important.dll','\??\C:\destination.dll')
Assert ($plan.Removed.Count -eq 2 -and $plan.Remaining.Count -eq 2) 'outras operações preservadas'
$plan = Get-GamingPendingPlan @('\??\C:\gamingservicesproxy_13.dll.0','\??\C:\destination.dll')
Assert ($plan.Removed.Count -eq 0) 'renomeação legítima preservada'
$failed = $false; try { Get-GamingPendingPlan @('sem-par') | Out-Null } catch { $failed = $true }
Assert $failed 'lista malformada bloqueada'
$snapshot = [pscustomobject]@{ Schema=1; Computer=$env:COMPUTERNAME; Sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value; Scope='User'; Values=@([pscustomobject]@{Name='SPLM_LICENSE_SERVER';Exists=$true;Kind='String';Value='28000@host'},[pscustomobject]@{Name='UGS_LICENSE_SERVER';Exists=$false;Kind='String';Value=$null}) }
Assert-LicenseSnapshot $snapshot
Assert $true 'backup NX válido aceito'
$snapshot.Values[0].Name='OUTRA_VARIAVEL'
$failed=$false; try { Assert-LicenseSnapshot $snapshot } catch {$failed=$true}
Assert $failed 'backup com variável indevida bloqueado'
$snapshot.Values[0].Name='SPLM_LICENSE_SERVER'; $snapshot.Sid='S-1-0-0'
$failed=$false; try { Assert-LicenseSnapshot $snapshot } catch {$failed=$true}
Assert $failed 'backup de outro usuário bloqueado'

# Simula o Registro em memória para testar restauração sem alterar o Windows.
$script:fake = @{}
function Get-LicenseKey([string]$Scope, [bool]$Write=$false) {
    $key = New-Object PSObject
    $key | Add-Member ScriptMethod GetValueNames { return [string[]]@($script:fake.Keys) }
    $key | Add-Member ScriptMethod GetValueKind { param($name) return $script:fake[$name].Kind }
    $key | Add-Member ScriptMethod GetValue { param($name,$fallback,$options) return $script:fake[$name].Value }
    $key | Add-Member ScriptMethod SetValue { param($name,$value,$kind) $script:fake[$name]=@{Value=$value;Kind=[string]$kind} }
    $key | Add-Member ScriptMethod DeleteValue { param($name,$throw) $script:fake.Remove($name) }
    $key | Add-Member ScriptMethod Dispose { }
    return $key
}
$snapshot.Sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$snapshot.Values[0].Kind='ExpandString'; $snapshot.Values[0].Value='%SERVIDOR%'
Set-LicenseSnapshot $snapshot
Assert ($script:fake['SPLM_LICENSE_SERVER'].Kind -eq 'ExpandString' -and $script:fake['SPLM_LICENSE_SERVER'].Value -eq '%SERVIDOR%' -and -not $script:fake.ContainsKey('UGS_LICENSE_SERVER')) 'restauração preserva tipo, valor literal e ausência'
# Exercita falha e recuperação do LabX usando serviços e Registro em memória.
$fixture = Join-Path ([IO.Path]::GetDirectoryName($ModulePath)) ('LabX-Test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
try {
    $installer = Join-Path $fixture 'LabXBundle.exe'
    Set-Content -LiteralPath $installer -Value 'fixture, nunca executar' -Encoding ASCII
    $labRequest = [pscustomobject]@{ Installer=$installer; InstallerHash=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash }
    $script:pending = @('\??\C:\Windows\System32\gamingservicesproxy_13.dll.0','')
    $script:serviceStates = @{GamingServices='Running';GamingServicesNet='Stopped'}
    $script:launchFails = $true
    $script:stops = 0
    function Get-RebootReasons { }
    function Get-Service {
        param([string[]]$Name)
        foreach ($serviceName in $Name) {
            $s = [pscustomobject]@{Name=$serviceName;Status=$script:serviceStates[$serviceName]}
            $s | Add-Member ScriptMethod WaitForStatus {param($status,$timeout) $this.Status=$script:serviceStates[$this.Name]; if($this.Status -ne [string]$status){throw 'Estado inesperado no teste.'}}
            $s
        }
    }
    function Stop-Service { param([string]$Name) $script:serviceStates[$Name]='Stopped'; $script:stops++ }
    function Start-Service { param([string]$Name) $script:serviceStates[$Name]='Running' }
    function Get-ItemProperty { param($LiteralPath,$Name) return [pscustomobject]@{PendingFileRenameOperations=$script:pending} }
    function Remove-ItemProperty { param($LiteralPath,$Name) $script:pending=@() }
    function New-ItemProperty { param($LiteralPath,$Name,$PropertyType,$Value,[switch]$Force) $script:pending=@($Value) }
    function Start-Process { param($FilePath,$WorkingDirectory,[switch]$PassThru) if($script:launchFails){throw 'Falha simulada ao abrir instalador.'}; $p=[pscustomobject]@{ExitCode=0}; $p | Add-Member ScriptMethod WaitForExit {}; return $p }
    $failed=$false; try { Invoke-LabX $labRequest $fixture | Out-Null } catch {$failed=$true}
    Assert ($failed -and $script:pending.Count -eq 2 -and $script:serviceStates.GamingServices -eq 'Running' -and $script:serviceStates.GamingServicesNet -eq 'Stopped') 'falha do instalador restaura pendências e estado dos serviços'
    $script:pending=@('\??\C:\important.dll',''); $script:stops=0
    $failed=$false; try { Invoke-LabX $labRequest $fixture | Out-Null } catch {$failed=$true}
    Assert ($failed -and $script:stops -eq 0 -and $script:pending[0] -eq '\??\C:\important.dll') 'pendência alheia bloqueia antes de parar serviços'
    $script:pending=@('\??\C:\Windows\System32\gamingservicesproxy_13.dll.0',''); $script:launchFails=$false
    Invoke-LabX $labRequest $fixture | Out-Null
    Assert ($script:pending.Count -eq 0 -and $script:serviceStates.GamingServices -eq 'Running' -and $script:serviceStates.GamingServicesNet -eq 'Stopped' -and $script:ResultStatus -eq 'REVIEW') 'LabX termina com serviços restaurados e resultado para conferência'
} finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    $expectedRoot = [IO.Path]::GetFullPath([IO.Path]::GetDirectoryName($ModulePath)).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($expectedRoot,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notlike 'LabX-Test-*') { throw 'Pasta de teste fora do destino esperado.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
Write-Output "$passed testes dos módulos aprovados. Nenhuma configuração real foi alterada."
