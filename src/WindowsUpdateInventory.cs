using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace CodexPerformanceOptimizer
{
    internal static class WindowsUpdateInventory
    {
        private const int SearchTimeout = 10 * 60 * 1000;

        public static List<WindowsSystemUpdate> Search(CancellationToken token, IProgress<string> progress)
        {
            progress.Report("Consultando atualizações do Windows...");
            const string script = @"$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Stop'; $b={param($v) [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$v))}; $session=New-Object -ComObject Microsoft.Update.Session; $searcher=$session.CreateUpdateSearcher(); $result=$searcher.Search(""IsInstalled=0 and Type='Software' and IsHidden=0""); $lines=@(); foreach($u in $result.Updates){ $lines += ((&$b $u.Title)+'|'+[string]$u.Identity.UpdateID+'|'+[string]$u.MaxDownloadSize+'|'+[string]$u.IsMandatory+'|'+[string]$u.RebootRequired) }; $lines -join ""`r`n""";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            CommandExecution result = SystemCommand.Execute("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, SearchTimeout, token);
            if (result.ExitCode != 0) throw new InvalidOperationException("O Windows Update não concluiu a consulta. " + Compact(result.Output));
            return Parse(result.Output);
        }

        internal static List<WindowsSystemUpdate> ParseForTesting(string output)
        {
            return Parse(output);
        }

        private static List<WindowsSystemUpdate> Parse(string output)
        {
            var updates = new List<WindowsSystemUpdate>();
            foreach (string line in (output ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = line.Trim().Split('|');
                long bytes;
                bool mandatory;
                bool reboot;
                if (fields.Length != 5 || !long.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out bytes)) continue;
                if (!bool.TryParse(fields[3], out mandatory)) mandatory = false;
                if (!bool.TryParse(fields[4], out reboot)) reboot = false;
                string title;
                try { title = Encoding.UTF8.GetString(Convert.FromBase64String(fields[0])); }
                catch { continue; }
                updates.Add(new WindowsSystemUpdate { Title = title, UpdateId = fields[1], DownloadBytes = bytes, Mandatory = mandatory, RebootRequired = reboot });
            }
            return updates.OrderByDescending(item => item.Mandatory).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private static string Compact(string value)
        {
            string text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length > 280 ? text.Substring(0, 280) + "..." : text;
        }
    }
}
