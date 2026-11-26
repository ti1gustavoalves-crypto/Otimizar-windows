#requires -Version 5.1
<#
.SYNOPSIS
  Diagnostica aplicativos que fecham sozinhos e, opcionalmente, executa reparos seguros.

.EXAMPLE
  powershell.exe -ExecutionPolicy Bypass -File .\Diagnosticar-Reparar-Apps.ps1

.EXAMPLE
  powershell.exe -ExecutionPolicy Bypass -File .\Diagnosticar-Reparar-Apps.ps1 -Modo Reparar
#>

[CmdletBinding()]
param(
    [ValidateSet('Diagnosticar', 'Reparar')]
    [string]$Modo = 'Diagnosticar',

    [ValidateRange(1, 30)]
    [int]$Dias = 7,

    [switch]$NaoRepararAppsWindows,

    [string]$PastaSaida,
    [switch]$SemPausa
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

function Testar-Administrador {
    $identidade = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identidade)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Iniciar-ComoAdministrador {
    $argumentos = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', ('"{0}"' -f $PSCommandPath),
        '-Modo', $Modo,
        '-Dias', $Dias
    )
    if ($NaoRepararAppsWindows) { $argumentos += '-NaoRepararAppsWindows' }
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $argumentos
}

if ($Modo -eq 'Reparar' -and -not (Testar-Administrador)) {
    Write-Host 'O modo Reparar precisa de permissao de administrador.' -ForegroundColor Yellow
    Write-Host 'Confirme a janela do Controle de Conta de Usuario (UAC).'
    throw "O reparo exige administrador."
}

$falhaFatal = $false
$carimbo = Get-Date -Format 'yyyyMMdd-HHmmss'
$pastaBase = $PastaSaida
if (-not $pastaBase) { throw "Pasta de saída obrigatória." }
New-Item -ItemType Directory -Path $pastaBase -Force | Out-Null
$arquivoLog = Join-Path $pastaBase 'execucao.log'
$arquivoResumo = Join-Path $pastaBase 'RESUMO.txt'
$arquivoEventos = Join-Path $pastaBase 'falhas-de-aplicativos.csv'
$arquivoSistema = Join-Path $pastaBase 'informacoes-do-sistema.txt'
$achados = New-Object System.Collections.Generic.List[string]
$acoes = New-Object System.Collections.Generic.List[string]

Start-Transcript -Path $arquivoLog -Force | Out-Null

function Adicionar-Achado([string]$Texto) {
    $achados.Add($Texto)
    Write-Host "[ATENCAO] $Texto" -ForegroundColor Yellow
}

function Adicionar-Acao([string]$Texto) {
    $acoes.Add($Texto)
    Write-Host "[OK] $Texto" -ForegroundColor Green
}

function Executar-ComandoNativo {
    param(
        [Parameter(Mandatory)] [string]$Programa,
        [Parameter(Mandatory)] [string[]]$Argumentos,
        [Parameter(Mandatory)] [string]$Descricao
    )

    Write-Host "`n== $Descricao ==" -ForegroundColor Cyan
    & $Programa @Argumentos 2>&1 | Tee-Object -FilePath $arquivoLog -Append
    $codigo = $LASTEXITCODE
    if ($codigo -in @(0, 1, 2, 3, 3010)) {
        Adicionar-Acao "$Descricao concluido (codigo $codigo)."
    } else {
        Adicionar-Achado "$Descricao retornou o codigo $codigo. Consulte execucao.log."
    }
    return $codigo
}

