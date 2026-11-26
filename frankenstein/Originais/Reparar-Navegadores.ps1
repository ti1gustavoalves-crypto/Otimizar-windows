[CmdletBinding()]
param(
    [switch]$SkipWindowsRepair,
    [int]$LaunchTimeoutSeconds = 5
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

# Solicita elevação porque DISM/SFC e alguns logs exigem administrador.
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    try {
        $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $PSCommandPath))
        if ($SkipWindowsRepair) { $arguments += '-SkipWindowsRepair' }
        $arguments += @('-LaunchTimeoutSeconds', $LaunchTimeoutSeconds)
        Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments
    } catch {
        Write-Host "Nao foi possivel solicitar permissao de administrador: $($_.Exception.Message)" -ForegroundColor Red
        Read-Host 'Pressione ENTER para sair'
    }
    exit
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$desktop = [Environment]::GetFolderPath('Desktop')
if ([string]::IsNullOrWhiteSpace($desktop) -or -not (Test-Path -LiteralPath $desktop)) {
    $desktop = $PSScriptRoot
}
$reportPath = Join-Path $desktop "Relatorio-Navegadores-$stamp.html"
$logPath = Join-Path $desktop "Relatorio-Navegadores-$stamp.log"
$jsonPath = Join-Path $desktop "Relatorio-Navegadores-$stamp.json"
$backupRoot = Join-Path $env:ProgramData "BrowserRepair\Backup-$stamp"
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null

$script:TestMarker = "BrowserRepair-$stamp"
$script:TestPage = Join-Path $env:TEMP "$($script:TestMarker).html"
$testHtml = '<!doctype html><html><head><meta charset="utf-8"><title>' + $script:TestMarker + '</title></head><body><h1>Teste de inicializacao concluido</h1><p>Esta pagina local confirma que o navegador abriu e carregou conteudo.</p></body></html>'
Set-Content -LiteralPath $script:TestPage -Value $testHtml -Encoding UTF8
$script:TestPageUri = ([uri]$script:TestPage).AbsoluteUri

$script:Rows = [System.Collections.Generic.List[object]]::new()
$script:BrowserTests = [System.Collections.Generic.List[object]]::new()
$script:Details = [System.Collections.Generic.List[string]]::new()

function Add-Result {
    param([string]$Etapa, [string]$Status, [string]$Detalhe)
    $script:Rows.Add([pscustomobject]@{
        Horario = (Get-Date -Format 'HH:mm:ss')
        Etapa = $Etapa
        Status = $Status
        Detalhe = $Detalhe
    })
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [$Status] $Etapa - $Detalhe"
    Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
    $color = switch ($Status) { 'OK' { 'Green' } 'FALHA' { 'Red' } 'ALERTA' { 'Yellow' } default { 'Gray' } }
    Write-Host $line -ForegroundColor $color
}

function Add-Detail {
    param([string]$Text)
    if (-not [string]::IsNullOrWhiteSpace($Text)) { $script:Details.Add($Text) }
}

