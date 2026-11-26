param([string]$Request, [switch]$LibraryOnly)
$ErrorActionPreference = 'Stop'

function Test-LicenseServer([string]$Server) {
    if ($Server -notmatch '^[0-9]{1,5}@[a-zA-Z0-9][a-zA-Z0-9.-]*$') { return $false }
    $port = [int]($Server.Split('@')[0])
    return ($port -ge 1 -and $port -le 65535)
}
function Get-LicenseKey([string]$Scope, [bool]$Write = $false) {
    if ($Scope -eq 'User') { $root = [Microsoft.Win32.Registry]::CurrentUser; $path = 'Environment' }
    elseif ($Scope -eq 'Machine') { $root = [Microsoft.Win32.Registry]::LocalMachine; $path = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment' }
    else { throw 'Escopo de licença inválido.' }
    if ($Write) { return $root.CreateSubKey($path) }
    return $root.OpenSubKey($path, $false)
}
function Get-LicenseSnapshot([string]$Scope) {
    $key = Get-LicenseKey $Scope
    try {
        $values = foreach ($name in @('SPLM_LICENSE_SERVER', 'UGS_LICENSE_SERVER')) {
            $exists = $null -ne $key -and $key.GetValueNames() -contains $name
            $kind = if ($exists) { [string]$key.GetValueKind($name) } else { 'String' }
            if ($kind -notin @('String', 'ExpandString')) { throw "Tipo de Registro inesperado em $name. Nenhuma alteração aplicada." }
            [pscustomobject]@{ Name = $name; Exists = [bool]$exists; Kind = $kind; Value = $(if ($exists) { $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) } else { $null }) }
        }
        return [pscustomobject]@{ Schema = 1; Computer = $env:COMPUTERNAME; Sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value; Scope = $Scope; Created = (Get-Date).ToString('o'); Values = @($values) }
    } finally { if ($null -ne $key) { $key.Dispose() } }
}
function Assert-LicenseSnapshot($Snapshot) {
    if ($Snapshot.Schema -ne 1 -or $Snapshot.Computer -ne $env:COMPUTERNAME -or $Snapshot.Scope -notin @('User', 'Machine')) { throw 'Backup incompatível com este computador ou escopo.' }
    if ($Snapshot.Scope -eq 'User' -and $Snapshot.Sid -ne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value) { throw 'O backup pertence a outra conta do Windows.' }
    if (@($Snapshot.Values).Count -ne 2) { throw 'O backup deve conter exatamente duas variáveis.' }
    foreach ($name in @('SPLM_LICENSE_SERVER', 'UGS_LICENSE_SERVER')) {
        $items = @($Snapshot.Values | Where-Object Name -eq $name)
        if ($items.Count -ne 1) { throw 'Variáveis inválidas no backup.' }
        $item = $items[0]
        if ($item.Exists -isnot [bool] -or $item.Kind -notin @('String', 'ExpandString')) { throw 'Tipo inválido no backup.' }
        if ($item.Exists -and $item.Value -isnot [string]) { throw 'Valor inválido no backup.' }
        if ($item.Exists -and $item.Value.Length -gt 32767) { throw 'Valor excessivamente longo no backup.' }
    }
}
function Set-LicenseSnapshot($Snapshot) {
    Assert-LicenseSnapshot $Snapshot
    $key = Get-LicenseKey $Snapshot.Scope $true
    try {
        foreach ($item in $Snapshot.Values) {
            if ($item.Exists) { $kind = [Microsoft.Win32.RegistryValueKind][Enum]::Parse([Microsoft.Win32.RegistryValueKind], $item.Kind); $key.SetValue($item.Name, $item.Value, $kind) }
            else { $key.DeleteValue($item.Name, $false) }
        }
    } finally { $key.Dispose() }
    $actual = Get-LicenseSnapshot $Snapshot.Scope
    foreach ($item in $Snapshot.Values) {
        $value = $actual.Values | Where-Object Name -eq $item.Name
        if ($value.Exists -ne $item.Exists -or ($item.Exists -and ($value.Value -cne $item.Value -or $value.Kind -ne $item.Kind))) { throw "Verificação falhou: $($item.Name)." }
    }
}
function Get-GamingPendingPlan([string[]]$Pairs) {
    if ($Pairs.Count % 2 -ne 0) { throw 'Lista de operações pendentes com formato inesperado.' }
    $remaining = New-Object 'System.Collections.Generic.List[string]'
    $removed = New-Object 'System.Collections.Generic.List[string]'
    for ($i = 0; $i -lt $Pairs.Count; $i += 2) {
        $source = $Pairs[$i]; $destination = $Pairs[$i + 1]
        if ($source -match '(?i)\\gamingservicesproxy_[0-9]+\.dll\.0$' -and [string]::IsNullOrEmpty($destination)) { $removed.Add($source); $removed.Add($destination) }
        else { $remaining.Add($source); $remaining.Add($destination) }
    }
    return [pscustomobject]@{ Remaining = $remaining.ToArray(); Removed = $removed.ToArray() }
}
function Get-RebootReasons {
    foreach ($path in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending', 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired')) {
        if (Test-Path -LiteralPath $path) { $path }
    }
}
function Invoke-Diagnose {
    $lines = New-Object 'System.Collections.Generic.List[string]'
    $lines.Add('RAIO-X DO COMPUTADOR · SOMENTE LEITURA')
    $lines.Add('Data: ' + (Get-Date).ToString('dd/MM/yyyy HH:mm:ss'))
    try {
        $os = Get-CimInstance Win32_OperatingSystem -OperationTimeoutSec 20
        $lines.Add("Windows: $($os.Caption) · build $($os.BuildNumber)")
        $lines.Add(('RAM livre / total: {0:N1} / {1:N1} GB' -f ($os.FreePhysicalMemory / 1MB), ($os.TotalVisibleMemorySize / 1MB)))
        $lines.Add("Última inicialização: $($os.LastBootUpTime)")
    } catch { $lines.Add('Consulta do sistema indisponível: ' + $_.Exception.Message) }
    try {
        foreach ($disk in Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' -OperationTimeoutSec 20) { $lines.Add(('Disco {0}: {1:N1} GB livres de {2:N1} GB' -f $disk.DeviceID, ($disk.FreeSpace / 1GB), ($disk.Size / 1GB))) }
    } catch { $lines.Add('Consulta de discos indisponível: ' + $_.Exception.Message) }
    $pending = @(Get-RebootReasons)
    $rename = Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name PendingFileRenameOperations -ErrorAction SilentlyContinue
    $lines.Add('Sinais comuns de reinicialização pendente: ' + $(if ($pending.Count -or $rename.PendingFileRenameOperations) { 'SIM' } else { 'não encontrados' }))
    $lines.Add(''); $lines.Add('NAVEGADORES · presença nos caminhos conhecidos')
    $found = 0
    foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA) | Where-Object { $_ } | Select-Object -Unique) {
        foreach ($relative in @('Microsoft\Edge\Application\msedge.exe','Google\Chrome\Application\chrome.exe','Mozilla Firefox\firefox.exe','BraveSoftware\Brave-Browser\Application\brave.exe','Programs\Opera\opera.exe','Programs\Opera GX\opera.exe')) {
            $path = Join-Path $root $relative
            if (Test-Path -LiteralPath $path) { $file = Get-Item -LiteralPath $path; $lines.Add("$($file.VersionInfo.ProductName) · $($file.VersionInfo.FileVersion) · $path"); $found++ }
        }
    }
    if (-not $found) { $lines.Add('Nenhum executável encontrado nos caminhos consultados. Isso não comprova ausência de instalações personalizadas.') }
    $lines.Add(''); $lines.Add('Nenhum navegador foi encerrado. Nenhum reparo foi aplicado. Não foi testado o carregamento de páginas.')
    return $lines -join "`r`n"
}
function Invoke-LabX($InputData, [string]$OutputFolder) {
    $installer = [string]$InputData.Installer
    if (-not (Test-Path -LiteralPath $installer -PathType Leaf) -or [IO.Path]::GetFileName($installer) -ine 'LabXBundle.exe') { throw 'Selecione um arquivo LabXBundle.exe existente.' }
    if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ne $InputData.InstallerHash) { throw 'O instalador mudou desde a seleção. Selecione-o novamente.' }
    if (@(Get-RebootReasons).Count) { throw 'Há reinicialização pendente de manutenção/atualização do Windows. Reinicie antes de instalar o LabX.' }
    $keyPath = 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager'
    $property = 'PendingFileRenameOperations'
    $pairs = @((Get-ItemProperty -LiteralPath $keyPath -Name $property -ErrorAction SilentlyContinue).$property | ForEach-Object { [string]$_ })
    $plan = Get-GamingPendingPlan $pairs
    if ($plan.Remaining.Count) { throw 'Existem operações pendentes diferentes do caso conhecido de Gaming Services. Nenhuma foi removida. Reinicie o Windows e investigue a pendência.' }
    $services = @(Get-Service -Name GamingServices, GamingServicesNet -ErrorAction SilentlyContinue)
    if (@($services | Where-Object { $_.Status -notin @('Running','Stopped') }).Count) { throw 'Gaming Services em transição. Aguarde e tente novamente.' }
    $backup = [ordered]@{ Computer = $env:COMPUTERNAME; Created = (Get-Date).ToString('o'); Pending = $pairs; Services = @($services | Select-Object Name, @{n='Status';e={[string]$_.Status}}) }
    $backup | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputFolder 'labx-backup.json') -Encoding UTF8
    $restoreErrors = New-Object 'System.Collections.Generic.List[string]'
    $changed = $false; $started = $false
    try {
        if ($plan.Removed.Count) {
            foreach ($service in $services | Where-Object Status -eq Running) { Stop-Service -Name $service.Name -ErrorAction Stop; $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30)) }
            $current = @((Get-ItemProperty -LiteralPath $keyPath -Name $property -ErrorAction SilentlyContinue).$property | ForEach-Object { [string]$_ })
            if (($current | ConvertTo-Json -Compress) -cne ($pairs | ConvertTo-Json -Compress)) { throw 'As pendências mudaram durante a preparação. Nenhuma operação foi removida.' }
            Remove-ItemProperty -LiteralPath $keyPath -Name $property -ErrorAction Stop
            $changed = $true
            if ((Get-ItemProperty -LiteralPath $keyPath -Name $property -ErrorAction SilentlyContinue).$property) { throw 'A pendência reapareceu. Instalação bloqueada.' }
        }
        if (@(Get-RebootReasons).Count) { throw 'Surgiu uma nova pendência de reinicialização. Instalação bloqueada.' }
        $process = Start-Process -FilePath $installer -WorkingDirectory (Split-Path -LiteralPath $installer) -PassThru -ErrorAction Stop
        $started = $true
        $process.WaitForExit()
        $script:ExitCode = $process.ExitCode
        if ($script:ExitCode -notin @(0,3010)) { throw "O instalador encerrou com código $script:ExitCode. Consulte o resultado no LabX." }
        $script:ResultStatus = 'REVIEW'
        return "Instalador encerrado com código $script:ExitCode. Confira se o LabX foi instalado corretamente. O encerramento não valida a instalação. Backup: labx-backup.json."
    } finally {
        if ($changed -and -not $started) {
            try {
                if (-not (Get-ItemProperty -LiteralPath $keyPath -Name $property -ErrorAction SilentlyContinue).$property) { New-ItemProperty -LiteralPath $keyPath -Name $property -PropertyType MultiString -Value ([string[]]$pairs) -Force | Out-Null }
                else { $restoreErrors.Add('Pendências novas detectadas; backup preservado sem sobrescrevê-las.') }
            } catch { $restoreErrors.Add('Falha ao restaurar pendências: ' + $_.Exception.Message) }
        }
        foreach ($service in $backup.Services) {
            try {
                $currentService = Get-Service -Name $service.Name
                if ($service.Status -eq 'Running') { if ($currentService.Status -ne 'Running') { Start-Service -Name $service.Name; $currentService.WaitForStatus('Running', [TimeSpan]::FromSeconds(30)) } }
                elseif ($currentService.Status -ne 'Stopped') { Stop-Service -Name $service.Name; $currentService.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30)) }
            } catch { $restoreErrors.Add("Serviço $($service.Name): $($_.Exception.Message)") }
        }
        if ($restoreErrors.Count) { throw ('Revise a recuperação dos serviços/pendências. ' + ($restoreErrors -join ' | ')) }
    }
}
if ($LibraryOnly) { return }
$script:ExitCode = 0
$script:ResultStatus = 'OK'
$outputFolder = $null
try {
    $inputData = Get-Content -LiteralPath $Request -Raw -Encoding UTF8 | ConvertFrom-Json
    $outputFolder = [IO.Path]::GetFullPath([string]$inputData.Output)
    if ($outputFolder -ne [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Request))) { throw 'A saída deve ficar na pasta desta execução.' }
    $detail = switch ([string]$inputData.Mode) {
        'Diagnose' { Invoke-Diagnose }
        'NxCheck' {
            $rows = foreach ($scope in @('User','Machine')) { $snapshot = Get-LicenseSnapshot $scope; foreach ($item in $snapshot.Values) { "$scope · $($item.Name) = $(if ($item.Exists) { $item.Value } else { '(não configurada)' })" } }
            ($rows -join "`r`n") + "`r`n`r`nConsulta de configuração. Não comprova conexão nem validade da licença."
        }
        { $_ -in @('NxSet','NxRestore') } {
            if (@(Get-Process -Name ugraf,nx -ErrorAction SilentlyContinue).Count) { throw 'Feche o Siemens NX antes de configurar ou restaurar a licença. Nenhum processo foi encerrado.' }
            if ($inputData.Mode -eq 'NxSet') {
                if (-not (Test-LicenseServer ([string]$inputData.Server))) { throw 'Servidor inválido. Use porta@servidor, com porta de 1 a 65535.' }
                $before = Get-LicenseSnapshot ([string]$inputData.Scope)
                $target = $before | ConvertTo-Json -Depth 6 | ConvertFrom-Json
                foreach ($value in $target.Values) { $value.Exists = $true; $value.Kind = 'String'; $value.Value = [string]$inputData.Server }
            } else {
                $target = Get-Content -LiteralPath ([string]$inputData.Backup) -Raw -Encoding UTF8 | ConvertFrom-Json
                Assert-LicenseSnapshot $target
                $before = Get-LicenseSnapshot $target.Scope
            }
            $backupPath = Join-Path $outputFolder 'nx-backup.json'
            $before | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $backupPath -Encoding UTF8
            try { Set-LicenseSnapshot $target }
            catch { $originalError = $_.Exception.Message; try { Set-LicenseSnapshot $before } catch { throw "Falha: $originalError. A recuperação também falhou: $($_.Exception.Message). Use o backup $backupPath." }; throw "Falha: $originalError. O estado anterior foi restaurado." }
            "Variáveis de licença gravadas e verificadas no escopo $($target.Scope).`r`nBackup: $backupPath`r`n`r`nSaia e entre novamente no Windows antes de abrir o NX. A configuração não comprova disponibilidade da licença no servidor."
        }
        'LabX' { Invoke-LabX $inputData $outputFolder }
        'Enrollment' {
            $lines = New-Object 'System.Collections.Generic.List[string]'
            $lines.Add('VÍNCULO CORPORATIVO · SOMENTE LEITURA')
            $lines.Add((& "$env:SystemRoot\System32\dsregcmd.exe" /status 2>&1 | Out-String))
            try { $lines.Add((Get-ScheduledTask | Where-Object TaskPath -like '\Microsoft\Windows\EnterpriseMgmt\*' | Select-Object TaskPath,TaskName,State | Format-Table -AutoSize | Out-String)) } catch { $lines.Add('Tarefas: ' + $_.Exception.Message) }
            try { $lines.Add((Get-ChildItem -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Enrollments' | Select-Object -ExpandProperty PSChildName | Out-String)) } catch { $lines.Add('Inscrições: ' + $_.Exception.Message) }
            $lines.Add('Nenhuma inscrição, associação ou tarefa foi removida. Este relatório pode conter identificadores corporativos; revise antes de compartilhar.')
            $lines -join "`r`n"
        }
        default { throw 'Modo de execução desconhecido.' }
    }
} catch { $script:ExitCode = 1; $script:ResultStatus = 'ERROR'; $detail = $_.Exception.Message }
if ($outputFolder -and (Test-Path -LiteralPath $outputFolder -PathType Container)) {
    $detail | Set-Content -LiteralPath (Join-Path $outputFolder 'relatorio.txt') -Encoding UTF8
    $safe = [System.Net.WebUtility]::HtmlEncode([string]$detail)
    '<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>Frankenstein TI · relatório</title><style>body{background:#0d1319;color:#ebf2f2;font:16px Segoe UI,sans-serif;max-width:960px;margin:48px auto;padding:24px}h1{color:#99edbe}pre{white-space:pre-wrap;overflow-wrap:anywhere;line-height:1.6;background:#161f27;padding:24px;border:1px solid #2b3943}</style><h1>Frankenstein TI</h1><p>Relatório local de execução</p><pre>' + $safe + '</pre></html>' | Set-Content -LiteralPath (Join-Path $outputFolder 'relatorio.html') -Encoding UTF8
    @{ Status = $script:ResultStatus; Detail = [string]$detail } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputFolder 'resultado.json') -Encoding UTF8
}
exit $script:ExitCode