try {
    Write-Host "Diagnostico de aplicativos - modo $Modo" -ForegroundColor Cyan
    Write-Host "Os relatorios serao salvos em: $pastaBase"

    # Informacoes basicas, sem coletar documentos ou senhas.
    $os = Get-CimInstance Win32_OperatingSystem
    $computador = Get-CimInstance Win32_ComputerSystem
    $bios = Get-CimInstance Win32_BIOS
    $unidadeSistema = Get-CimInstance Win32_LogicalDisk -Filter ("DeviceID='{0}'" -f $env:SystemDrive)
    @(
        "Data: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        "Modo: $Modo"
        "Windows: $($os.Caption) $($os.Version) build $($os.BuildNumber)"
        "Fabricante/modelo: $($computador.Manufacturer) / $($computador.Model)"
        "BIOS: $($bios.SMBIOSBIOSVersion)"
        "RAM total (GB): $([math]::Round($computador.TotalPhysicalMemory / 1GB, 1))"
        "Disco do sistema livre (GB): $([math]::Round($unidadeSistema.FreeSpace / 1GB, 1))"
        "PowerShell: $($PSVersionTable.PSVersion)"
    ) | Set-Content -Path $arquivoSistema -Encoding UTF8

    if ($unidadeSistema.FreeSpace -lt 10GB) {
        Adicionar-Achado "Ha menos de 10 GB livres em $env:SystemDrive. Pouco espaco pode fazer aplicativos fecharem."
    }

    # Falhas recentes: Application Error, Windows Error Reporting e .NET Runtime.
    Write-Host "`n== Coletando falhas dos ultimos $Dias dia(s) ==" -ForegroundColor Cyan
    $inicio = (Get-Date).AddDays(-$Dias)
    $provedores = @('Application Error', 'Windows Error Reporting', '.NET Runtime', 'Application Hang')
    $eventos = @(Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $inicio } -ErrorAction SilentlyContinue |
        Where-Object { $_.ProviderName -in $provedores -and $_.LevelDisplayName -in @('Error', 'Erro', 'Critical', 'Critico') } |
        Select-Object TimeCreated, ProviderName, Id, LevelDisplayName,
            @{Name='Mensagem'; Expression={ ($_.Message -replace "`r?`n", ' ') }})

    $eventos | Export-Csv -Path $arquivoEventos -NoTypeInformation -Encoding UTF8
    if ($eventos.Count -gt 0) {
        Adicionar-Achado "Foram encontradas $($eventos.Count) falhas recentes. Veja falhas-de-aplicativos.csv."
        $nomesComuns = $eventos.Mensagem |
            ForEach-Object {
                if ($_ -match '(?i)(?:Faulting application name|Nome do aplicativo com falha):\s*([^,]+)') { $Matches[1].Trim() }
            } |
            Group-Object |
            Sort-Object Count -Descending |
            Select-Object -First 8
        foreach ($item in $nomesComuns) {
            $achados.Add("Aplicativo recorrente: $($item.Name) ($($item.Count) falha(s)).")
        }
    } else {
        Adicionar-Acao 'Nenhuma falha de aplicativo relevante foi encontrada no periodo consultado.'
    }

    # Detecta reinicializacao pendente, causa frequente de reparos incompletos.
    $chavesReinicio = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
    )
    if ($chavesReinicio | Where-Object { Test-Path $_ }) {
        Adicionar-Achado 'O Windows indica uma reinicializacao pendente. Reinicie o computador depois deste processo.'
    }

    # Servicos relacionados a temas, apps modernos e Office.
    Write-Host "`n== Verificando servicos importantes ==" -ForegroundColor Cyan
    $nomesServicos = @('Themes', 'AppXSvc', 'ClipSVC', 'StateRepository', 'InstallService', 'ClickToRunSvc')
    foreach ($nome in $nomesServicos) {
        $servico = Get-Service -Name $nome -ErrorAction SilentlyContinue
        if (-not $servico) { continue }
        Write-Host ("{0}: {1} ({2})" -f $servico.Name, $servico.Status, $servico.StartType)
        if ($Modo -eq 'Reparar' -and $servico.Status -ne 'Running' -and $servico.StartType -ne 'Disabled') {
            try {
                Start-Service -Name $nome -ErrorAction Stop
                Adicionar-Acao "Servico $nome iniciado."
            } catch {
                Adicionar-Achado "Nao foi possivel iniciar o servico ${nome}: $($_.Exception.Message)"
            }
        }
        if ($servico.StartType -eq 'Disabled') {
            Adicionar-Achado "O servico $nome esta desativado. Isso pode afetar temas ou aplicativos."
        }
    }

    # Descobre instalacoes Click-to-Run do Microsoft 365/Office.
    $officeConfig = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration' -ErrorAction SilentlyContinue
    if ($officeConfig) {
        Add-Content -Path $arquivoSistema -Encoding UTF8 -Value @(
            "Office Click-to-Run: detectado"
            "Office ProductReleaseIds: $($officeConfig.ProductReleaseIds)"
            "Office VersionToReport: $($officeConfig.VersionToReport)"
        )
        if ($Modo -eq 'Reparar') {
            $c2r = Get-Service ClickToRunSvc -ErrorAction SilentlyContinue
            if ($c2r -and $c2r.Status -eq 'Running') {
                try {
                    Restart-Service ClickToRunSvc -Force -ErrorAction Stop
                    Adicionar-Acao 'Servico do Microsoft Office Click-to-Run reiniciado.'
                } catch {
                    Adicionar-Achado "Nao foi possivel reiniciar o servico do Office: $($_.Exception.Message)"
                }
            }
        }
    }

    # Disco, imagem do Windows e arquivos protegidos do sistema.
    Executar-ComandoNativo -Programa 'chkdsk.exe' -Argumentos @($env:SystemDrive, '/scan') -Descricao 'Verificacao online do disco' | Out-Null
    if ($Modo -eq 'Diagnosticar') {
        Executar-ComandoNativo -Programa 'DISM.exe' -Argumentos @('/Online', '/Cleanup-Image', '/ScanHealth') -Descricao 'Diagnostico da imagem do Windows (DISM)' | Out-Null
        Executar-ComandoNativo -Programa 'sfc.exe' -Argumentos @('/verifyonly') -Descricao 'Verificacao dos arquivos do Windows (SFC)' | Out-Null
    } else {
        # Tenta criar um ponto de restauracao; pode estar desativado pelo Windows.
        try {
            Checkpoint-Computer -Description "Antes-Reparo-Apps-$carimbo" -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
            Adicionar-Acao 'Ponto de restauracao criado.'
        } catch {
            Adicionar-Achado 'Nao foi possivel criar ponto de restauracao (a Protecao do Sistema pode estar desativada).'
        }

        Executar-ComandoNativo -Programa 'DISM.exe' -Argumentos @('/Online', '/Cleanup-Image', '/RestoreHealth') -Descricao 'Reparo da imagem do Windows (DISM)' | Out-Null
        Executar-ComandoNativo -Programa 'sfc.exe' -Argumentos @('/scannow') -Descricao 'Reparo dos arquivos do Windows (SFC)' | Out-Null

        if (-not $NaoRepararAppsWindows) {
            Write-Host "`n== Registrando novamente os aplicativos do usuario ==" -ForegroundColor Cyan
            $pacotesOk = 0
            $pacotesErro = 0
            Get-AppxPackage -ErrorAction SilentlyContinue | ForEach-Object {
                $pacote = $_
                $manifesto = Join-Path $pacote.InstallLocation 'AppxManifest.xml'
                if (Test-Path $manifesto) {
                    try {
                        Add-AppxPackage -DisableDevelopmentMode -Register $manifesto -ErrorAction Stop
                        $pacotesOk++
                    } catch {
                        $pacotesErro++
                        Add-Content -Path $arquivoLog -Value "Falha AppX $($pacote.Name): $($_.Exception.Message)"
                    }
                }
            }
            Adicionar-Acao "$pacotesOk aplicativo(s) do Windows registrado(s) novamente."
            if ($pacotesErro -gt 0) {
                Adicionar-Achado "$pacotesErro aplicativo(s) nao puderam ser registrados; detalhes em execucao.log."
            }
        }
    }

    # Gera resumo acionavel.
    $linhasResumo = New-Object System.Collections.Generic.List[string]
    $linhasResumo.Add('DIAGNOSTICO DE APLICATIVOS DO WINDOWS')
    $linhasResumo.Add("Executado em: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    $linhasResumo.Add("Modo: $Modo")
    $linhasResumo.Add('')
    $linhasResumo.Add('ACHADOS')
    if ($achados.Count -eq 0) { $linhasResumo.Add('- Nenhum problema evidente foi encontrado.') }
    foreach ($item in $achados) { $linhasResumo.Add("- $item") }
    $linhasResumo.Add('')
    $linhasResumo.Add('ACOES EXECUTADAS')
    foreach ($item in $acoes) { $linhasResumo.Add("- $item") }
    $linhasResumo.Add('')
    $linhasResumo.Add('PROXIMOS PASSOS')
    if ($Modo -eq 'Diagnosticar') {
        $linhasResumo.Add('- Se houver corrupcao ou falhas, execute novamente com: -Modo Reparar')
    } else {
        $linhasResumo.Add('- Reinicie o computador e teste os mesmos aplicativos.')
    }
    if ($officeConfig) {
        $linhasResumo.Add('- Se somente o Office continuar fechando: Configuracoes > Aplicativos > Aplicativos instalados > Microsoft 365/Office > Modificar > Reparo Rapido.')
        $linhasResumo.Add('- Se o Reparo Rapido nao resolver, repita e escolha Reparo Online (requer internet e pode reinstalar o Office).')
    }
    $linhasResumo.Add('- Se a falha continuar, compartilhe RESUMO.txt e falhas-de-aplicativos.csv com o suporte. Revise o CSV antes, pois mensagens de erro podem conter caminhos e nomes de usuario.')
    $linhasResumo | Set-Content -Path $arquivoResumo -Encoding UTF8

    Write-Host "`nConcluido. Abra primeiro: $arquivoResumo" -ForegroundColor Green
} catch {
    $falhaFatal = $true
    Adicionar-Achado "Erro inesperado: $($_.Exception.Message)"
    $_ | Format-List * -Force | Out-String | Add-Content -Path $arquivoLog
    Write-Host "O script encontrou um erro. Consulte: $arquivoLog" -ForegroundColor Red
} finally {
    try { Stop-Transcript | Out-Null } catch { }
}

if (-not $SemPausa) {
    Write-Host 'Pressione ENTER para fechar.'
    [void](Read-Host)
}

@{ Status="REVIEW"; Detail="Rotina encerrada. Consulte RESUMO.txt, execucao.log e os eventos coletados; o encerramento não confirma a resolução das falhas." } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $pastaBase "resultado.json") -Encoding UTF8

if ($falhaFatal) { exit 1 }
