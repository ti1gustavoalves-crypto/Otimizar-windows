using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace CodexPerformanceOptimizer
{
    internal enum ServiceProfile
    {
        Preventive,
        SlowComputer,
        LowStorage,
        SlowStartup
    }

    [Flags]
    internal enum MaintenanceAction
    {
        None = 0,
        ConfigurePower = 1,
        ReduceVisuals = 2,
        OptimizeStartup = 4,
        CleanupTemporaryFiles = 8,
        ReduceBackgroundActivity = 16,
        OptimizeVolume = 32,
        CreateRestorePoint = 64
    }

    internal sealed class MaintenanceIssue
    {
        public string Id { get; set; }
        public bool Selected { get; set; }
        public string Severity { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public MaintenanceAction Action { get; set; }
        public bool RequiresAdministrator { get; set; }
        public bool CanFix { get { return Action != MaintenanceAction.None; } }
    }

    internal sealed class MaintenancePlan
    {
        public ServiceProfile Profile { get; set; }
        public List<MaintenanceIssue> Issues { get; set; }
        public bool RequiresAdministrator { get { return Issues.Any(item => item.Selected && item.RequiresAdministrator); } }
        public int SelectedCount { get { return Issues.Count(item => item.Selected && item.CanFix); } }
    }

    internal static class MaintenanceWorkflow
    {
        public static MaintenancePlan BuildPlan(ServiceProfile profile, SystemMetrics metrics, DiagnosticSnapshot diagnostics, IEnumerable<StartupEntry> startup, int driverUpdates, int programUpdates)
        {
            metrics = metrics ?? new SystemMetrics();
            var issues = new List<MaintenanceIssue>();
            MaintenanceAction preset = PresetActions(profile);

            AddAction(issues, preset, MaintenanceAction.CleanupTemporaryFiles, "storage", "Revisar arquivos temporários", "Remove apenas temporários antigos e caches conhecidos.", "Recomendado", false);
            AddAction(issues, preset, MaintenanceAction.OptimizeStartup, "startup", "Reduzir a inicialização", "Desativa entradas conhecidas de alto impacto e preserva itens corporativos.", "Recomendado", false);
            AddAction(issues, preset, MaintenanceAction.ReduceBackgroundActivity, "background", "Reduzir atividade em segundo plano", "Limita recursos não essenciais sem remover aplicativos.", "Recomendado", false);
            AddAction(issues, preset, MaintenanceAction.ConfigurePower, "power", "Ajustar o perfil de energia", "Prioriza resposta e desempenho durante o atendimento.", "Recomendado", false);
            AddAction(issues, preset, MaintenanceAction.ReduceVisuals, "visuals", "Reduzir efeitos visuais", "Diminui animações e transparências para melhorar a resposta.", "Opcional", false);
            AddAction(issues, preset, MaintenanceAction.OptimizeVolume, "volume", "Otimizar a unidade do sistema", "O Windows escolherá TRIM ou desfragmentação conforme a mídia.", "Recomendado", true);
            AddAction(issues, preset, MaintenanceAction.CreateRestorePoint, "restore", "Criar ponto de restauração", "Registra uma proteção antes das alterações do sistema.", "Proteção", true);

            double memoryFreePercent = metrics.TotalRamGb > 0 ? metrics.FreeRamGb * 100.0 / metrics.TotalRamGb : 100;
            if (metrics.TotalDiskGb > 0 && metrics.FreeDiskPercent < 15)
                Promote(issues, "storage", "Crítico", "Apenas " + metrics.FreeDiskPercent.ToString("N1", CultureInfo.CurrentCulture) + "% do disco C: está livre.");
            if (memoryFreePercent < 15)
                Promote(issues, "background", "Atenção", "Apenas " + memoryFreePercent.ToString("N0", CultureInfo.CurrentCulture) + "% da memória está disponível.");

            int activeHighImpact = (startup ?? Enumerable.Empty<StartupEntry>()).Count(item => item.Enabled && string.Equals(item.Impact, "alto", StringComparison.OrdinalIgnoreCase) && item.CanChange);
            if (activeHighImpact > 0)
                Promote(issues, "startup", activeHighImpact >= 3 ? "Atenção" : "Recomendado", activeHighImpact + (activeHighImpact == 1 ? " aplicativo alterável tem" : " aplicativos alteráveis têm") + " alto impacto.");

            if (diagnostics != null && diagnostics.Stability != null && diagnostics.Stability.PendingRestart)
                issues.Add(Information("restart", "Sistema", "Reinicialização pendente", "Conclua a manutenção reiniciando o Windows."));
            if (diagnostics != null && diagnostics.Stability != null && (diagnostics.Stability.UnexpectedShutdowns > 0 || diagnostics.Stability.SystemFailures > 0))
                issues.Add(new MaintenanceIssue { Id = "stability", Selected = false, Severity = "Atenção", Category = "Estabilidade", Title = "Eventos de desligamento ou falha", Detail = diagnostics.Stability.UnexpectedShutdowns + " desligamentos inesperados e " + diagnostics.Stability.SystemFailures + " falhas nos últimos 30 dias.", Action = MaintenanceAction.None });
            if (diagnostics != null && diagnostics.Disks != null && diagnostics.Disks.Any(item => item.Warning))
                issues.Add(new MaintenanceIssue { Id = "disk-health", Selected = false, Severity = "Crítico", Category = "Hardware", Title = "Unidade com alerta de saúde", Detail = "Faça backup dos dados e revise o diagnóstico do armazenamento.", Action = MaintenanceAction.None });
            if (driverUpdates > 0)
                issues.Add(Information("drivers", "Atualizações", driverUpdates + (driverUpdates == 1 ? " driver disponível" : " drivers disponíveis"), "Revise a central de Atualizações antes de instalar."));
            if (programUpdates > 0)
                issues.Add(Information("programs", "Atualizações", programUpdates + (programUpdates == 1 ? " aplicativo disponível" : " aplicativos disponíveis"), "As atualizações podem ser instaladas pela central do programa."));
            if (issues.Count == 0)
                issues.Add(Information("healthy", "Sistema", "Nenhuma pendência importante", "Os indicadores disponíveis estão dentro dos limites definidos."));

            return new MaintenancePlan { Profile = profile, Issues = issues };
        }

        public static string Execute(MaintenancePlan plan, CancellationToken token, IProgress<string> progress)
        {
            if (plan == null) return "Nenhum plano de manutenção foi preparado.";
            MaintenanceAction actions = plan.Issues.Where(item => item.Selected && item.CanFix).Aggregate(MaintenanceAction.None, (current, item) => current | item.Action);
            if (actions == MaintenanceAction.None) return "Nenhuma ação de manutenção foi selecionada.";
            var options = new ApplyOptions
            {
                Profile = plan.Profile == ServiceProfile.SlowComputer ? 0 : 1,
                ConfigurePower = actions.HasFlag(MaintenanceAction.ConfigurePower),
                ReduceVisuals = actions.HasFlag(MaintenanceAction.ReduceVisuals),
                OptimizeStartup = actions.HasFlag(MaintenanceAction.OptimizeStartup),
                CleanupTemp = actions.HasFlag(MaintenanceAction.CleanupTemporaryFiles),
                BackgroundEfficiency = actions.HasFlag(MaintenanceAction.ReduceBackgroundActivity),
                OptimizeVolume = actions.HasFlag(MaintenanceAction.OptimizeVolume),
                CreateRestorePoint = actions.HasFlag(MaintenanceAction.CreateRestorePoint)
            };
            return V2Engine.Apply(options, token, progress);
        }

        public static string ProfileName(ServiceProfile profile)
        {
            switch (profile)
            {
                case ServiceProfile.SlowComputer: return "PC lento";
                case ServiceProfile.LowStorage: return "Pouco espaço";
                case ServiceProfile.SlowStartup: return "Inicialização lenta";
                default: return "Manutenção preventiva";
            }
        }

        private static MaintenanceAction PresetActions(ServiceProfile profile)
        {
            switch (profile)
            {
                case ServiceProfile.SlowComputer:
                    return MaintenanceAction.ConfigurePower | MaintenanceAction.ReduceVisuals | MaintenanceAction.OptimizeStartup | MaintenanceAction.ReduceBackgroundActivity | MaintenanceAction.OptimizeVolume | MaintenanceAction.CreateRestorePoint;
                case ServiceProfile.LowStorage:
                    return MaintenanceAction.CleanupTemporaryFiles | MaintenanceAction.OptimizeVolume | MaintenanceAction.CreateRestorePoint;
                case ServiceProfile.SlowStartup:
                    return MaintenanceAction.OptimizeStartup | MaintenanceAction.ReduceBackgroundActivity | MaintenanceAction.CreateRestorePoint;
                default:
                    return MaintenanceAction.CleanupTemporaryFiles | MaintenanceAction.OptimizeStartup | MaintenanceAction.ReduceBackgroundActivity | MaintenanceAction.OptimizeVolume | MaintenanceAction.CreateRestorePoint;
            }
        }

        private static void AddAction(List<MaintenanceIssue> issues, MaintenanceAction preset, MaintenanceAction action, string id, string title, string detail, string severity, bool requiresAdministrator)
        {
            bool selected = preset.HasFlag(action);
            issues.Add(new MaintenanceIssue { Id = id, Selected = selected, Severity = selected ? severity : "Opcional", Category = Category(action), Title = title, Detail = detail, Action = action, RequiresAdministrator = requiresAdministrator });
        }

        private static string Category(MaintenanceAction action)
        {
            if (action == MaintenanceAction.CleanupTemporaryFiles || action == MaintenanceAction.OptimizeVolume) return "Armazenamento";
            if (action == MaintenanceAction.OptimizeStartup || action == MaintenanceAction.ReduceBackgroundActivity) return "Desempenho";
            if (action == MaintenanceAction.CreateRestorePoint) return "Proteção";
            return "Sistema";
        }

        private static void Promote(List<MaintenanceIssue> issues, string id, string severity, string detail)
        {
            MaintenanceIssue issue = issues.FirstOrDefault(item => item.Id == id);
            if (issue == null) return;
            issue.Severity = severity;
            issue.Detail = detail;
            issue.Selected = true;
        }

        private static MaintenanceIssue Information(string id, string category, string title, string detail)
        {
            return new MaintenanceIssue { Id = id, Selected = false, Severity = "Informativo", Category = category, Title = title, Detail = detail, Action = MaintenanceAction.None };
        }
    }

    internal static class ResponsiveLayoutPolicy
    {
        public static string Validate(int width, int height, int dpi)
        {
            if (width < 1260 || height < 760) return "A janela está abaixo do tamanho mínimo suportado.";
            if (dpi < 96 || dpi > 240) return "Escala de DPI fora da faixa validada.";
            int contentWidth = width - 184;
            if (contentWidth < 1000) return "A área útil não comporta as tabelas técnicas.";
            return string.Empty;
        }
    }
}
