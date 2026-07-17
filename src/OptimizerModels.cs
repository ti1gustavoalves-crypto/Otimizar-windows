using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace CodexPerformanceOptimizer
{
    internal sealed class ApplyOptions
    {
        public int Profile { get; set; }
        public bool DarkMode { get; set; }
        public bool ReduceVisuals { get; set; }
        public bool OptimizeStartup { get; set; }
        public bool CleanupTemp { get; set; }
        public bool CreateRestorePoint { get; set; }
        public bool BackgroundEfficiency { get; set; }
    }

    internal sealed class SystemMetrics
    {
        public double TotalRamGb { get; set; }
        public double FreeRamGb { get; set; }
        public double FreeDiskGb { get; set; }
        public double TotalDiskGb { get; set; }
        public double FreeDiskPercent { get; set; }
        public double CpuUsagePercent { get; set; }
        public int CpuCores { get; set; }
        public int CpuThreads { get; set; }
        public string PowerScheme { get; set; }
    }

    internal sealed class SystemActivitySampler
    {
        private ulong _previousIdle;
        private ulong _previousKernel;
        private ulong _previousUser;
        private bool _hasCpuBaseline;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeFileTime
        {
            public uint Low;
            public uint High;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out NativeFileTime idle, out NativeFileTime kernel, out NativeFileTime user);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

        public void Prime()
        {
            NativeFileTime idle;
            NativeFileTime kernel;
            NativeFileTime user;
            if (!GetSystemTimes(out idle, out kernel, out user)) return;
            _previousIdle = ToUInt64(idle);
            _previousKernel = ToUInt64(kernel);
            _previousUser = ToUInt64(user);
            _hasCpuBaseline = true;
        }

        public double? Sample(out double totalRamGb, out double freeRamGb)
        {
            totalRamGb = 0;
            freeRamGb = 0;
            var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf(typeof(MemoryStatus)) };
            if (GlobalMemoryStatusEx(ref memory))
            {
                totalRamGb = memory.TotalPhysical / 1073741824.0;
                freeRamGb = memory.AvailablePhysical / 1073741824.0;
            }

            NativeFileTime idle;
            NativeFileTime kernel;
            NativeFileTime user;
            if (!GetSystemTimes(out idle, out kernel, out user)) return null;
            ulong currentIdle = ToUInt64(idle);
            ulong currentKernel = ToUInt64(kernel);
            ulong currentUser = ToUInt64(user);
            if (!_hasCpuBaseline)
            {
                _previousIdle = currentIdle;
                _previousKernel = currentKernel;
                _previousUser = currentUser;
                _hasCpuBaseline = true;
                return null;
            }

            ulong idleDelta = currentIdle - _previousIdle;
            ulong kernelDelta = currentKernel - _previousKernel;
            ulong userDelta = currentUser - _previousUser;
            _previousIdle = currentIdle;
            _previousKernel = currentKernel;
            _previousUser = currentUser;
            ulong totalDelta = kernelDelta + userDelta;
            if (totalDelta == 0) return null;
            double busy = totalDelta > idleDelta ? totalDelta - idleDelta : 0;
            return Math.Max(0, Math.Min(100, busy * 100.0 / totalDelta));
        }

        private static ulong ToUInt64(NativeFileTime value)
        {
            return ((ulong)value.High << 32) | value.Low;
        }
    }

    internal sealed class ProcessActivity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double CpuPercent { get; set; }
        public long WorkingSetBytes { get; set; }
        public bool Protected { get; set; }
        public string Impact { get; set; }
    }

    internal sealed class ProcessActivitySampler
    {
        private static readonly Dictionary<string, string> FriendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "msedge", "Microsoft Edge" },
            { "msedgewebview2", "Microsoft Edge WebView" },
            { "ms-teams", "Microsoft Teams" },
            { "olk", "Outlook" },
            { "chrome", "Google Chrome" },
            { "SearchHost", "Pesquisa do Windows" },
            { "StartMenuExperienceHost", "Menu Iniciar" },
            { "explorer", "Explorador do Windows" },
            { "dwm", "Gerenciador de Janelas" },
            { "MsMpEng", "Microsoft Defender" }
        };

        private static readonly HashSet<string> ProtectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Registry", "MsMpEng", "Sense", "OneDrive", "winlogon", "lsass", "csrss", "services", "svchost", "dwm",
            "explorer", "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost", "OfficeClickToRun", "olk", "ms-teams"
        };

        private sealed class Baseline
        {
            public TimeSpan CpuTime { get; set; }
        }

        private Dictionary<int, Baseline> _previous = new Dictionary<int, Baseline>();
        private DateTime _previousUtc;
        private readonly int _processorCount = Math.Max(1, Environment.ProcessorCount);
        private readonly int _ownProcessId;

        public ProcessActivitySampler()
        {
            using (Process current = Process.GetCurrentProcess()) _ownProcessId = current.Id;
        }

        public void Prime()
        {
            Capture(0, false);
        }

        public List<ProcessActivity> Sample(int maximum)
        {
            return Capture(Math.Max(0, maximum), true);
        }

        private List<ProcessActivity> Capture(int maximum, bool calculateUsage)
        {
            DateTime now = DateTime.UtcNow;
            double elapsedMilliseconds = _previousUtc == default(DateTime) ? 0 : (now - _previousUtc).TotalMilliseconds;
            var next = new Dictionary<int, Baseline>();
            var activities = new List<ProcessActivity>();
            Process[] processes;
            try { processes = Process.GetProcesses(); }
            catch { processes = new Process[0]; }

            foreach (Process process in processes)
            {
                try
                {
                    int id = process.Id;
                    string processName = process.ProcessName;
                    TimeSpan cpuTime = process.TotalProcessorTime;
                    long memory = process.WorkingSet64;
                    next[id] = new Baseline { CpuTime = cpuTime };
                    if (!calculateUsage || elapsedMilliseconds <= 0 || id == 0 || id == _ownProcessId || processName.StartsWith("codex-computer-use", StringComparison.OrdinalIgnoreCase)) continue;

                    Baseline baseline;
                    double cpu = 0;
                    if (_previous.TryGetValue(id, out baseline))
                    {
                        double delta = (cpuTime - baseline.CpuTime).TotalMilliseconds;
                        if (delta > 0) cpu = Math.Max(0, Math.Min(100, delta * 100.0 / (elapsedMilliseconds * _processorCount)));
                    }
                    if (cpu <= 0.01 && memory < 64L * 1024 * 1024) continue;
                    activities.Add(new ProcessActivity
                    {
                        Id = id,
                        Name = FriendlyName(processName),
                        CpuPercent = cpu,
                        WorkingSetBytes = Math.Max(0, memory),
                        Protected = IsProtected(processName),
                        Impact = Impact(cpu, memory)
                    });
                }
                catch { }
                finally { process.Dispose(); }
            }

            _previous = next;
            _previousUtc = now;
            if (!calculateUsage || maximum == 0) return new List<ProcessActivity>();

            List<ProcessActivity> grouped = activities
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    double cpu = Math.Min(100, group.Sum(item => item.CpuPercent));
                    long memory = group.Sum(item => item.WorkingSetBytes);
                    return new ProcessActivity
                    {
                        Id = group.First().Id,
                        Name = group.Key,
                        CpuPercent = cpu,
                        WorkingSetBytes = memory,
                        Protected = group.Any(item => item.Protected),
                        Impact = Impact(cpu, memory)
                    };
                })
                .ToList();

            var selected = grouped.OrderByDescending(item => item.CpuPercent).ThenByDescending(item => item.WorkingSetBytes).Take(2).ToList();
            ProcessActivity largestMemory = grouped.OrderByDescending(item => item.WorkingSetBytes).FirstOrDefault();
            if (largestMemory != null && selected.All(item => item.Id != largestMemory.Id)) selected.Add(largestMemory);
            foreach (ProcessActivity item in grouped.OrderByDescending(item => item.CpuPercent * 10 + item.WorkingSetBytes / 104857600.0))
            {
                if (selected.Count >= maximum) break;
                if (selected.All(existing => existing.Id != item.Id)) selected.Add(item);
            }
            return selected.Take(maximum).ToList();
        }

        private static string FriendlyName(string name)
        {
            string friendly;
            return FriendlyNames.TryGetValue(name, out friendly) ? friendly : name;
        }

        private static bool IsProtected(string name)
        {
            return ProtectedNames.Contains(name) || name.StartsWith("Veeam", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Intune", StringComparison.OrdinalIgnoreCase);
        }

        private static string Impact(double cpu, long memory)
        {
            if (cpu >= 20 || memory >= 1024L * 1024 * 1024) return "Alto";
            if (cpu >= 5 || memory >= 400L * 1024 * 1024) return "Médio";
            return "Baixo";
        }
    }

    internal sealed class SustainedAlert
    {
        public string Title { get; set; }
        public string Detail { get; set; }
    }

    internal sealed class SustainedAlertMonitor
    {
        private readonly TimeSpan _duration;
        private DateTime? _cpuSince;
        private DateTime? _memorySince;
        private DateTime? _diskSince;

        public SustainedAlertMonitor(TimeSpan duration)
        {
            _duration = duration;
        }

        public SustainedAlert Evaluate(SystemMetrics metrics)
        {
            DateTime now = DateTime.UtcNow;
            double freeMemoryPercent = metrics.TotalRamGb > 0 ? metrics.FreeRamGb * 100.0 / metrics.TotalRamGb : 100;
            bool cpuAlert = IsSustained(ref _cpuSince, metrics.CpuUsagePercent >= 90, now);
            bool memoryAlert = IsSustained(ref _memorySince, freeMemoryPercent <= 8, now);
            bool diskAlert = IsSustained(ref _diskSince, metrics.TotalDiskGb > 0 && metrics.FreeDiskPercent <= 5, now);
            if (cpuAlert) return new SustainedAlert { Title = "Processador em uso crítico", Detail = "Uso acima de 90% por pelo menos 20 segundos" };
            if (memoryAlert) return new SustainedAlert { Title = "Memória quase esgotada", Detail = "Menos de 8% disponível por pelo menos 20 segundos" };
            if (diskAlert) return new SustainedAlert { Title = "Espaço no disco crítico", Detail = "Menos de 5% livre por pelo menos 20 segundos" };
            return null;
        }

        private bool IsSustained(ref DateTime? since, bool condition, DateTime now)
        {
            if (!condition)
            {
                since = null;
                return false;
            }
            if (!since.HasValue) since = now;
            return now - since.Value >= _duration;
        }
    }

    internal sealed class BackupSnapshot
    {
        public string CreatedUtc { get; set; }
        public string Computer { get; set; }
        public string User { get; set; }
        public string PowerScheme { get; set; }
        public Dictionary<string, string> RegistryValues { get; set; }
        public Dictionary<string, string> StartupValues { get; set; }
    }

    internal sealed class StartupEntry
    {
        public bool Enabled { get; set; }
        public bool OriginalEnabled { get; set; }
        public bool CanChange { get; set; }
        public string Name { get; set; }
        public string Command { get; set; }
        public string Impact { get; set; }
        public string Source { get; set; }
        public string RegistryHive { get; set; }
        public string RegistryPath { get; set; }
        public string ApprovalPath { get; set; }
        public string ValueName { get; set; }
        public string StateKind { get; set; }
    }

    internal sealed class DriverUpdate
    {
        public bool Selected { get; set; }
        public string Title { get; set; }
        public string Provider { get; set; }
        public string UpdateId { get; set; }
        public long DownloadBytes { get; set; }
        public bool RebootRequired { get; set; }
        public string SupportName { get; set; }
        public string SupportUrl { get; set; }
        public string CatalogUrl { get; set; }
        public string HardwareId { get; set; }
        public string Model { get; set; }
        public string AvailableVersion { get; set; }
        public string AvailableDate { get; set; }
        public string InstalledVersion { get; set; }
        public string Comparison { get; set; }
        public string Classification { get; set; }
        public bool IsFirmware { get; set; }
        public bool IsOlderRisk { get; set; }
    }

    internal sealed class DriverInventoryItem
    {
        public string Category { get; set; }
        public string Device { get; set; }
        public string Provider { get; set; }
        public string Version { get; set; }
        public string Date { get; set; }
        public string InfName { get; set; }
        public string DeviceId { get; set; }
        public string HardwareId { get; set; }
        public string Status { get; set; }
        public int ProblemCode { get; set; }
        public bool Signed { get; set; }
        public bool HasProblem { get; set; }
    }

    internal sealed class DriverSafetyStatus
    {
        public bool IsAdministrator { get; set; }
        public bool AcConnected { get; set; }
        public bool HasBattery { get; set; }
        public int BatteryPercent { get; set; }
        public bool BitLockerProtectionOn { get; set; }
        public bool BitLockerKnown { get; set; }
        public bool PendingRestart { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string Summary { get; set; }
        public bool FirmwareSafe { get; set; }
    }

    internal sealed class ProgramUpdate
    {
        public bool Selected { get; set; }
        public string Name { get; set; }
        public string PackageId { get; set; }
        public string InstalledVersion { get; set; }
        public string AvailableVersion { get; set; }
        public string Source { get; set; }
    }

    internal sealed class PackagedStartupTask
    {
        public string PackageFamilyName { get; set; }
        public string Package { get; set; }
        public string AppId { get; set; }
        public string TaskId { get; set; }
        public string Command { get; set; }
        public bool DefaultEnabled { get; set; }
    }

    internal sealed class StorageEntry
    {
        public string Kind { get; set; }
        public string Path { get; set; }
        public long LogicalBytes { get; set; }
        public long AllocatedBytes { get; set; }
    }

    internal sealed class DuplicateEntry
    {
        public int Group { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public string Hash { get; set; }
    }

    internal sealed class ImportantHardware
    {
        public string Component { get; set; }
        public string Model { get; set; }
        public string Specifications { get; set; }
        public string Status { get; set; }
        public bool Warning { get; set; }
    }

    internal sealed class VolumeEntry
    {
        public string Drive { get; set; }
        public string Label { get; set; }
        public string FileSystem { get; set; }
        public string Health { get; set; }
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
        public long UsedBytes { get; set; }
        public double UsagePercent { get; set; }
    }

    internal sealed class CleanupTarget
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Pattern { get; set; }
        public long SizeBytes { get; set; }
        public bool DefaultSelected { get; set; }
    }

}
