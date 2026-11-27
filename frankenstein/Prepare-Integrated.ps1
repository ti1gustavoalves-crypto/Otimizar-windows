param([string]$OptimizerSource,[string]$Destination)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
foreach($file in Get-ChildItem -LiteralPath $OptimizerSource -Filter '*.cs' | Where-Object Name -notin @('Installer.cs','V2SelfTest.cs','MainForm.Operations.cs')) {
    $source=[IO.File]::ReadAllText($file.FullName)
    $source=[regex]::Replace($source,'(?m)^\[assembly:.*\]\s*$','')
    if($file.Name -eq 'UiComponents.cs'){$source=$source.Replace('static readonly Color','static Color')}
    if($file.Name -eq 'PerformanceOptimizerV2.cs'){$source=$source.Replace('readonly bool _suppressStartup','bool _suppressStartup').Replace('if (_suppressStartup) return;','if (_suppressStartup || !TopLevel) return;')}
    if($file.Name -eq 'PerformanceOptimizerV2.cs'){$source=$source.Replace('_advancedSettings = AdvancedEngine.ReadSettings();','_advancedSettings = AdvancedEngine.ReadSettings(); _advancedSettings.AutomaticPowerProfiles=false; _advancedSettings.MinimizeToTray=false;')}
    if($file.Name -eq 'MainForm.Control.cs'){$source=$source.Replace('check.Click += async delegate { await CheckForUpdates(); };','check.Text="Atualização na barra lateral"; check.Enabled=false;')}
    if($file.Name -eq 'MainForm.Control.cs'){$source=$source.Replace('restoreQuarantine.Click += delegate { MessageBox.Show(this, AdvancedEngine.RestoreLatestQuarantine(), "Quarentena"); };','restoreQuarantine.Click += async delegate { string result = await RunWork("Restaurando quarentena...", delegate(System.Threading.CancellationToken t, System.IProgress<string> p) { return AdvancedEngine.RestoreLatestQuarantine(); }); MessageBox.Show(this, result, "Quarentena"); };')}
    # Never associate reliability counters to drives by enumeration order.
    if($file.Name -eq 'AdvancedFeatures.cs'){
        $begin=$source.IndexOf('            try', $source.IndexOf('            catch { }', $source.IndexOf('public static List<DiskDiagnostic> ReadDiskDiagnostics()')))
        $end=$source.IndexOf('            return disks;', $begin)
        if($begin -lt 0 -or $end -lt $begin){throw 'Estrutura do diagnostico de discos mudou; revise o adaptador.'}
        $source=$source.Remove($begin,$end-$begin)
    }
    if($file.Name -eq 'AppPaths.cs'){$source=[regex]::Replace($source,'private static readonly string Root = Portable[\s\S]*?;','private static readonly string Root = Path.Combine(FrankensteinTI.Distribution.DataRoot, "Otimizador");')}
    # Restart the whole application when elevation is needed, never close only its embedded workspace.
    if($file.Name -in @('MainForm.Guided.cs','MainForm.Integrity.cs','PerformanceOptimizerV2.cs')) {
        $source=$source.Replace('Close();','if (TopLevel) Close(); else Application.Exit();')
        $source=$source.Replace('Reabrir uma única vez com o plano preservado?','Reabrir como administrador? Revise o plano novamente após a reabertura.')
    }
    [IO.File]::WriteAllText((Join-Path $Destination $file.Name),$source,(New-Object Text.UTF8Encoding($true)))
}
