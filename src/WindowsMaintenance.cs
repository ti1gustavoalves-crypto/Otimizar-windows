using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CodexPerformanceOptimizer
{
    internal static class WindowsMaintenance
    {
        private static readonly string EnergyReportsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Codex", "PerformanceOptimizer", "EnergyReports");

        public static string LatestEnergyReportPath { get; private set; }

        public static string OptimizeVolume(string drive, CancellationToken token, IProgress<string> progress)
        {
            string normalized = NormalizeDrive(drive);
            if (string.IsNullOrEmpty(normalized)) return "A unidade selecionada não é válida.";
            if (!Optimizer.IsAdministrator()) return "A otimização da unidade exige privilégios de administrador. Use 'Executar como admin'.";

            progress.Report("Otimizando " + normalized + " conforme o tipo de mídia...");
            CommandExecution result = SystemCommand.Execute("defrag.exe", normalized + " /O /U /V", 30 * 60 * 1000, token);
            var report = new StringBuilder("OTIMIZAÇÃO INTELIGENTE DA UNIDADE\r\n" + new string('=', 72) + "\r\n");
            report.AppendLine("Unidade: " + normalized);
            report.AppendLine("Método: seleção automática do Windows para SSD, HDD ou armazenamento em camadas.");
            report.AppendLine(result.ExitCode == 0 ? "Resultado: concluído." : "Resultado: não concluído (código " + result.ExitCode + ").");
            AppendCommandOutput(report, result.Output);
            return report.ToString();
        }

        public static string CleanupComponentStore(CancellationToken token, IProgress<string> progress)
        {
            if (!Optimizer.IsAdministrator()) return "A limpeza de componentes exige privilégios de administrador. Use 'Executar como admin'.";

            double before = FreeSystemDriveGb();
            progress.Report("Analisando componentes do Windows...");
            CommandExecution analysis = SystemCommand.Execute("dism.exe", "/Online /Cleanup-Image /AnalyzeComponentStore /English", 20 * 60 * 1000, token);
            if (analysis.ExitCode != 0)
            {
                var failed = new StringBuilder("LIMPEZA DE COMPONENTES DO WINDOWS\r\n" + new string('=', 72) + "\r\n");
                failed.AppendLine("A análise do repositório de componentes não foi concluída.");
                AppendCommandOutput(failed, analysis.Output);
                return failed.ToString();
            }

            token.ThrowIfCancellationRequested();
            progress.Report("Removendo componentes substituídos...");
            CommandExecution cleanup = SystemCommand.Execute("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup /English", 45 * 60 * 1000, token);
            double after = FreeSystemDriveGb();
            var report = new StringBuilder("LIMPEZA DE COMPONENTES DO WINDOWS\r\n" + new string('=', 72) + "\r\n");
            report.AppendLine("Modo: StartComponentCleanup sem ResetBase.");
            report.AppendLine(cleanup.ExitCode == 0 ? "Resultado: concluído." : "Resultado: não concluído (código " + cleanup.ExitCode + ").");
            if (before >= 0 && after >= 0) report.AppendLine("Variação de espaço livre: " + (after - before).ToString("+0.00;-0.00;0.00", CultureInfo.CurrentCulture) + " GB.");
            report.AppendLine("O modo ResetBase não foi usado; a opção mais agressiva permanece desativada.");
            AppendCommandOutput(report, cleanup.Output);
            return report.ToString();
        }

        public static string GenerateEnergyReport(CancellationToken token, IProgress<string> progress)
        {
            if (!Optimizer.IsAdministrator()) return "O diagnóstico de energia exige privilégios de administrador. Use 'Executar como admin'.";

            Directory.CreateDirectory(EnergyReportsFolder);
            string path = Path.Combine(EnergyReportsFolder, "energia-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".html");
            progress.Report("Observando energia e atividade por 15 segundos...");
            CommandExecution result = SystemCommand.Execute("powercfg.exe", "/energy /duration 15 /output \"" + path + "\"", 90 * 1000, token);
            LatestEnergyReportPath = result.ExitCode == 0 && File.Exists(path) ? path : null;

            var report = new StringBuilder("DIAGNÓSTICO DE ENERGIA E DESEMPENHO\r\n" + new string('=', 72) + "\r\n");
            report.AppendLine(result.ExitCode == 0 ? "Resultado: relatório criado." : "Resultado: relatório não criado (código " + result.ExitCode + ").");
            if (!string.IsNullOrEmpty(LatestEnergyReportPath)) report.AppendLine("Arquivo: " + LatestEnergyReportPath);
            AppendCommandOutput(report, result.Output);
            return report.ToString();
        }

        public static void OpenLatestEnergyReport()
        {
            if (string.IsNullOrWhiteSpace(LatestEnergyReportPath) || !File.Exists(LatestEnergyReportPath)) return;
            Process.Start(new ProcessStartInfo(LatestEnergyReportPath) { UseShellExecute = true });
        }

        public static void OpenStorageSenseSettings()
        {
            Process.Start(new ProcessStartInfo("ms-settings:storagepolicies") { UseShellExecute = true });
        }

        internal static string NormalizeDriveForTesting(string drive)
        {
            return NormalizeDrive(drive);
        }

        private static string NormalizeDrive(string drive)
        {
            string value = (drive ?? string.Empty).Trim().Replace('/', '\\');
            if (Regex.IsMatch(value, "^[a-zA-Z]:\\\\?$")) return char.ToUpperInvariant(value[0]) + ":";
            return string.Empty;
        }

        private static double FreeSystemDriveGb()
        {
            try { return new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)).AvailableFreeSpace / 1073741824.0; }
            catch { return -1; }
        }

        private static void AppendCommandOutput(StringBuilder report, string output)
        {
            string value = (output ?? string.Empty).Trim();
            if (value.Length == 0) return;
            if (value.Length > 6000) value = value.Substring(0, 6000) + "\r\n[saída reduzida]";
            report.AppendLine();
            report.AppendLine(value);
        }
    }
}
