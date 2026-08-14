using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;

namespace CodexPerformanceOptimizer
{
    internal sealed class IntegrityCheckResult
    {
        public string Area { get; set; }
        public string Check { get; set; }
        public string Status { get; set; }
        public string Detail { get; set; }
        public bool CanRepair { get; set; }
        public bool Warning { get { return Status == "Atenção" || Status == "Falha"; } }
    }

    internal static class SystemIntegrityEngine
    {
        public static List<IntegrityCheckResult> QuickScan()
        {
            var results = new List<IntegrityCheckResult>();
            AddStorage(results);
            AddDiskHealth(results);
            AddStability(results);
            AddDevices(results);
            AddServices(results);
            return results;
        }

        public static List<IntegrityCheckResult> DeepScan(CancellationToken token, IProgress<string> progress)
        {
            if (!Optimizer.IsAdministrator()) throw new InvalidOperationException("A verificação profunda exige privilégios de administrador.");
            var results = new List<IntegrityCheckResult>();
            progress.Report("Verificando a imagem do Windows com DISM...");
            CommandExecution dism = SystemCommand.Execute("dism.exe", "/Online /Cleanup-Image /ScanHealth /English", 45 * 60 * 1000, token);
            results.Add(ClassifyCommand("Componentes do Windows", "Imagem do Windows (DISM)", dism, "dism"));
            token.ThrowIfCancellationRequested();

            progress.Report("Verificando arquivos protegidos com SFC...");
            CommandExecution sfc = SystemCommand.Execute("sfc.exe", "/verifyonly", 35 * 60 * 1000, token);
            results.Add(ClassifyCommand("Arquivos do sistema", "Arquivos protegidos (SFC)", sfc, "sfc"));
            token.ThrowIfCancellationRequested();

            string drive = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
            progress.Report("Verificando o sistema de arquivos em " + drive + "...");
            CommandExecution disk = SystemCommand.Execute("chkdsk.exe", drive + " /scan", 35 * 60 * 1000, token);
            results.Add(ClassifyCommand("Armazenamento", "Sistema de arquivos (CHKDSK)", disk, "chkdsk"));
            return results;
        }

        public static string RepairWindows(CancellationToken token, IProgress<string> progress)
        {
            if (!Optimizer.IsAdministrator()) return "O reparo exige privilégios de administrador.";
            var report = new StringBuilder("REPARO DE INTEGRIDADE DO WINDOWS\r\n" + new string('=', 72) + "\r\n");
            progress.Report("Criando ponto de restauração...");
            CommandExecution checkpoint = SystemCommand.Execute("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description 'Antes do reparo de integridade' -RestorePointType MODIFY_SETTINGS\"", 120000, token);
            report.AppendLine(checkpoint.ExitCode == 0 ? "Ponto de restauração: criado." : "Ponto de restauração: indisponível; o reparo continuará pelos mecanismos oficiais.");
            token.ThrowIfCancellationRequested();

            progress.Report("Reparando a imagem do Windows com DISM...");
            CommandExecution dism = SystemCommand.Execute("dism.exe", "/Online /Cleanup-Image /RestoreHealth /English", 60 * 60 * 1000, token);
            report.AppendLine("DISM RestoreHealth: " + (dism.ExitCode == 0 ? "concluído." : "não concluído (código " + dism.ExitCode + ")."));
            report.AppendLine(CompactOutput(dism.Output));
            token.ThrowIfCancellationRequested();

            progress.Report("Reparando arquivos protegidos com SFC...");
            CommandExecution sfc = SystemCommand.Execute("sfc.exe", "/scannow", 45 * 60 * 1000, token);
            report.AppendLine("SFC ScanNow: " + (sfc.ExitCode == 0 ? "concluído." : "não concluído (código " + sfc.ExitCode + ")."));
            report.AppendLine(CompactOutput(sfc.Output));
            return report.ToString().TrimEnd();
        }

        public static string BuildReport(IEnumerable<IntegrityCheckResult> results)
        {
            var report = new StringBuilder("INTEGRIDADE DO SISTEMA\r\n" + new string('=', 72) + "\r\n");
            foreach (IntegrityCheckResult item in results ?? Enumerable.Empty<IntegrityCheckResult>())
                report.AppendLine(item.Status + " | " + item.Area + " | " + item.Check + " | " + item.Detail);
            return report.ToString().TrimEnd();
        }

        internal static IntegrityCheckResult ClassifyCommandForTesting(string kind, int exitCode, string output)
        {
            return ClassifyCommand("Teste", "Teste", new CommandExecution { ExitCode = exitCode, Output = output }, kind);
        }

        private static IntegrityCheckResult ClassifyCommand(string area, string check, CommandExecution command, string kind)
        {
            string output = command.Output ?? string.Empty;
            string normalized = output.ToLowerInvariant();
            bool healthy = command.ExitCode == 0;
            bool repairable = false;
            if (kind == "dism")
            {
                bool noCorruption = ContainsAny(normalized, "no component store corruption detected", "nenhuma corrupção no repositório de componentes", "no se detectó corrupción");
                repairable = ContainsAny(normalized, "repairable", "reparável", "reparable");
                healthy = command.ExitCode == 0 && (noCorruption || (!repairable && !ContainsAny(normalized, "corruption detected", "corrupção detectada")));
            }
            else if (kind == "sfc")
            {
                bool noViolation = ContainsAny(normalized, "did not find any integrity violations", "não encontrou nenhuma violação de integridade", "no encontró ninguna infracción de integridad");
                repairable = ContainsAny(normalized, "found integrity violations", "encontrou violações de integridade", "found corrupt files", "encontrou arquivos corrompidos");
                healthy = command.ExitCode == 0 && (noViolation || !repairable);
            }
            else if (kind == "chkdsk")
            {
                bool errors = ContainsAny(normalized, "found problems", "encontrou problemas", "found errors", "encontrou erros");
                healthy = command.ExitCode == 0 && !errors;
            }
            return new IntegrityCheckResult
            {
                Area = area,
                Check = check,
                Status = healthy ? "OK" : command.TimedOut ? "Falha" : "Atenção",
                Detail = command.TimedOut ? "Tempo limite excedido." : CompactOutput(output),
                CanRepair = repairable && !command.TimedOut
            };
        }