function Get-Browsers {
    $programFilesX86 = ${env:ProgramFiles(x86)}
    $candidates = @(
        @{ Name='Microsoft Edge'; Process='msedge'; Kind='Chromium'; Path=(Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe') },
        @{ Name='Microsoft Edge'; Process='msedge'; Kind='Chromium'; Path=(Join-Path $programFilesX86 'Microsoft\Edge\Application\msedge.exe') },
        @{ Name='Google Chrome'; Process='chrome'; Kind='Chromium'; Path=(Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe') },
        @{ Name='Google Chrome'; Process='chrome'; Kind='Chromium'; Path=(Join-Path $programFilesX86 'Google\Chrome\Application\chrome.exe') },
        @{ Name='Google Chrome'; Process='chrome'; Kind='Chromium'; Path=(Join-Path $env:LOCALAPPDATA 'Google\Chrome\Application\chrome.exe') },
        @{ Name='Mozilla Firefox'; Process='firefox'; Kind='Firefox'; Path=(Join-Path $env:ProgramFiles 'Mozilla Firefox\firefox.exe') },
        @{ Name='Mozilla Firefox'; Process='firefox'; Kind='Firefox'; Path=(Join-Path $programFilesX86 'Mozilla Firefox\firefox.exe') },
        @{ Name='Brave'; Process='brave'; Kind='Chromium'; Path=(Join-Path $env:ProgramFiles 'BraveSoftware\Brave-Browser\Application\brave.exe') },
        @{ Name='Brave'; Process='brave'; Kind='Chromium'; Path=(Join-Path $programFilesX86 'BraveSoftware\Brave-Browser\Application\brave.exe') },
        @{ Name='Brave'; Process='brave'; Kind='Chromium'; Path=(Join-Path $env:LOCALAPPDATA 'BraveSoftware\Brave-Browser\Application\brave.exe') },
        @{ Name='Opera'; Process='opera'; Kind='Chromium'; Path=(Join-Path $env:LOCALAPPDATA 'Programs\Opera\opera.exe') },
        @{ Name='Opera GX'; Process='opera'; Kind='Chromium'; Path=(Join-Path $env:LOCALAPPDATA 'Programs\Opera GX\opera.exe') }
    )
    $appPathRoots = @(
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths'
    )
    $knownExecutables = @{
        'msedge.exe'=@('Microsoft Edge','msedge','Chromium'); 'chrome.exe'=@('Google Chrome','chrome','Chromium')
        'firefox.exe'=@('Mozilla Firefox','firefox','Firefox'); 'brave.exe'=@('Brave','brave','Chromium')
        'opera.exe'=@('Opera','opera','Chromium')
    }
    foreach ($root in $appPathRoots) {
        foreach ($exeName in $knownExecutables.Keys) {
            $key = Join-Path $root $exeName
            if (Test-Path $key) {
                try {
                    $registeredPath = (Get-Item -Path $key -ErrorAction Stop).GetValue('')
                    if ($registeredPath) {
                        $meta = $knownExecutables[$exeName]
                        $candidates += @{ Name=$meta[0]; Process=$meta[1]; Kind=$meta[2]; Path=$registeredPath }
                    }
                } catch {}
            }
        }
    }
    $seen = @{}
    foreach ($candidate in $candidates) {
        if ($candidate.Path -and (Test-Path -LiteralPath $candidate.Path)) {
            $resolved = (Resolve-Path -LiteralPath $candidate.Path).Path
            if (-not $seen.ContainsKey($resolved.ToLowerInvariant())) {
                $seen[$resolved.ToLowerInvariant()] = $true
                $file = Get-Item -LiteralPath $resolved -ErrorAction SilentlyContinue
                $signature = Get-AuthenticodeSignature -LiteralPath $resolved -ErrorAction SilentlyContinue
                [pscustomobject]@{
                    Name=$candidate.Name; Process=$candidate.Process; Kind=$candidate.Kind; Path=$resolved
                    Version=$file.VersionInfo.FileVersion
                    Assinatura=$(if ($signature) { $signature.Status.ToString() } else { 'Nao verificada' })
                }
            }
        }
    }
}

function Get-FreshCrashDetail {
    param([pscustomobject]$Browser, [datetime]$Since)
    try {
        $exeName = [IO.Path]::GetFileName($Browser.Path)
        $event = Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=$Since; Id=@(1000,1001,1002)} -ErrorAction Stop |
            Where-Object { $_.Message -match [regex]::Escape($exeName) } |
            Sort-Object TimeCreated -Descending | Select-Object -First 1
        if ($event) {
            $compact = ($event.Message -replace '\s+', ' ').Trim()
            if ($compact.Length -gt 600) { $compact = $compact.Substring(0,600) + '...' }
            return " Evento novo $($event.Id): $compact"
        }
    } catch {}
    return ''
}

function Stop-BrowserProcess {
    param([pscustomobject]$Browser)
    Get-Process -Name $Browser.Process -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 700
}

function Test-BrowserLaunch {
    param(
        [pscustomobject]$Browser,
        [ValidateSet('Normal','SemGPU','PerfilTemporario')][string]$Mode,
        [string]$Phase
    )
    Stop-BrowserProcess $Browser
    $attemptStarted = Get-Date
    $tempProfile = Join-Path $env:TEMP ("BrowserRepair-{0}-{1}" -f $Browser.Process, [guid]::NewGuid().ToString('N'))
    $args = @()
    if ($Browser.Kind -eq 'Firefox') {
        switch ($Mode) {
            'Normal' { $args = @('-new-window', $script:TestPageUri) }
            'SemGPU' { $args = @('-safe-mode', '-new-window', $script:TestPageUri) }
            'PerfilTemporario' {
                New-Item -ItemType Directory -Path $tempProfile -Force | Out-Null
                $args = @('-no-remote', '-profile', ('"{0}"' -f $tempProfile), $script:TestPageUri)
            }
        }
    } else {
        switch ($Mode) {
            'Normal' { $args = @('--no-first-run', '--new-window', $script:TestPageUri) }
            'SemGPU' { $args = @('--no-first-run', '--disable-gpu', '--disable-extensions', '--new-window', $script:TestPageUri) }
            'PerfilTemporario' {
                New-Item -ItemType Directory -Path $tempProfile -Force | Out-Null
                $args = @(("--user-data-dir={0}" -f $tempProfile), '--no-first-run', '--disable-extensions', '--new-window', $script:TestPageUri)
            }
        }
    }

    $success = $false
    $detail = ''
    try {
        $started = Start-Process -FilePath $Browser.Path -ArgumentList $args -PassThru -ErrorAction Stop
        # Nao considera uma abertura momentanea como sucesso. Aguarda o periodo
        # completo e verifica somente depois se o processo e a janela sobreviveram.
        Start-Sleep -Seconds $LaunchTimeoutSeconds
        $running = @(Get-Process -Name $Browser.Process -ErrorAction SilentlyContinue)
        $withWindow = @($running | Where-Object { $_.MainWindowHandle -ne 0 })
        $success = ($withWindow.Count -gt 0)
        $pageLoaded = @($withWindow | Where-Object { $_.MainWindowTitle -like "*$($script:TestMarker)*" }).Count -gt 0
        if ($success) {
            if ($pageLoaded) {
                $detail = 'O processo e a janela permaneceram abertos; a pagina local de teste foi carregada.'
            } else {
                $detail = 'A janela permaneceu aberta, mas o titulo da pagina local nao foi confirmado (resultado parcial).'
            }
        } elseif ($running.Count -gt 0) {
            $detail = 'O processo permaneceu ativo, mas nenhuma janela principal foi detectada.'
        } else {
            $detail = "O processo e a janela nao permaneceram abertos por $LaunchTimeoutSeconds segundos."
        }
        if (-not $success) { $detail += Get-FreshCrashDetail $Browser $attemptStarted }
    } catch {
        $detail = "Falha ao executar: $($_.Exception.Message)"
        $pageLoaded = $false
    }

    $script:BrowserTests.Add([pscustomobject]@{
        Fase=$Phase; Navegador=$Browser.Name; Modo=$Mode
        Resultado=$(if ($success -and $pageLoaded) {'ABRIU E CARREGOU'} elseif ($success) {'ABRIU PARCIALMENTE'} else {'NAO ABRIU'})
        PaginaLocal=$(if ($pageLoaded) {'SIM'} else {'NAO'}); Detalhe=$detail
    })
    Add-Result "Teste: $($Browser.Name) / $Mode / $Phase" $(if ($success) {'OK'} else {'FALHA'}) $detail
    Stop-BrowserProcess $Browser
    if (Test-Path -LiteralPath $tempProfile) {
        Remove-Item -LiteralPath $tempProfile -Recurse -Force -ErrorAction SilentlyContinue
    }
    return $success
}

function Move-RecreatableItem {
    param([string]$Path, [string]$Label)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    try {
        $safeName = ($Label -replace '[^a-zA-Z0-9._-]', '_') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8)
        $destination = Join-Path $backupRoot $safeName
        Move-Item -LiteralPath $Path -Destination $destination -Force -ErrorAction Stop
        Add-Result "Arquivar cache/bloqueio: $Label" 'OK' "Movido para $destination"
    } catch {
        Add-Result "Arquivar cache/bloqueio: $Label" 'ALERTA' $_.Exception.Message
    }
}

function Repair-RecreatableBrowserData {
    $chromiumRoots = @(
        @{ Name='Edge'; Path=(Join-Path $env:LOCALAPPDATA 'Microsoft\Edge\User Data') },
        @{ Name='Chrome'; Path=(Join-Path $env:LOCALAPPDATA 'Google\Chrome\User Data') },
        @{ Name='Brave'; Path=(Join-Path $env:LOCALAPPDATA 'BraveSoftware\Brave-Browser\User Data') },
        @{ Name='Opera'; Path=(Join-Path $env:APPDATA 'Opera Software\Opera Stable') },
        @{ Name='OperaGX'; Path=(Join-Path $env:APPDATA 'Opera Software\Opera GX Stable') }
    )
    foreach ($root in $chromiumRoots) {
        if (-not (Test-Path -LiteralPath $root.Path)) { continue }
        foreach ($leaf in @('SingletonLock','SingletonSocket','SingletonCookie','ShaderCache','GrShaderCache','GraphiteDawnCache')) {
            Move-RecreatableItem (Join-Path $root.Path $leaf) "$($root.Name)-$leaf"
        }
        Get-ChildItem -LiteralPath $root.Path -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq 'Default' -or $_.Name -like 'Profile *' } |
            ForEach-Object {
                foreach ($leaf in @('GPUCache','DawnCache','Code Cache')) {
                    Move-RecreatableItem (Join-Path $_.FullName $leaf) "$($root.Name)-$($_.Name)-$leaf"
                }
            }
    }

    $firefoxProfiles = Join-Path $env:APPDATA 'Mozilla\Firefox\Profiles'
    if (Test-Path -LiteralPath $firefoxProfiles) {
        Get-ChildItem -LiteralPath $firefoxProfiles -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            Move-RecreatableItem (Join-Path $_.FullName 'parent.lock') "Firefox-$($_.Name)-parent.lock"
            Move-RecreatableItem (Join-Path $_.FullName 'startupCache') "Firefox-$($_.Name)-startupCache"
        }
    }
}

