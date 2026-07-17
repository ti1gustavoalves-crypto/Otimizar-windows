using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CodexPerformanceOptimizer
{
    internal static class ProgramUpdater
    {
        private const int SearchTimeout = 5 * 60 * 1000;
        private const int UpdateTimeout = 45 * 60 * 1000;
        private static readonly Regex PackageIdPattern = new Regex(@"^[A-Za-z0-9][A-Za-z0-9._+\-]{1,255}$", RegexOptions.Compiled);
        private static readonly Regex AnsiPattern = new Regex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", RegexOptions.Compiled);

        public static bool IsAvailable()
        {
            CommandExecution result = SystemCommand.Execute("winget.exe", "--version", 15000);
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output);
        }

        public static string ReadVersion()
        {
            CommandExecution result = SystemCommand.Execute("winget.exe", "--version", 15000);
            return result.ExitCode == 0 ? (result.Output ?? string.Empty).Trim() : string.Empty;
        }

        public static List<ProgramUpdate> SearchUpdates(CancellationToken token, IProgress<string> progress)
        {
            progress.Report("Consultando programas pelo WinGet...");
            CommandExecution result = SystemCommand.Execute("winget.exe", "upgrade --source winget --accept-source-agreements --disable-interactivity", SearchTimeout, token);
            List<ProgramUpdate> updates = ParseUpgradeOutput(result.Output);
            if (updates.Count == 0 && result.ExitCode != 0)
                throw new InvalidOperationException("O WinGet não concluiu a consulta. " + CompactError(result.Output));
            return updates;
        }

        public static string InstallUpdates(IEnumerable<string> packageIds, CancellationToken token, IProgress<string> progress)
        {
            string[] ids = (packageIds ?? Enumerable.Empty<string>())
                .Where(IsValidPackageId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => string.Equals(id, "Microsoft.AppInstaller", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ToArray();
            if (ids.Length == 0) return "Nenhum programa válido foi selecionado.";

            var report = new StringBuilder();
            report.AppendLine("ATUALIZAÇÃO DE PROGRAMAS");
            report.AppendLine(new string('=', 72));
            report.AppendLine("Origem: Windows Package Manager (WinGet)");
            report.AppendLine("Selecionados: " + ids.Length);
            report.AppendLine();
            int succeeded = 0;
            for (int index = 0; index < ids.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                string id = ids[index];
                progress.Report("Atualizando " + (index + 1) + " de " + ids.Length + ": " + id);
                string arguments = "upgrade --id \"" + id + "\" --exact --source winget --silent --accept-package-agreements --accept-source-agreements --disable-interactivity";
                CommandExecution result = SystemCommand.Execute("winget.exe", arguments, UpdateTimeout, token);
                bool success = result.ExitCode == 0;
                if (success) succeeded++;
                report.AppendLine((success ? "✓ " : "✗ ") + id + " — " + (success ? "atualizado" : "não concluído"));
                if (!success) report.AppendLine("  " + CompactError(result.Output));
            }
            report.AppendLine();
            report.AppendLine("Concluídos: " + succeeded + " de " + ids.Length);
            if (succeeded < ids.Length) report.AppendLine("Os programas com falha foram mantidos na versão anterior.");
            return report.ToString().TrimEnd();
        }

        internal static List<ProgramUpdate> ParseUpgradeOutputForTesting(string output)
        {
            return ParseUpgradeOutput(output);
        }

        internal static bool IsValidPackageIdForTesting(string value)
        {
            return IsValidPackageId(value);
        }

        private static List<ProgramUpdate> ParseUpgradeOutput(string output)
        {
            var updates = new List<ProgramUpdate>();
            bool tableStarted = false;
            string previousLine = string.Empty;
            int[] columnStarts = new int[0];
            foreach (string rawLine in (output ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string line = AnsiPattern.Replace(rawLine, string.Empty).TrimEnd();
                if (!tableStarted)
                {
                    if (Regex.IsMatch(line.Trim(), @"^-{10,}$"))
                    {
                        columnStarts = FindColumnStarts(previousLine);
                        tableStarted = columnStarts.Length >= 4;
                    }
                    else if (!string.IsNullOrWhiteSpace(line)) previousLine = line;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] fields = SplitColumns(line, columnStarts);
                if (fields.Length < 4) continue;
                int last = fields.Length - 1;
                string source = fields.Length >= 5 ? fields[last] : "winget";
                string available = fields[fields.Length >= 5 ? last - 1 : last];
                string installed = fields[fields.Length >= 5 ? last - 2 : last - 1];
                string id = fields[fields.Length >= 5 ? last - 3 : last - 2];
                int nameParts = fields.Length >= 5 ? last - 3 : last - 2;
                string name = string.Join("  ", fields.Take(nameParts).ToArray());
                if (!IsValidPackageId(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(installed) || string.IsNullOrWhiteSpace(available)) continue;
                updates.Add(new ProgramUpdate { Selected = true, Name = name, PackageId = id, InstalledVersion = installed, AvailableVersion = available, Source = source });
            }
            return updates
                .GroupBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static int[] FindColumnStarts(string header)
        {
            var starts = new List<int>();
            for (int index = 0; index < (header ?? string.Empty).Length; index++)
            {
                if (char.IsWhiteSpace(header[index])) continue;
                if (index == 0 || (index >= 2 && char.IsWhiteSpace(header[index - 1]) && char.IsWhiteSpace(header[index - 2]))) starts.Add(index);
            }
            return starts.ToArray();
        }

        private static string[] SplitColumns(string line, int[] starts)
        {
            if (starts == null || starts.Length < 4) return Regex.Split(line.Trim(), @"\s{2,}");
            var fields = new List<string>();
            for (int index = 0; index < starts.Length; index++)
            {
                int start = starts[index];
                if (start >= line.Length) { fields.Add(string.Empty); continue; }
                int end = index + 1 < starts.Length ? Math.Min(line.Length, starts[index + 1]) : line.Length;
                fields.Add(line.Substring(start, Math.Max(0, end - start)).Trim());
            }
            return fields.ToArray();
        }

        private static bool IsValidPackageId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && PackageIdPattern.IsMatch(value);
        }

        private static string CompactError(string output)
        {
            string value = AnsiPattern.Replace(output ?? string.Empty, string.Empty).Trim().Replace("\r", " ").Replace("\n", " ");
            while (value.Contains("  ")) value = value.Replace("  ", " ");
            if (value.Length > 400) value = value.Substring(0, 400) + "...";
            return string.IsNullOrWhiteSpace(value) ? "erro não informado." : value;
        }
    }
}