        private static void AddStorage(List<IntegrityCheckResult> results)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                double free = drive.TotalSize > 0 ? drive.AvailableFreeSpace * 100.0 / drive.TotalSize : 0;
                results.Add(Result("Armazenamento", "Espaço na unidade do sistema", free < 10 ? "Atenção" : "OK", free.ToString("N1", CultureInfo.CurrentCulture) + "% livre em " + drive.Name, false));
            }
            catch (Exception ex) { results.Add(Result("Armazenamento", "Espaço na unidade do sistema", "Falha", ex.Message, false)); }
        }

        private static void AddStability(List<IntegrityCheckResult> results)
        {
            StabilityDiagnostic stability = AdvancedEngine.ReadStability();
            bool failures = stability.UnexpectedShutdowns > 0 || stability.SystemFailures > 0;
            results.Add(Result("Estabilidade", "Falhas recentes do sistema", failures ? "Atenção" : "OK", stability.UnexpectedShutdowns + " desligamentos inesperados e " + stability.SystemFailures + " falhas nos últimos 30 dias.", false));
            results.Add(Result("Windows", "Reinicialização pendente", stability.PendingRestart ? "Atenção" : "OK", stability.PendingRestart ? "Uma atualização ou alteração aguarda reinicialização." : "Nenhuma reinicialização pendente foi detectada.", false));
        }

        private static void AddDiskHealth(List<IntegrityCheckResult> results)
        {
            List<DiskDiagnostic> disks = AdvancedEngine.ReadDiskDiagnostics();
            if (disks.Count == 0)
            {
                results.Add(Result("Armazenamento", "Saúde física dos discos", "Atenção", "O dispositivo não informou dados de saúde.", false));
                return;
            }
            foreach (DiskDiagnostic disk in disks)
            {
                string detail = disk.Name + " • " + disk.Health + " • vida útil " + disk.Life + " • " + disk.Temperature;
                results.Add(Result("Armazenamento", "Saúde de " + disk.Type, disk.Warning ? "Atenção" : "OK", detail, false));
            }
        }

        private static void AddDevices(List<IntegrityCheckResult> results)
        {
            List<DriverInventoryItem> devices = DriverManager.ReadInstalledDrivers();
            int problems = devices.Count(item => item.HasProblem);
            results.Add(Result("Dispositivos", "Drivers e dispositivos", problems > 0 ? "Atenção" : "OK", problems == 0 ? devices.Count + " dispositivos relevantes sem problema detectado." : problems + " dispositivo(s) com falha ou assinatura ausente.", false));
        }

        private static void AddServices(List<IntegrityCheckResult> results)
        {
            var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "EventLog", "Log de Eventos" }, { "Winmgmt", "Instrumentação do Windows" }, { "wuauserv", "Windows Update" }, { "BITS", "Transferência em Segundo Plano" } };
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, State, StartMode FROM Win32_Service WHERE Name='EventLog' OR Name='Winmgmt' OR Name='wuauserv' OR Name='BITS'"))
                using (ManagementObjectCollection services = searcher.Get())
                foreach (ManagementObject service in services)
                {
                    string name = Convert.ToString(service["Name"]);
                    string state = Convert.ToString(service["State"]);
                    string startMode = Convert.ToString(service["StartMode"]);
                    bool essentialRunning = name == "EventLog" || name == "Winmgmt";
                    bool warning = string.Equals(startMode, "Disabled", StringComparison.OrdinalIgnoreCase) || (essentialRunning && !string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase));
                    results.Add(Result("Serviços", wanted.ContainsKey(name) ? wanted[name] : name, warning ? "Atenção" : "OK", "Estado: " + state + " • Inicialização: " + startMode, false));
                    wanted.Remove(name);
                }
                foreach (KeyValuePair<string, string> missing in wanted) results.Add(Result("Serviços", missing.Value, "Atenção", "Serviço não encontrado.", false));
            }
            catch (Exception ex) { results.Add(Result("Serviços", "Serviços essenciais", "Falha", ex.Message, false)); }
        }

        private static IntegrityCheckResult Result(string area, string check, string status, string detail, bool canRepair)
        {
            return new IntegrityCheckResult { Area = area, Check = check, Status = status, Detail = detail, CanRepair = canRepair };
        }

        private static bool ContainsAny(string value, params string[] patterns)
        {
            return patterns.Any(pattern => value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string CompactOutput(string output)
        {
            string value = (output ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (value.Contains("  ")) value = value.Replace("  ", " ");
            if (value.Length == 0) return "Comando concluído sem detalhes adicionais.";
            return value.Length > 500 ? value.Substring(0, 500) + "..." : value;
        }
    }
}
