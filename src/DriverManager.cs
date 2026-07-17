using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;

namespace CodexPerformanceOptimizer
{
    internal static class DriverManager
    {
        private const int SearchTimeout = 10 * 60 * 1000;
        private const int InstallTimeout = 60 * 60 * 1000;

        public static List<DriverUpdate> SearchUpdates(CancellationToken token)
        {
            return SearchUpdates(token, new Progress<string>());
        }

        public static List<DriverUpdate> SearchUpdates(CancellationToken token, IProgress<string> progress)
        {
            progress.Report("Consultando drivers no Windows Update...");
            const string script = @"$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Stop'; $b={param($v) [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$v))}; $session=New-Object -ComObject Microsoft.Update.Session; $searcher=$session.CreateUpdateSearcher(); $result=$searcher.Search(""IsInstalled=0 and Type='Driver' and IsHidden=0""); $lines=@(); foreach($u in $result.Updates){ $lines += ((&$b $u.Title)+'|'+(&$b $u.DriverManufacturer)+'|'+[string]$u.Identity.UpdateID+'|'+[string]$u.MaxDownloadSize+'|'+[string]$u.RebootRequired) }; $lines -join ""`r`n""";
            CommandExecution result = RunPowerShell(script, SearchTimeout, token);
            if (result.ExitCode != 0) throw new InvalidOperationException("O Windows Update não concluiu a busca. " + CompactError(result.Output));
            var updates = new List<DriverUpdate>();
            foreach (string line in (result.Output ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = line.Trim().Split('|');
                long bytes;
                bool reboot;
                if (fields.Length != 5 || !long.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out bytes) || !bool.TryParse(fields[4], out reboot) || !IsValidUpdateId(fields[2])) continue;
                updates.Add(new DriverUpdate { Selected = true, Title = DecodeField(fields[0]), Provider = DecodeField(fields[1]), UpdateId = fields[2], DownloadBytes = bytes, RebootRequired = reboot });
            }
            return updates.OrderBy(delegate(DriverUpdate item) { return item.Title; }).ToList();
        }

        public static string InstallUpdates(IEnumerable<string> updateIds, CancellationToken token, IProgress<string> progress)
        {
            if (!Optimizer.IsAdministrator()) return "A atualização de drivers exige privilégios de administrador. Use 'Executar como admin'.";
            string[] ids = (updateIds ?? Enumerable.Empty<string>()).Where(IsValidUpdateId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (ids.Length == 0) return "Nenhuma atualização de driver válida foi selecionada.";

            progress.Report("Preparando atualizações de drivers...");
            string wanted = string.Join(",", ids.Select(delegate(string id) { return "'" + id + "'"; }));
            string script = @"$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Stop'; $wanted=@(" + wanted + @"); $session=New-Object -ComObject Microsoft.Update.Session; $searcher=$session.CreateUpdateSearcher(); $result=$searcher.Search(""IsInstalled=0 and Type='Driver' and IsHidden=0""); $selected=New-Object -ComObject Microsoft.Update.UpdateColl; foreach($u in $result.Updates){ if($wanted -contains [string]$u.Identity.UpdateID){ if(-not $u.EulaAccepted){$u.AcceptEula()}; [void]$selected.Add($u) } }; if($selected.Count -eq 0){ throw 'As atualizações selecionadas não estão mais disponíveis.' }; try { Checkpoint-Computer -Description 'Antes de atualizar drivers pelo Otimizador' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop } catch {}; $downloader=$session.CreateUpdateDownloader(); $downloader.Updates=$selected; $download=$downloader.Download(); $ready=New-Object -ComObject Microsoft.Update.UpdateColl; foreach($u in $selected){ if($u.IsDownloaded){[void]$ready.Add($u)} }; if($ready.Count -eq 0){ throw 'Nenhum driver foi baixado pelo Windows Update.' }; $installer=$session.CreateUpdateInstaller(); $installer.Updates=$ready; $install=$installer.Install(); $lines=@('ATUALIZAÇÃO DE DRIVERS','========================================================================','Origem: Windows Update','Drivers selecionados: '+$selected.Count,'Drivers baixados: '+$ready.Count,'Resultado geral: '+$install.ResultCode,'Reinicialização necessária: '+$(if($install.RebootRequired){'sim'}else{'não'})); for($i=0;$i -lt $ready.Count;$i++){ $item=$install.GetUpdateResult($i); $lines += $ready.Item($i).Title+' | resultado '+$item.ResultCode+' | HRESULT 0x'+('{0:X8}' -f ($item.HResult -band 0xffffffffL)) }; $lines -join ""`r`n""";
            CommandExecution result = RunPowerShell(script, InstallTimeout, token);
            if (result.ExitCode != 0) return "ATUALIZAÇÃO DE DRIVERS\r\n" + new string('=', 72) + "\r\nFalha: " + CompactError(result.Output);
            return (result.Output ?? string.Empty).Trim();
        }

        public static int CountInstalledDrivers()
        {
            try
            {
                int count = 0;
                using (var searcher = new ManagementObjectSearcher("SELECT DeviceName FROM Win32_PnPSignedDriver WHERE DeviceName IS NOT NULL"))
                using (ManagementObjectCollection results = searcher.Get()) foreach (ManagementObject ignored in results) count++;
                return count;
            }
            catch { return 0; }
        }

        public static string BuildSearchReport(ICollection<DriverUpdate> updates)
        {
            var report = new StringBuilder("VERIFICAÇÃO DE DRIVERS\r\n" + new string('=', 72) + "\r\n");
            report.AppendLine("Origem: Windows Update");
            report.AppendLine("Atualizações encontradas: " + (updates == null ? 0 : updates.Count));
            if (updates != null) foreach (DriverUpdate update in updates) report.AppendLine("• " + update.Title);
            return report.ToString();
        }

        public static void OpenWindowsUpdate()
        {
            Process.Start(new ProcessStartInfo("ms-settings:windowsupdate-optionalupdates") { UseShellExecute = true });
        }

        internal static bool IsValidUpdateIdForTesting(string value)
        {
            return IsValidUpdateId(value);
        }

        private static bool IsValidUpdateId(string value)
        {
            Guid parsed;
            return Guid.TryParse(value, out parsed);
        }

        private static CommandExecution RunPowerShell(string script, int timeout, CancellationToken token)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            return SystemCommand.Execute("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, timeout, token);
        }

        private static string CompactError(string output)
        {
            string value = (output ?? string.Empty).Trim().Replace("\r", " ").Replace("\n", " ");
            while (value.Contains("  ")) value = value.Replace("  ", " ");
            if (value.Length > 500) value = value.Substring(0, 500) + "...";
            return string.IsNullOrEmpty(value) ? "erro não informado." : value;
        }

        private static string DecodeField(string value)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
            catch { return string.Empty; }
        }
    }
}
