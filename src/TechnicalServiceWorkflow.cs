using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace CodexPerformanceOptimizer
{
    internal sealed class TechnicalServiceResult
    {
        public HealthAssessment BeforeHealth { get; set; }
        public HealthAssessment AfterHealth { get; set; }
        public BottleneckCause Cause { get; set; }
        public List<DriverUpdate> DriverUpdates { get; set; }
        public List<ProgramUpdate> ProgramUpdates { get; set; }
        public List<IntegrityCheckResult> IntegrityResults { get; set; }
        public List<RepairFinding> RepairFindings { get; set; }
        public string Report { get; set; }
    }

    internal static class TechnicalServiceWorkflow
    {
        public static TechnicalServiceResult Execute(MaintenancePlan plan, IEnumerable<ProcessActivity> processes, CancellationToken token, IProgress<string> progress)
        {
            progress.Report("Etapa 1 de 5 • registrando diagnóstico inicial...");
            SystemMetrics before = V2Engine.ReadMetrics();
            DiagnosticSnapshot beforeDiagnostics = CachedAnalysis.ReadDiagnostics(false);
            token.ThrowIfCancellationRequested();

            string maintenance = "Nenhuma correção selecionada.";
            if (plan != null && plan.SelectedCount > 0)
            {
                progress.Report("Etapa 2 de 5 • aplicando correções selecionadas...");
                maintenance = MaintenanceWorkflow.Execute(plan, token, progress);
                CachedAnalysis.InvalidateStorage();
            }

            progress.Report("Etapa 3 de 5 • procurando falhas gerais...");
            List<IntegrityCheckResult> integrity = SystemIntegrityEngine.QuickScan();
            List<RepairFinding> repairs = GeneralRepairEngine.Scan(token, progress);
            token.ThrowIfCancellationRequested();

            progress.Report("Etapa 4 de 5 • consultando atualizações...");
            var drivers = new List<DriverUpdate>();
            var programs = new List<ProgramUpdate>();
            string driverError = string.Empty;
            string programError = string.Empty;
            try { drivers = CachedAnalysis.SearchDriverUpdates(false, token, progress); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { driverError = ex.Message; }
            token.ThrowIfCancellationRequested();
            try
            {
                if (ProgramUpdater.IsAvailable()) programs = CachedAnalysis.SearchProgramUpdates(false, token, progress);
                else programError = "WinGet indisponível.";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { programError = ex.Message; }
            token.ThrowIfCancellationRequested();

            progress.Report("Etapa 5 de 5 • verificando o resultado final...");
            SystemMetrics after = V2Engine.ReadMetrics();
            DiagnosticSnapshot afterDiagnostics = CachedAnalysis.ReadDiagnostics(true);
            HealthAssessment beforeHealth = SystemHealthEngine.Assess(before, beforeDiagnostics, processes, drivers.Count, programs.Count);
            HealthAssessment afterHealth = SystemHealthEngine.Assess(after, afterDiagnostics, processes, drivers.Count, programs.Count);
            BottleneckCause cause = BottleneckAnalyzer.Analyze(after, afterDiagnostics, processes);

            var report = new StringBuilder("ATENDIMENTO TÉCNICO COMPLETO\r\n" + new string('=', 72) + "\r\n");
            report.AppendLine("Saúde inicial: " + beforeHealth.Level + " • " + beforeHealth.Score + "/100");
            report.AppendLine("Saúde final: " + afterHealth.Level + " • " + afterHealth.Score + "/100");
            report.AppendLine("Causa provável: " + cause.Title + " — " + cause.Detail);
            report.AppendLine("Atualizações de drivers pendentes: " + drivers.Count);
            report.AppendLine("Atualizações de programas pendentes: " + programs.Count);
            report.AppendLine("Alertas de integridade: " + integrity.Count(item => item.Warning));
            report.AppendLine("Correções gerais sugeridas: " + repairs.Count(item => item.Warning));
            if (!string.IsNullOrWhiteSpace(driverError)) report.AppendLine("Consulta de drivers: " + driverError);
            if (!string.IsNullOrWhiteSpace(programError)) report.AppendLine("Consulta de programas: " + programError);
            report.AppendLine("\r\nAÇÕES EXECUTADAS");
            report.AppendLine(maintenance);

            return new TechnicalServiceResult
            {
                BeforeHealth = beforeHealth,
                AfterHealth = afterHealth,
                Cause = cause,
                DriverUpdates = drivers,
                ProgramUpdates = programs,
                IntegrityResults = integrity,
                RepairFindings = repairs,
                Report = report.ToString()
            };
        }
    }
}