function Invoke-RepairCommand {
    param([string]$Name, [string]$FilePath, [string[]]$Arguments)
    try {
        Add-Result $Name 'INFO' 'Iniciado; esta etapa pode demorar.'
        $outputFile = Join-Path $env:TEMP ("BrowserRepair-{0}-{1}.txt" -f ($Name -replace '\W',''), [guid]::NewGuid().ToString('N'))
        $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -Wait -PassThru -NoNewWindow -RedirectStandardOutput $outputFile -RedirectStandardError ($outputFile + '.err')
        $output = @()
        if (Test-Path -LiteralPath $outputFile) { $output += Get-Content -LiteralPath $outputFile -ErrorAction SilentlyContinue }
        if (Test-Path -LiteralPath ($outputFile + '.err')) { $output += Get-Content -LiteralPath ($outputFile + '.err') -ErrorAction SilentlyContinue }
        Add-Detail ("{0}:`r`n{1}" -f $Name, ($output -join "`r`n"))
        Add-Result $Name $(if ($process.ExitCode -eq 0) {'OK'} else {'ALERTA'}) "Codigo de saida: $($process.ExitCode)"
        Remove-Item -LiteralPath $outputFile,($outputFile + '.err') -Force -ErrorAction SilentlyContinue
    } catch {
        Add-Result $Name 'FALHA' $_.Exception.Message
    }
}

