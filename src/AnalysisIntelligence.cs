using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace CodexPerformanceOptimizer
{
    internal sealed class HealthAssessment
    {
        public int Score { get; set; }
        public string Level { get; set; }
        public string Summary { get; set; }
        public List<string> Factors { get; set; }
    }

    internal sealed class BottleneckCause
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        public bool Warning { get; set; }
    }

    internal static class SystemHealthEngine
    {
        public static HealthAssessment Assess(SystemMetrics metrics, DiagnosticSnapshot diagnostics, IEnumerable<ProcessActivity> processes, int driverUpdates, int programUpdates)
        {
            metrics = metrics ?? new SystemMetrics();
            var factors = new List<string>();
            int score = 100;
            double freeMemoryPercent = metrics.TotalRamGb > 0 ? metrics.FreeRamGb * 100.0 / metrics.TotalRamGb : 100;

            if (metrics.FreeDiskPercent < 10) { score -= 25; factors.Add("disco com menos de 10% livre"); }
            else if (metrics.FreeDiskPercent < 15) { score -= 15; factors.Add("pouco espaço no disco"); }
            else if (metrics.FreeDiskPercent < 25) { score -= 6; factors.Add("espaço em disco abaixo do ideal"); }

            if (freeMemoryPercent < 10) { score -= 25; factors.Add("memória disponível crítica"); }
            else if (freeMemoryPercent < 15) { score -= 17; factors.Add("memória disponível baixa"); }
            else if (freeMemoryPercent < 25) { score -= 7; factors.Add("memória pressionada"); }

            if (metrics.CpuUsagePercent >= 90) { score -= 15; factors.Add("processador próximo do limite"); }
            else if (metrics.CpuUsagePercent >= 75) { score -= 7; factors.Add("processador com uso alto"); }

            if (diagnostics != null)
            {
                if (diagnostics.Disks != null && diagnostics.Disks.Any(item => item.Warning)) { score -= 25; factors.Add("alerta de saúde em unidade"); }
                if (diagnostics.Stability != null)
                {
                    if (diagnostics.Stability.UnexpectedShutdowns > 0 || diagnostics.Stability.SystemFailures > 0) { score -= 12; factors.Add("falhas recentes do sistema"); }
                    if (diagnostics.Stability.PendingRestart) { score -= 4; factors.Add("reinicialização pendente"); }
                }
                StartupMeasurement boot = diagnostics.Startup == null ? null : diagnostics.Startup.FirstOrDefault(item => item.Name == "Inicialização do Windows");
                if (boot != null && boot.DurationMilliseconds > 60000) { score -= 9; factors.Add("inicialização acima de 60 segundos"); }
            }

            int pendingUpdates = Math.Max(0, driverUpdates) + Math.Max(0, programUpdates);
            if (pendingUpdates > 0)
            {
                score -= Math.Min(8, pendingUpdates);
                factors.Add(pendingUpdates + (pendingUpdates == 1 ? " atualização pendente" : " atualizações pendentes"));
            }

            score = Math.Max(0, Math.Min(100, score));
            string level = score >= 90 ? "Excelente" : score >= 75 ? "Boa" : score >= 55 ? "Atenção" : "Crítica";
            return new HealthAssessment
            {
                Score = score,
                Level = level,
                Summary = factors.Count == 0 ? "Nenhum desvio relevante nos indicadores disponíveis" : factors[0],
                Factors = factors
            };
        }
    }

    internal static class BottleneckAnalyzer
    {
        public static BottleneckCause Analyze(SystemMetrics metrics, DiagnosticSnapshot diagnostics, IEnumerable<ProcessActivity> processes)
        {
            metrics = metrics ?? new SystemMetrics();
            List<ProcessActivity> activity = (processes ?? Enumerable.Empty<ProcessActivity>()).ToList();
            double freeMemoryPercent = metrics.TotalRamGb > 0 ? metrics.FreeRamGb * 100.0 / metrics.TotalRamGb : 100;

            if (metrics.CpuUsagePercent >= 70)
            {
                ProcessActivity top = activity.OrderByDescending(item => item.CpuPercent).FirstOrDefault();
                return new BottleneckCause
                {
                    Title = top == null ? "Processador com uso alto" : "CPU concentrada em " + top.Name,
                    Detail = top == null ? "Acompanhe os processos por alguns segundos para identificar a origem." : top.CpuPercent.ToString("N1", CultureInfo.CurrentCulture) + "% de CPU na amostra atual; confirme no histórico antes de encerrar qualquer processo.",
                    Warning = true
                };
            }

            if (freeMemoryPercent < 20)
            {
                string names = string.Join(" e ", activity.OrderByDescending(item => item.WorkingSetBytes).Take(2).Select(item => item.Name));
                return new BottleneckCause
                {
                    Title = string.IsNullOrWhiteSpace(names) ? "Memória disponível baixa" : "Memória pressionada por " + names,
                    Detail = freeMemoryPercent.ToString("N0", CultureInfo.CurrentCulture) + "% da memória está livre. Feche apenas aplicativos que não estejam em uso.",
                    Warning = true
                };
            }

            if (metrics.FreeDiskPercent < 15)
                return new BottleneckCause { Title = "Pouco espaço no disco C:", Detail = metrics.FreeDiskPercent.ToString("N1", CultureInfo.CurrentCulture) + "% livre; temporários e arquivos grandes são as primeiras verificações recomendadas.", Warning = true };

            if (diagnostics != null && diagnostics.Startup != null)
            {
                StartupMeasurement boot = diagnostics.Startup.FirstOrDefault(item => item.Name == "Inicialização do Windows");
                if (boot != null && boot.DurationMilliseconds > 60000)
                    return new BottleneckCause { Title = "Inicialização lenta", Detail = "O último início medido levou " + TimeSpan.FromMilliseconds(boot.DurationMilliseconds).TotalSeconds.ToString("N1", CultureInfo.CurrentCulture) + " segundos.", Warning = true };
            }

            return new BottleneckCause { Title = "Nenhum gargalo dominante", Detail = "A conclusão usa métricas atuais e o histórico recente; continue monitorando durante a carga real do usuário.", Warning = false };
        }
    }

    internal static class AnalysisCache
    {
        private sealed class Entry
        {
            public object Value { get; set; }
            public string Fingerprint { get; set; }
            public DateTime CreatedUtc { get; set; }
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public static bool TryGet<T>(string key, string fingerprint, TimeSpan lifetime, out T value)
        {
            lock (Sync)
            {
                Entry entry;
                if (Entries.TryGetValue(key, out entry) && entry.Value is T && string.Equals(entry.Fingerprint, fingerprint ?? string.Empty, StringComparison.Ordinal) && DateTime.UtcNow - entry.CreatedUtc <= lifetime)
                {
                    value = (T)entry.Value;
                    return true;
                }
                Entries.Remove(key);
            }
            value = default(T);
            return false;
        }

        public static void Set<T>(string key, string fingerprint, T value)
        {
            lock (Sync) Entries[key] = new Entry { Value = value, Fingerprint = fingerprint ?? string.Empty, CreatedUtc = DateTime.UtcNow };
        }

        public static void Invalidate(string prefix)
        {
            lock (Sync)
                foreach (string key in Entries.Keys.Where(item => item.StartsWith(prefix ?? string.Empty, StringComparison.OrdinalIgnoreCase)).ToArray()) Entries.Remove(key);
        }

        public static string DriveFingerprint(string drive)
        {
            try
            {
                var info = new DriveInfo(drive);
                return info.AvailableFreeSpace + ":" + info.TotalSize + ":" + Directory.GetLastWriteTimeUtc(info.RootDirectory.FullName).Ticks;
            }
            catch { return drive ?? string.Empty; }
        }

        public static string FolderFingerprint(string folder)
        {
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(folder));
                return Path.GetFullPath(folder).ToUpperInvariant() + ":" + Directory.GetLastWriteTimeUtc(folder).Ticks + ":" + DriveFingerprint(root);
            }
            catch { return folder ?? string.Empty; }
        }
    }

    internal static class CachedAnalysis
    {
        public static DiagnosticSnapshot ReadDiagnostics(bool force)
        {
            DiagnosticSnapshot value;
            if (!force && AnalysisCache.TryGet("diagnostics", string.Empty, TimeSpan.FromMinutes(2), out value)) return value;
            value = AdvancedEngine.ReadDiagnostics();
            AnalysisCache.Set("diagnostics", string.Empty, value);
            return value;
        }

        public static List<ImportantHardware> ReadHardware(bool force, CancellationToken token, IProgress<string> progress)
        {
            List<ImportantHardware> value;
            if (!force && AnalysisCache.TryGet("hardware", string.Empty, TimeSpan.FromMinutes(30), out value)) return value;
            value = V2Engine.ReadImportantHardware(token, progress);
            AnalysisCache.Set("hardware", string.Empty, value);
            return value;
        }

        public static List<DriverInventoryItem> ReadDriverInventory(bool force)
        {
            List<DriverInventoryItem> value;
            if (!force && AnalysisCache.TryGet("driver-inventory", string.Empty, TimeSpan.FromMinutes(10), out value)) return value;
            value = DriverManager.ReadInstalledDrivers();
            AnalysisCache.Set("driver-inventory", string.Empty, value);
            return value;
        }

        public static List<DriverUpdate> SearchDriverUpdates(bool force, CancellationToken token, IProgress<string> progress)
        {
            List<DriverUpdate> value;
            if (!force && AnalysisCache.TryGet("driver-updates", string.Empty, TimeSpan.FromMinutes(10), out value)) return value;
            value = DriverManager.SearchUpdates(token, progress);
            AnalysisCache.Set("driver-updates", string.Empty, value);
            return value;
        }

        public static List<ProgramUpdate> SearchProgramUpdates(bool force, CancellationToken token, IProgress<string> progress)
        {
            List<ProgramUpdate> value;
            if (!force && AnalysisCache.TryGet("program-updates", string.Empty, TimeSpan.FromMinutes(10), out value)) return value;
            value = ProgramUpdater.SearchUpdates(token, progress);
            AnalysisCache.Set("program-updates", string.Empty, value);
            return value;
        }

        public static List<StorageEntry> ScanVolume(string drive, bool force, CancellationToken token, IProgress<string> progress, Action<StorageEntry> onItem)
        {
            string key = "storage-volume:" + (drive ?? string.Empty);
            string fingerprint = AnalysisCache.DriveFingerprint(drive);
            List<StorageEntry> value;
            if (!force && AnalysisCache.TryGet(key, fingerprint, TimeSpan.FromMinutes(5), out value))
            {
                if (onItem != null) foreach (StorageEntry item in value) onItem(item);
                return value;
            }
            value = V2Engine.ScanVolume(drive, token, progress, onItem);
            AnalysisCache.Set(key, fingerprint, value);
            return value;
        }

        public static List<LargeFileEntry> FindLargeFiles(string drive, bool force, CancellationToken token, IProgress<string> progress)
        {
            string key = "large-files:" + (drive ?? string.Empty);
            string fingerprint = AnalysisCache.DriveFingerprint(drive);
            List<LargeFileEntry> value;
            if (!force && AnalysisCache.TryGet(key, fingerprint, TimeSpan.FromMinutes(5), out value)) return value;
            value = AdvancedEngine.FindLargeFiles(drive, token, progress);
            AnalysisCache.Set(key, fingerprint, value);
            return value;
        }

        public static List<DuplicateEntry> FindDuplicates(string folder, bool force, CancellationToken token, IProgress<string> progress)
        {
            string key = "duplicates:" + (folder ?? string.Empty);
            string fingerprint = AnalysisCache.FolderFingerprint(folder);
            List<DuplicateEntry> value;
            if (!force && AnalysisCache.TryGet(key, fingerprint, TimeSpan.FromMinutes(5), out value)) return value;
            value = V2Engine.FindDuplicates(folder, token, progress);
            AnalysisCache.Set(key, fingerprint, value);
            return value;
        }

        public static void InvalidateStorage()
        {
            AnalysisCache.Invalidate("storage-volume:");
            AnalysisCache.Invalidate("large-files:");
            AnalysisCache.Invalidate("duplicates:");
            AnalysisCache.Invalidate("diagnostics");
        }

        public static void InvalidateDrivers()
        {
            AnalysisCache.Invalidate("driver-");
            AnalysisCache.Invalidate("hardware");
            AnalysisCache.Invalidate("diagnostics");
        }
    }
}
