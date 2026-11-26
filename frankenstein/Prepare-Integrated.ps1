param([string]$OptimizerSource,[string]$Destination)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
foreach($file in Get-ChildItem -LiteralPath $OptimizerSource -Filter '*.cs' | Where-Object Name -notin @('Installer.cs','V2SelfTest.cs')) {
    $source=[IO.File]::ReadAllText($file.FullName)
    $source=[regex]::Replace($source,'(?m)^\[assembly:.*\]\s*$','')
    if($file.Name -eq 'UiComponents.cs'){$source=$source.Replace('static readonly Color','static Color')}
    if($file.Name -eq 'PerformanceOptimizerV2.cs'){$source=$source.Replace('_advancedSettings = AdvancedEngine.ReadSettings();','_advancedSettings = AdvancedEngine.ReadSettings(); _advancedSettings.AutomaticPowerProfiles=false; _advancedSettings.MinimizeToTray=false;')}
    if($file.Name -eq 'MainForm.Control.cs'){$source=$source.Replace('check.Click += async delegate { await CheckForUpdates(); };','check.Text="Atualização na barra lateral"; check.Enabled=false;')}
    if($file.Name -eq 'AppPaths.cs'){$source=[regex]::Replace($source,'private static readonly string Root = Portable[\s\S]*?;','private static readonly string Root = Path.Combine(FrankensteinTI.Distribution.DataRoot, "Otimizador");')}
    if($file.Name -eq 'MainForm.Operations.cs') {
        $source=$source.Replace('_cts = new CancellationTokenSource();','if (!FrankensteinTI.Session.Begin(initialStatus)) return "Outra operação está em andamento.";'+"`r`n"+'            _cts = new CancellationTokenSource();')
        $source=$source.Replace('return result;','FrankensteinTI.Session.End(_status.Text, result); return result;')
        $source=$source.Replace('_cts.Dispose();','FrankensteinTI.Session.End(_status.Text, "Consulte os relatórios de manutenção.");'+"`r`n"+'                _cts.Dispose();')
    }
    # Restart the whole application when elevation is needed, never close only its embedded workspace.
    if($file.Name -in @('MainForm.Guided.cs','MainForm.Integrity.cs','PerformanceOptimizerV2.cs')) {
        $source=$source.Replace('Close();','if (TopLevel) Close(); else Application.Exit();')
        $source=$source.Replace('Reabrir uma única vez com o plano preservado?','Reabrir como administrador? Revise o plano novamente após a reabertura.')
    }
    [IO.File]::WriteAllText((Join-Path $Destination $file.Name),$source,(New-Object Text.UTF8Encoding($true)))
}