function Collect-Diagnostics {
    try {
        $os = Get-CimInstance Win32_OperatingSystem
        $computer = Get-CimInstance Win32_ComputerSystem
        Add-Detail ("Computador: {0}`r`nUsuario: {1}`r`nWindows: {2} (build {3})`r`nRAM: {4:N1} GB" -f $env:COMPUTERNAME, $env:USERNAME, $os.Caption, $os.BuildNumber, ($computer.TotalPhysicalMemory/1GB))
    } catch { Add-Result 'Informacoes do sistema' 'ALERTA' $_.Exception.Message }

    try {
        $systemDrive = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$env:SystemDrive'"
        $freeGB = [math]::Round($systemDrive.FreeSpace/1GB, 2)
        Add-Result 'Espaco livre no disco do Windows' $(if ($freeGB -ge 5) {'OK'} else {'ALERTA'}) "$freeGB GB livres"
    } catch { Add-Result 'Espaco livre no disco do Windows' 'ALERTA' $_.Exception.Message }

    try {
        $tempTest = Join-Path $env:TEMP ("browser-repair-write-{0}.tmp" -f [guid]::NewGuid().ToString('N'))
        Set-Content -LiteralPath $tempTest -Value 'teste' -Encoding ASCII -ErrorAction Stop
        Remove-Item -LiteralPath $tempTest -Force -ErrorAction Stop
        Add-Result 'Permissao de escrita na pasta TEMP' 'OK' $env:TEMP
    } catch { Add-Result 'Permissao de escrita na pasta TEMP' 'FALHA' $_.Exception.Message }

    try {
        $eventIds = @(1000,1001,1002)
        $since = (Get-Date).AddDays(-3)
        $events = Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=$since; Id=$eventIds} -ErrorAction Stop |
            Where-Object { $_.Message -match '(?i)msedge|chrome|firefox|brave|opera' } | Select-Object -First 30
        foreach ($event in $events) {
            Add-Detail ("Evento {0} em {1}:`r`n{2}" -f $event.Id, $event.TimeCreated, $event.Message)
        }
        Add-Result 'Eventos recentes de falha dos navegadores' $(if ($events) {'ALERTA'} else {'OK'}) "Encontrados: $(@($events).Count)"
    } catch { Add-Result 'Eventos recentes de falha dos navegadores' 'ALERTA' $_.Exception.Message }

    foreach ($root in @('HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options','HKCU:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options')) {
        foreach ($exe in @('msedge.exe','chrome.exe','firefox.exe','brave.exe','opera.exe')) {
            $key = Join-Path $root $exe
            if (Test-Path $key) {
                $debugger = (Get-ItemProperty -Path $key -Name Debugger -ErrorAction SilentlyContinue).Debugger
                if ($debugger) {
                    Add-Result "IFEO/Debugger para $exe" 'ALERTA' "Existe redirecionamento de execucao: $debugger. Nao foi removido automaticamente."
                }
            }
        }
    }

    $policyRoots = @(
        'HKLM:\SOFTWARE\Policies\Microsoft\Edge', 'HKCU:\SOFTWARE\Policies\Microsoft\Edge',
        'HKLM:\SOFTWARE\Policies\Google\Chrome', 'HKCU:\SOFTWARE\Policies\Google\Chrome',
        'HKLM:\SOFTWARE\Policies\Mozilla\Firefox', 'HKCU:\SOFTWARE\Policies\Mozilla\Firefox'
    )
    foreach ($policyRoot in $policyRoots) {
        if (Test-Path $policyRoot) {
            try { Add-Detail ("Politicas em {0}:`r`n{1}" -f $policyRoot, ((Get-ItemProperty $policyRoot | Format-List | Out-String).Trim())) } catch {}
        }
    }

    try {
        $applocker = Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-AppLocker/EXE and DLL'; StartTime=(Get-Date).AddDays(-3)} -ErrorAction Stop |
            Where-Object { $_.Message -match '(?i)msedge|chrome|firefox|brave|opera' } | Select-Object -First 20
        foreach ($event in $applocker) { Add-Detail ("AppLocker {0} em {1}:`r`n{2}" -f $event.Id, $event.TimeCreated, $event.Message) }
        if ($applocker) { Add-Result 'Bloqueios do AppLocker' 'ALERTA' "Encontrados: $(@($applocker).Count)" }
    } catch {}
}

function Write-Report {
    param([object[]]$Browsers)
    $encoder = [System.Net.WebUtility]
    $machine = $encoder::HtmlEncode($env:COMPUTERNAME)
    $rowsHtml = ($script:Rows | ConvertTo-Html -Fragment)
    $testsHtml = if ($script:BrowserTests.Count) { $script:BrowserTests | ConvertTo-Html -Fragment } else { '<p>Nenhum navegador encontrado para teste.</p>' }
    $finalTests = @($script:BrowserTests | Where-Object { $_.Fase -eq 'Depois das correcoes' })
    $finalHtml = if ($finalTests.Count) { $finalTests | ConvertTo-Html -Fragment } else { '<p>Nao houve teste final.</p>' }
    $browserHtml = if ($Browsers.Count) { $Browsers | Select-Object Name,Version,Assinatura,Path | ConvertTo-Html -Fragment } else { '<p>Nenhuma instalacao reconhecida foi encontrada.</p>' }
    $detailsHtml = foreach ($detail in $script:Details) { '<pre>' + $encoder::HtmlEncode($detail) + '</pre>' }
    $opened = @($script:BrowserTests | Where-Object Resultado -eq 'ABRIU E CARREGOU').Count
    $partial = @($script:BrowserTests | Where-Object Resultado -eq 'ABRIU PARCIALMENTE').Count
    $failed = @($script:BrowserTests | Where-Object Resultado -eq 'NAO ABRIU').Count
    $summaryClass = if ($opened -gt 0) { 'good' } else { 'bad' }
    $recommendations = @()
    if (($opened + $partial) -eq 0) { $recommendations += 'Nenhum navegador criou uma janela. Encaminhe este relatorio ao suporte de TI e verifique o modulo com falha nos eventos.' }
    if (@($finalTests | Where-Object Resultado -eq 'ABRIU E CARREGOU').Count) { $recommendations += 'Ao menos um navegador abriu corretamente no teste final, depois dos reparos.' }
    if (@($finalTests | Where-Object Resultado -eq 'NAO ABRIU').Count) { $recommendations += 'Ainda ha falha no teste final. Use os eventos novos e o modulo com falha registrados abaixo para direcionar a correcao.' }
    if (@($script:BrowserTests | Where-Object { $_.Modo -eq 'SemGPU' -and $_.Resultado -eq 'ABRIU E CARREGOU' }).Count) { $recommendations += 'O navegador abriu sem GPU/extensoes: atualize ou reverta o driver de video e revise extensoes.' }
    if (@($script:BrowserTests | Where-Object { $_.Modo -eq 'PerfilTemporario' -and $_.Resultado -eq 'ABRIU E CARREGOU' }).Count) { $recommendations += 'O perfil temporario abriu: o perfil normal provavelmente esta corrompido. Exporte dados e recrie o perfil com acompanhamento do TI.' }
    if (@($script:Rows | Where-Object { $_.Etapa -like 'IFEO*' -or $_.Etapa -like '*AppLocker*' }).Count) { $recommendations += 'Foi encontrada uma possivel regra de redirecionamento/bloqueio. O TI deve validar antes de remove-la.' }
    if (-not $recommendations.Count) { $recommendations += 'Os testes encontraram ao menos uma forma funcional de inicializacao. Consulte a tabela para saber qual modo funcionou.' }
    $recommendationsHtml = '<ul>' + (($recommendations | ForEach-Object { '<li>' + $encoder::HtmlEncode($_) + '</li>' }) -join '') + '</ul>'

    $html = @"
<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><title>Relatorio de reparo de navegadores</title>
<style>body{font-family:Segoe UI,Arial,sans-serif;margin:32px;color:#17202a}h1{margin-bottom:4px}h2{margin-top:28px;border-bottom:1px solid #ccd1d1;padding-bottom:6px}.card{padding:16px;border-radius:8px;background:#f4f6f7}.good{border-left:6px solid #239b56}.bad{border-left:6px solid #c0392b}table{border-collapse:collapse;width:100%;font-size:13px}th,td{border:1px solid #ccd1d1;padding:7px;text-align:left;vertical-align:top}th{background:#eaecee}pre{white-space:pre-wrap;word-break:break-word;background:#f7f9f9;padding:12px;border:1px solid #ddd}small{color:#566573}</style></head><body>
<h1>Reparo e diagnostico de navegadores</h1><small>Computador: $machine | Gerado em $(Get-Date -Format 'dd/MM/yyyy HH:mm:ss')</small>
<div class="card $summaryClass"><strong>Resumo:</strong> $opened teste(s) abriram e carregaram a pagina; $partial abriram parcialmente; $failed falharam.</div>
<h2>Recomendacoes</h2>$recommendationsHtml
<h2>Navegadores encontrados</h2>$browserHtml
<h2>Estado final apos os reparos</h2>$finalHtml
<h2>Testes de inicializacao</h2>$testsHtml
<h2>Etapas executadas</h2>$rowsHtml
<h2>Dados tecnicos e eventos</h2>$($detailsHtml -join "`r`n")
<p><small>Backups de caches recriaveis: $($encoder::HtmlEncode($backupRoot)). Nenhum favorito, senha, cookie ou perfil foi apagado.</small></p>
</body></html>
"@
    Set-Content -LiteralPath $reportPath -Value $html -Encoding UTF8
    $jsonData = [ordered]@{
        GeradoEm=(Get-Date -Format 'o'); Computador=$env:COMPUTERNAME; Usuario=$env:USERNAME
        Navegadores=$Browsers; Testes=@($script:BrowserTests); Etapas=@($script:Rows); Detalhes=@($script:Details)
        RelatorioHtml=$reportPath; Log=$logPath; Backup=$backupRoot
    }
    $jsonData | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
}

Clear-Host
Write-Host 'REPARO E DIAGNOSTICO DE NAVEGADORES' -ForegroundColor Cyan
Write-Host 'Feche documentos importantes. Navegadores abertos serao encerrados durante os testes.'
Write-Host "Relatorio final: $reportPath"
Write-Host ''

Add-Result 'Inicio' 'INFO' 'Execucao elevada iniciada.'
Collect-Diagnostics
$browsers = @(Get-Browsers)
if (-not $browsers.Count) {
    Add-Result 'Localizacao de navegadores' 'FALHA' 'Nenhum Edge, Chrome, Firefox, Brave ou Opera foi encontrado nos locais conhecidos.'
} else {
    Add-Result 'Localizacao de navegadores' 'OK' ("Encontrados: " + (($browsers.Name | Select-Object -Unique) -join ', '))
}

# As correcoes sempre sao aplicadas antes de qualquer tentativa de abertura.
foreach ($browser in $browsers) { Stop-BrowserProcess $browser }
Repair-RecreatableBrowserData

if (-not $SkipWindowsRepair) {
    Invoke-RepairCommand 'DISM - reparar imagem do Windows' 'dism.exe' @('/Online','/Cleanup-Image','/RestoreHealth')
    Invoke-RepairCommand 'SFC - verificar arquivos do Windows' 'sfc.exe' @('/scannow')
} else {
    Add-Result 'DISM/SFC' 'INFO' 'Ignorado por opcao de linha de comando.'
}

foreach ($browser in $browsers) {
    [void](Test-BrowserLaunch $browser 'Normal' 'Depois das correcoes')
    [void](Test-BrowserLaunch $browser 'SemGPU' 'Depois das correcoes')
    [void](Test-BrowserLaunch $browser 'PerfilTemporario' 'Depois das correcoes')
}

try {
    $pendingReboot = (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') -or
                     (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired')
    Add-Result 'Reinicio pendente' $(if ($pendingReboot) {'ALERTA'} else {'OK'}) $(if ($pendingReboot) {'Reinicie o computador e teste novamente.'} else {'Nenhum sinal comum de reinicio pendente.'})
} catch {}

Write-Report $browsers
Add-Result 'Relatorio' 'OK' $reportPath
Write-Report $browsers
Remove-Item -LiteralPath $script:TestPage -Force -ErrorAction SilentlyContinue

try { Start-Process -FilePath $reportPath } catch {}
Write-Host ''
Write-Host 'Concluido. O relatorio foi aberto e salvo na Area de Trabalho.' -ForegroundColor Cyan
Read-Host 'Pressione ENTER para sair'
