using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Cache;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace CodexPerformanceOptimizer
{
    internal sealed class PerformanceComparison
    {
        public string CreatedUtc { get; set; }
        public string Operation { get; set; }
        public double BeforeFreeRamGb { get; set; }
        public double AfterFreeRamGb { get; set; }
        public double BeforeFreeDiskGb { get; set; }
        public double AfterFreeDiskGb { get; set; }
        public double BeforeCpuPercent { get; set; }
        public double AfterCpuPercent { get; set; }
        public long BootDurationMilliseconds { get; set; }
    }

    internal sealed class TemperatureReading
    {
        public string Name { get; set; }
        public double Celsius { get; set; }
        public string Source { get; set; }
    }

    internal sealed class DiskDiagnostic
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Health { get; set; }
        public string Life { get; set; }
        public string Temperature { get; set; }
        public bool Warning { get; set; }
    }

    internal sealed class StartupMeasurement
    {
        public string Name { get; set; }
        public long DurationMilliseconds { get; set; }
        public DateTime RecordedAt { get; set; }
    }

    internal sealed class StabilityDiagnostic
    {
        public int UnexpectedShutdowns { get; set; }
        public int SystemFailures { get; set; }
        public bool PendingRestart { get; set; }
        public TimeSpan Uptime { get; set; }
    }

    internal sealed class RecommendationItem
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        public string Severity { get; set; }
    }

    internal sealed class DiagnosticSnapshot
    {
        public List<TemperatureReading> Temperatures { get; set; }
        public List<DiskDiagnostic> Disks { get; set; }
        public List<StartupMeasurement> Startup { get; set; }
        public StabilityDiagnostic Stability { get; set; }
        public List<RecommendationItem> Recommendations { get; set; }
        public PerformanceComparison Comparison { get; set; }
        public string TrimStatus { get; set; }
        public string ClockStatus { get; set; }
        public string SignatureStatus { get; set; }
    }

    internal sealed class LargeFileEntry
    {
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime Modified { get; set; }
    }

    internal sealed class QuarantineEntry
    {
        public string OriginalPath { get; set; }
        public string QuarantinePath { get; set; }
        public long Size { get; set; }
    }

    internal sealed class AdvancedSettings
    {
        public bool MinimizeToTray { get; set; }
        public bool AutomaticPowerProfiles { get; set; }
        public string LastTemporaryPowerScheme { get; set; }
        public string UpdateManifestUrl { get; set; }
    }

    internal sealed class UpdateManifest
    {
        public string Version { get; set; }
        public string InstallerUrl { get; set; }
        public string Sha256 { get; set; }
        public string Notes { get; set; }
    }

    internal sealed class ReleaseChannel
    {
        public string ManifestUrl { get; set; }
    }

    internal sealed class UpdateCheckResult
    {
        public bool Available { get; set; }
        public string Message { get; set; }
        public UpdateManifest Manifest { get; set; }
    }

    internal sealed class ProcessHistorySummary
    {
        public string Name { get; set; }
        public double AverageCpu { get; set; }
        public double PeakCpu { get; set; }
        public long PeakMemory { get; set; }
        public int Samples { get; set; }
    }

    internal sealed class ProcessHistoryTracker
    {
        private sealed class HistoryPoint
        {
            public DateTime At { get; set; }
            public double Cpu { get; set; }
            public long Memory { get; set; }
        }

        private readonly Dictionary<string, List<HistoryPoint>> _history = new Dictionary<string, List<HistoryPoint>>(StringComparer.OrdinalIgnoreCase);

        public void Record(IEnumerable<ProcessActivity> activities)
        {
            DateTime now = DateTime.UtcNow;
            DateTime cutoff = now.AddMinutes(-5);
            foreach (ProcessActivity activity in activities ?? Enumerable.Empty<ProcessActivity>())
            {
                List<HistoryPoint> points;
                if (!_history.TryGetValue(activity.Name, out points))
                {
                    points = new List<HistoryPoint>();
                    _history[activity.Name] = points;
                }
                points.Add(new HistoryPoint { At = now, Cpu = activity.CpuPercent, Memory = activity.WorkingSetBytes });
            }
            foreach (string key in _history.Keys.ToArray())
            {
                _history[key].RemoveAll(point => point.At < cutoff);
                if (_history[key].Count == 0) _history.Remove(key);
            }
        }

        public List<ProcessHistorySummary> Summaries(int maximum)
        {
            return _history.Select(pair => new ProcessHistorySummary
            {
                Name = pair.Key,
                AverageCpu = pair.Value.Count == 0 ? 0 : pair.Value.Average(point => point.Cpu),
                PeakCpu = pair.Value.Count == 0 ? 0 : pair.Value.Max(point => point.Cpu),
                PeakMemory = pair.Value.Count == 0 ? 0 : pair.Value.Max(point => point.Memory),
                Samples = pair.Value.Count
            })
            .OrderByDescending(item => item.AverageCpu * 10 + item.PeakMemory / 104857600.0)
            .Take(Math.Max(0, maximum))
            .ToList();
        }
    }

    internal static class AdvancedEngine
    {
        private sealed class UpdateWebClient : WebClient
        {
            private readonly int _timeoutMilliseconds;

            public UpdateWebClient(int timeoutMilliseconds)
            {
                _timeoutMilliseconds = timeoutMilliseconds;
                Encoding = Encoding.UTF8;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                request.Timeout = _timeoutMilliseconds;
                request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
                var http = request as HttpWebRequest;
                if (http != null)
                {
                    http.ReadWriteTimeout = _timeoutMilliseconds;
                    http.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    http.UserAgent = "OtimizadorDeDesempenho/" + typeof(AdvancedEngine).Assembly.GetName().Version;
                }
                return request;
            }
        }

        private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Codex", "PerformanceOptimizer");
        private static readonly string ComparisonPath = Path.Combine(AppFolder, "comparison-v2.json");
        private static readonly string SettingsPath = Path.Combine(AppFolder, "advanced-settings.json");
        private static readonly string QuarantineFolder = Path.Combine(AppFolder, "Quarantine");

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        public static void SaveComparison(SystemMetrics before, SystemMetrics after, string operation)
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                var comparison = new PerformanceComparison
                {
                    CreatedUtc = DateTime.UtcNow.ToString("o"),
                    Operation = operation,
                    BeforeFreeRamGb = before.FreeRamGb,
                    AfterFreeRamGb = after.FreeRamGb,
                    BeforeFreeDiskGb = before.FreeDiskGb,
                    AfterFreeDiskGb = after.FreeDiskGb,
                    BeforeCpuPercent = before.CpuUsagePercent,
                    AfterCpuPercent = after.CpuUsagePercent,
                    BootDurationMilliseconds = ReadBootMeasurements().Where(item => item.Name == "Inicialização do Windows").Select(item => item.DurationMilliseconds).FirstOrDefault()
                };
                WriteJson(ComparisonPath, comparison);
            }
            catch { }
        }

        public static PerformanceComparison ReadComparison()
        {
            try { return File.Exists(ComparisonPath) ? ReadJson<PerformanceComparison>(ComparisonPath) : null; }
            catch { return null; }
        }

        public static DiagnosticSnapshot ReadDiagnostics()
        {
            SystemMetrics metrics = V2Engine.ReadMetrics();
            List<TemperatureReading> temperatures = ReadTemperatures();
            List<DiskDiagnostic> disks = ReadDiskDiagnostics();
            List<StartupMeasurement> startup = ReadBootMeasurements();
            StabilityDiagnostic stability = ReadStability();
            return new DiagnosticSnapshot
            {
                Temperatures = temperatures,
                Disks = disks,
                Startup = startup,
                Stability = stability,
                Comparison = ReadComparison(),
                TrimStatus = ReadTrimStatus(),
                ClockStatus = ReadClockStatus(metrics),
                SignatureStatus = ReadSignatureStatus(Application.ExecutablePath),
                Recommendations = BuildRecommendations(metrics, temperatures, disks, startup, stability)
            };
        }

        public static List<TemperatureReading> ReadTemperatures()
        {
            List<TemperatureReading> readings = OptionalSensorProvider.ReadTemperatures();
            if (readings.Count > 0) return readings;
            ReadTemperatureNamespace(@"root\LibreHardwareMonitor", "LibreHardwareMonitor", readings);
            if (readings.Count == 0) ReadTemperatureNamespace(@"root\OpenHardwareMonitor", "OpenHardwareMonitor", readings);
            if (readings.Count == 0)
            {
                try
                {
                    var scope = new ManagementScope(@"\\.\root\wmi");
                    scope.Connect();
                    using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT CurrentTemperature,InstanceName FROM MSAcpi_ThermalZoneTemperature")))
                    foreach (ManagementObject sensor in searcher.Get())
                    {
                        double raw = Convert.ToDouble(sensor["CurrentTemperature"] ?? 0, CultureInfo.InvariantCulture);
                        double celsius = raw / 10.0 - 273.15;
                        if (celsius > 0 && celsius < 150) readings.Add(new TemperatureReading { Name = "Zona térmica", Celsius = celsius, Source = "ACPI" });
                    }
                }
                catch { }
            }
            return readings.OrderByDescending(item => item.Celsius).Take(8).ToList();
        }

        private static void ReadTemperatureNamespace(string path, string source, List<TemperatureReading> readings)
        {
            try
            {
                var scope = new ManagementScope("\\\\.\\" + path);
                scope.Connect();
                using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT Name,Value FROM Sensor WHERE SensorType='Temperature'")))
                foreach (ManagementObject sensor in searcher.Get())
                {
                    double value = Convert.ToDouble(sensor["Value"] ?? 0, CultureInfo.InvariantCulture);
                    if (value > 0 && value < 150) readings.Add(new TemperatureReading { Name = Convert.ToString(sensor["Name"]), Celsius = value, Source = source });
                }
            }
            catch { }
        }

        public static List<DiskDiagnostic> ReadDiskDiagnostics()
        {
            var disks = new List<DiskDiagnostic>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Model,MediaType,InterfaceType,Size,Status FROM Win32_DiskDrive"))
                foreach (ManagementObject disk in searcher.Get())
                {
                    string model = Convert.ToString(disk["Model"]).Trim();
                    string media = Convert.ToString(disk["MediaType"]);
                    string type = model.IndexOf("NVMe", StringComparison.OrdinalIgnoreCase) >= 0 ? "NVMe" : (!string.IsNullOrWhiteSpace(media) ? media : Convert.ToString(disk["InterfaceType"]));
                    string status = Convert.ToString(disk["Status"]);
                    bool warning = !string.IsNullOrWhiteSpace(status) && !status.Equals("OK", StringComparison.OrdinalIgnoreCase);
                    disks.Add(new DiskDiagnostic { Name = model, Type = type, Health = string.IsNullOrWhiteSpace(status) ? "Não informado" : status, Life = "Não informado pelo dispositivo", Temperature = "Não informada", Warning = warning });
                }
            }
            catch { }

            try
            {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                scope.Connect();
                var reliability = new List<ManagementObject>();
                using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT Temperature,Wear,ReadErrorsTotal,WriteErrorsTotal FROM MSFT_StorageReliabilityCounter")))
                    reliability.AddRange(searcher.Get().Cast<ManagementObject>());
                if (reliability.Count == disks.Count) for (int i = 0; i < disks.Count; i++)
                {
                    ManagementObject value = reliability[i];
                    if (value["Wear"] != null)
                    {
                        int wear = Convert.ToInt32(value["Wear"], CultureInfo.InvariantCulture);
                        disks[i].Life = Math.Max(0, 100 - wear) + "% estimado";
                        disks[i].Warning = disks[i].Warning || wear >= 90;
                    }
                    if (value["Temperature"] != null)
                    {
                        int temperature = Convert.ToInt32(value["Temperature"], CultureInfo.InvariantCulture);
                        if (temperature > 0) disks[i].Temperature = temperature + " °C";
                        disks[i].Warning = disks[i].Warning || temperature >= 65;
                    }
                    value.Dispose();
                }
                else foreach (ManagementObject value in reliability) value.Dispose();
            }
            catch { }
            return disks;
        }

        public static List<StartupMeasurement> ReadBootMeasurements()
        {
            string xml = RunCommand("wevtutil.exe", "qe Microsoft-Windows-Diagnostics-Performance/Operational /q:\"*[System[(EventID=100 or EventID=101 or EventID=102 or EventID=103 or EventID=106 or EventID=110)]]\" /c:50 /rd:true /f:xml", 30000);
            var rows = new List<StartupMeasurement>();
            foreach (Match eventMatch in Regex.Matches(xml, @"<Event[\s\S]*?</Event>", RegexOptions.IgnoreCase))
            {
                string block = eventMatch.Value;
                int eventId = ParseInt(FirstMatch(block, @"<EventID[^>]*>(\d+)</EventID>"));
                DateTime recorded = ParseDate(FirstMatch(block, @"SystemTime=(?:'|"")([^'""]+)(?:'|"")"));
                if (eventId == 100)
                {
                    long bootTime = ParseLong(DataValue(block, "BootTime"));
                    if (bootTime > 0) rows.Add(new StartupMeasurement { Name = "Inicialização do Windows", DurationMilliseconds = bootTime, RecordedAt = recorded });
                    continue;
                }
                string name = DataValue(block, "FriendlyName");
                if (string.IsNullOrWhiteSpace(name)) name = DataValue(block, "FileName");
                if (string.IsNullOrWhiteSpace(name)) name = DataValue(block, "Name");
                long duration = ParseLong(DataValue(block, "TotalTime"));
                if (duration == 0) duration = ParseLong(DataValue(block, "DegradationTime"));
                if (!string.IsNullOrWhiteSpace(name) && duration > 0) rows.Add(new StartupMeasurement { Name = WebUtility.HtmlDecode(name), DurationMilliseconds = duration, RecordedAt = recorded });
            }
            return rows.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.RecordedAt).First())
                .OrderByDescending(item => item.DurationMilliseconds)
                .Take(12)
                .ToList();
        }

        public static StabilityDiagnostic ReadStability()
        {
            string xml = RunCommand("wevtutil.exe", "qe System /q:\"*[System[(EventID=41 or EventID=6008 or EventID=1001) and TimeCreated[timediff(@SystemTime) <= 2592000000]]]\" /c:100 /rd:true /f:xml", 30000);
            int shutdowns = Regex.Matches(xml, @"<EventID[^>]*>(41|6008)</EventID>", RegexOptions.IgnoreCase).Count;
            int failures = Regex.Matches(xml, @"<EventID[^>]*>1001</EventID>", RegexOptions.IgnoreCase).Count;
            return new StabilityDiagnostic
            {
                UnexpectedShutdowns = shutdowns,
                SystemFailures = failures,
                PendingRestart = IsRestartPending(),
                Uptime = TimeSpan.FromMilliseconds(GetTickCount64())
            };
        }

        private static bool IsRestartPending()
        {
            try
            {
                using (RegistryKey update = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")) if (update != null) return true;
                using (RegistryKey servicing = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending")) if (servicing != null) return true;
                using (RegistryKey session = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager"))
                    return session != null && session.GetValue("PendingFileRenameOperations") != null;
            }
            catch { return false; }
        }

        private static List<RecommendationItem> BuildRecommendations(SystemMetrics metrics, List<TemperatureReading> temperatures, List<DiskDiagnostic> disks, List<StartupMeasurement> startup, StabilityDiagnostic stability)
        {
            var items = new List<RecommendationItem>();
            double freeRam = metrics.TotalRamGb > 0 ? metrics.FreeRamGb * 100.0 / metrics.TotalRamGb : 100;
            if (metrics.FreeDiskPercent < 10) items.Add(new RecommendationItem { Title = "Liberar espaço no disco C:", Detail = "Há apenas " + metrics.FreeDiskPercent.ToString("N1", CultureInfo.CurrentCulture) + "% livre.", Severity = "Alta" });
            if (freeRam < 12) items.Add(new RecommendationItem { Title = "Reduzir aplicativos simultâneos", Detail = "A memória disponível está abaixo de 12%.", Severity = "Alta" });
            TemperatureReading hottest = temperatures.OrderByDescending(item => item.Celsius).FirstOrDefault();
            if (hottest != null && hottest.Celsius >= 85) items.Add(new RecommendationItem { Title = "Verificar refrigeração", Detail = hottest.Name + " chegou a " + hottest.Celsius.ToString("N0", CultureInfo.CurrentCulture) + " °C.", Severity = "Alta" });
            if (disks.Any(item => item.Warning)) items.Add(new RecommendationItem { Title = "Verificar a saúde do armazenamento", Detail = "Um dispositivo reportou temperatura, desgaste ou estado fora do normal.", Severity = "Alta" });
            StartupMeasurement boot = startup.FirstOrDefault(item => item.Name == "Inicialização do Windows");
            if (boot != null && boot.DurationMilliseconds > 60000) items.Add(new RecommendationItem { Title = "Revisar a inicialização", Detail = "A última medição disponível passou de 60 segundos.", Severity = "Média" });
            if (stability.PendingRestart) items.Add(new RecommendationItem { Title = "Reiniciar o Windows", Detail = "Há uma atualização ou alteração pendente de reinicialização.", Severity = "Média" });
            if (stability.UnexpectedShutdowns > 0) items.Add(new RecommendationItem { Title = "Investigar desligamentos inesperados", Detail = stability.UnexpectedShutdowns + " evento(s) nos últimos 30 dias.", Severity = "Média" });
            if (items.Count == 0) items.Add(new RecommendationItem { Title = "Nenhuma ação urgente", Detail = "Os indicadores disponíveis estão dentro dos limites definidos.", Severity = "OK" });
            return items;
        }

        public static List<LargeFileEntry> FindLargeFiles(string root, CancellationToken token, IProgress<string> progress)
        {
            string fullRoot = Path.GetFullPath(root.EndsWith("\\", StringComparison.Ordinal) ? root : root + "\\");
            var largest = new List<LargeFileEntry>();
            var pending = new Stack<string>();
            pending.Push(fullRoot);
            int scanned = 0;
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string directory = pending.Pop();
                try
                {
                    foreach (string child in Directory.GetDirectories(directory))
                    {
                        try { if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child); } catch { }
                    }
                    foreach (string file in Directory.GetFiles(directory))
                    {
                        token.ThrowIfCancellationRequested();
                        scanned++;
                        try
                        {
                            var info = new FileInfo(file);
                            if (info.Length >= 100L * 1024 * 1024) largest.Add(new LargeFileEntry { Path = file, Size = info.Length, Modified = info.LastWriteTime });
                        }
                        catch { }
                        if (largest.Count > 200) largest = largest.OrderByDescending(item => item.Size).Take(100).ToList();
                        if (scanned % 1000 == 0) progress.Report("Mapeando arquivos grandes: " + scanned.ToString("N0", CultureInfo.CurrentCulture) + " verificados...");
                    }
                }
                catch { }
            }
            return largest.OrderByDescending(item => item.Size).Take(100).ToList();
        }

        public static string QuarantineDuplicates(IEnumerable<DuplicateEntry> selected, CancellationToken token, IProgress<string> progress)
        {
            List<DuplicateEntry> files = (selected ?? Enumerable.Empty<DuplicateEntry>()).Where(item => File.Exists(item.Path)).ToList();
            if (files.Count == 0) return "Nenhum arquivo foi selecionado.";
            string batch = Path.Combine(QuarantineFolder, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(batch);
            var moved = new List<QuarantineEntry>();
            foreach (DuplicateEntry entry in files)
            {
                token.ThrowIfCancellationRequested();
                progress.Report("Movendo " + Path.GetFileName(entry.Path) + " para a quarentena...");
                try
                {
                    string safeName = entry.Group + "-" + entry.Hash.Substring(0, Math.Min(8, entry.Hash.Length)) + "-" + Path.GetFileName(entry.Path);
                    string destination = UniquePath(Path.Combine(batch, safeName));
                    File.Move(entry.Path, destination);
                    moved.Add(new QuarantineEntry { OriginalPath = entry.Path, QuarantinePath = destination, Size = entry.Size });
                }
                catch { }
            }
            WriteJson(Path.Combine(batch, "manifest.json"), moved);
            return "DUPLICADOS MOVIDOS PARA QUARENTENA\r\n" + new string('=', 72) + "\r\nArquivos: " + moved.Count + "\r\nEspaço: " + V2Engine.FormatBytes(moved.Sum(item => item.Size)) + "\r\nPasta: " + batch;
        }

        public static string RestoreLatestQuarantine()
        {
            if (!Directory.Exists(QuarantineFolder)) return "A quarentena está vazia.";
            string batch = Directory.GetDirectories(QuarantineFolder).OrderByDescending(path => path).FirstOrDefault();
            if (string.IsNullOrEmpty(batch) || !File.Exists(Path.Combine(batch, "manifest.json"))) return "Nenhum lote restaurável foi encontrado.";
            List<QuarantineEntry> entries = ReadJson<List<QuarantineEntry>>(Path.Combine(batch, "manifest.json"));
            int restored = 0;
            foreach (QuarantineEntry entry in entries)
            {
                try
                {
                    if (!File.Exists(entry.QuarantinePath) || File.Exists(entry.OriginalPath)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(entry.OriginalPath));
                    File.Move(entry.QuarantinePath, entry.OriginalPath);
                    restored++;
                }
                catch { }
            }
            return restored + " arquivo(s) restaurado(s) do último lote.";
        }

        public static AdvancedSettings ReadSettings()
        {
            try { return File.Exists(SettingsPath) ? ReadJson<AdvancedSettings>(SettingsPath) : new AdvancedSettings(); }
            catch { return new AdvancedSettings(); }
        }

        public static void SaveSettings(AdvancedSettings settings)
        {
            Directory.CreateDirectory(AppFolder);
            WriteJson(SettingsPath, settings ?? new AdvancedSettings());
        }

        public static string ApplyTemporaryProfile(bool gameMode)
        {
            AdvancedSettings settings = ReadSettings();
            if (string.IsNullOrWhiteSpace(settings.LastTemporaryPowerScheme)) settings.LastTemporaryPowerScheme = ExtractGuid(RunCommand("powercfg.exe", "/getactivescheme", 15000));
            string target = gameMode ? "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" : "381b4222-f694-41f0-9685-ff5bb260df2e";
            string result = RunCommand("powercfg.exe", "/setactive " + target, 15000);
            SaveSettings(settings);
            return string.IsNullOrWhiteSpace(result) ? (gameMode ? "Modo jogo ativado temporariamente." : "Modo trabalho equilibrado ativado.") : result;
        }

        public static string RestoreTemporaryProfile()
        {
            AdvancedSettings settings = ReadSettings();
            if (string.IsNullOrWhiteSpace(settings.LastTemporaryPowerScheme)) return "Nenhum perfil temporário está ativo.";
            string result = RunCommand("powercfg.exe", "/setactive " + settings.LastTemporaryPowerScheme, 15000);
            settings.LastTemporaryPowerScheme = null;
            SaveSettings(settings);
            return string.IsNullOrWhiteSpace(result) ? "Plano de energia anterior restaurado." : result;
        }

        public static void ApplyAutomaticPowerProfile(bool onAcPower)
        {
            string target = onAcPower ? "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" : "381b4222-f694-41f0-9685-ff5bb260df2e";
            RunCommand("powercfg.exe", "/setactive " + target, 15000);
        }

        public static UpdateCheckResult CheckForUpdates()
        {
            AdvancedSettings settings = ReadSettings();
            string source = settings.UpdateManifestUrl;
            if (string.IsNullOrWhiteSpace(source))
            {
                string channelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "release-channel.json");
                try
                {
                    if (File.Exists(channelPath))
                    {
                        ReleaseChannel channel = new JavaScriptSerializer().Deserialize<ReleaseChannel>(File.ReadAllText(channelPath, Encoding.UTF8));
                        if (channel != null) source = channel.ManifestUrl;
                    }
                }
                catch { }
            }
            if (string.IsNullOrWhiteSpace(source)) source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update-manifest.json");
            try
            {
                string json;
                if (Uri.IsWellFormedUriString(source, UriKind.Absolute))
                {
                    var uri = new Uri(source);
                    if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return new UpdateCheckResult { Message = "O canal de atualização deve usar HTTPS." };
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    using (var client = new UpdateWebClient(20000)) json = client.DownloadString(uri);
                }
                else
                {
                    if (!File.Exists(source)) return new UpdateCheckResult { Message = "Canal de atualização ainda não configurado." };
                    json = File.ReadAllText(source, Encoding.UTF8);
                }
                UpdateManifest manifest = new JavaScriptSerializer().Deserialize<UpdateManifest>(json);
                Version remote;
                Version current = typeof(AdvancedEngine).Assembly.GetName().Version;
                if (manifest == null || !Version.TryParse(manifest.Version, out remote)) return new UpdateCheckResult { Message = "Manifesto de atualização inválido." };
                bool available = remote > current;
                Uri installerUri;
                if (available && (!Uri.TryCreate(manifest.InstallerUrl, UriKind.Absolute, out installerUri) ||
                    !installerUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                    !Regex.IsMatch(manifest.Sha256 ?? string.Empty, "^[0-9a-fA-F]{64}$")))
                    return new UpdateCheckResult { Message = "A atualização publicada está incompleta ou não passou na validação de segurança." };
                return new UpdateCheckResult { Available = available, Manifest = manifest, Message = available ? "Versão " + remote + " disponível." : "Você já está na versão mais recente (" + current + ")." };
            }
            catch (Exception ex) { return new UpdateCheckResult { Message = "Não foi possível verificar: " + ex.Message }; }
        }

        public static string DownloadVerifiedUpdate(UpdateManifest manifest)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.InstallerUrl) || !Uri.IsWellFormedUriString(manifest.InstallerUrl, UriKind.Absolute)) return "O manifesto não contém um instalador válido.";
            var uri = new Uri(manifest.InstallerUrl);
            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return "O instalador precisa ser fornecido por HTTPS.";
            if (!Regex.IsMatch(manifest.Sha256 ?? string.Empty, "^[0-9a-fA-F]{64}$")) return "O manifesto não contém um SHA-256 válido.";
            string destination = Path.Combine(Path.GetTempPath(), "InstalarOtimizador-" + manifest.Version + ".exe");
            try
            {
                if (File.Exists(destination)) File.Delete(destination);
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var client = new UpdateWebClient(120000)) client.DownloadFile(uri, destination);
                string actual;
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(destination)) actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                if (!actual.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(destination);
                    return "A atualização foi rejeitada porque a verificação SHA-256 falhou.";
                }
                Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true });
                return "Instalador verificado e iniciado.";
            }
            catch (Exception ex)
            {
                try { if (File.Exists(destination)) File.Delete(destination); } catch { }
                return "Não foi possível baixar a atualização: " + ex.Message;
            }
        }

        public static string ReadReleaseNotes()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "release-notes.txt");
            try { return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "Notas da versão não encontradas."; }
            catch (Exception ex) { return "Não foi possível ler as notas: " + ex.Message; }
        }

        public static string ReadSignatureStatus(string path)
        {
            try
            {
                X509Certificate certificate = X509Certificate.CreateFromSignedFile(path);
                using (var certificate2 = new X509Certificate2(certificate)) return "Assinado por " + certificate2.GetNameInfo(X509NameType.SimpleName, false);
            }
            catch { return "Sem assinatura digital confiável"; }
        }

        private static string ReadTrimStatus()
        {
            string result = RunCommand("fsutil.exe", "behavior query DisableDeleteNotify", 15000);
            if (Regex.IsMatch(result, @"=\s*0")) return "TRIM habilitado";
            if (Regex.IsMatch(result, @"=\s*1")) return "TRIM desabilitado";
            return "TRIM não informado";
        }

        private static string ReadClockStatus(SystemMetrics metrics)
        {
            try
            {
                double current = 0;
                double maximum = 0;
                using (var searcher = new ManagementObjectSearcher("SELECT CurrentClockSpeed,MaxClockSpeed FROM Win32_Processor"))
                foreach (ManagementObject cpu in searcher.Get())
                {
                    current += Convert.ToDouble(cpu["CurrentClockSpeed"] ?? 0, CultureInfo.InvariantCulture);
                    maximum += Convert.ToDouble(cpu["MaxClockSpeed"] ?? 0, CultureInfo.InvariantCulture);
                }
                if (maximum <= 0) return "Frequência não informada";
                double ratio = current * 100.0 / maximum;
                string note = ratio < 60 && metrics.CpuUsagePercent >= 70 ? " • possível limitação" : string.Empty;
                return ratio.ToString("N0", CultureInfo.CurrentCulture) + "% da frequência nominal" + note;
            }
            catch { return "Frequência não informada"; }
        }

        private static string RunCommand(string file, string arguments, int timeout)
        {
            return SystemCommand.Run(file, arguments, timeout);
        }

        private static string DataValue(string xml, string name)
        {
            return FirstMatch(xml, @"<Data\s+Name=(?:'|"")" + Regex.Escape(name) + @"(?:'|"")>([\s\S]*?)</Data>");
        }

        private static string FirstMatch(string text, string pattern)
        {
            Match match = Regex.Match(text ?? string.Empty, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static int ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        private static long ParseLong(string value)
        {
            long parsed;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        private static DateTime ParseDate(string value)
        {
            DateTime parsed;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out parsed) ? parsed.ToLocalTime() : DateTime.MinValue;
        }

        private static string ExtractGuid(string value)
        {
            Match match = Regex.Match(value ?? string.Empty, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return match.Success ? match.Value : string.Empty;
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string directory = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            for (int i = 2; i < 10000; i++)
            {
                string candidate = Path.Combine(directory, name + " (" + i + ")" + extension);
                if (!File.Exists(candidate)) return candidate;
            }
            return Path.Combine(directory, Guid.NewGuid().ToString("N") + extension);
        }

        private static T ReadJson<T>(string path)
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8));
        }

        private static void WriteJson(string path, object value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(value), Encoding.UTF8);
        }
    }

    internal sealed class DuplicateReviewForm : Form
    {
        private readonly CheckedListBox _items;
        private readonly List<DuplicateEntry> _entries;

        public List<DuplicateEntry> SelectedEntries
        {
            get
            {
                var selected = new List<DuplicateEntry>();
                for (int i = 0; i < _entries.Count; i++) if (_items.GetItemChecked(i)) selected.Add(_entries[i]);
                return selected;
            }
        }

        public DuplicateReviewForm(List<DuplicateEntry> entries)
        {
            _entries = entries ?? new List<DuplicateEntry>();
            Text = "Revisar arquivos duplicados";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(780, 520);
            Size = new Size(900, 620);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);

            Controls.Add(new Label { Text = "Duplicados confirmados por SHA-256", Location = new Point(22, 18), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 14f) });
            Controls.Add(new Label { Text = "Um arquivo de cada grupo é preservado. Os selecionados irão para uma quarentena reversível.", Location = new Point(24, 52), AutoSize = true, ForeColor = Theme.Muted });
            _items = new CheckedListBox { Location = new Point(24, 86), Size = new Size(836, 420), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackColor = Theme.SurfaceDark, ForeColor = Theme.Text, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle };
            int currentGroup = -1;
            for (int i = 0; i < _entries.Count; i++)
            {
                DuplicateEntry entry = _entries[i];
                bool canQuarantine = entry.Group == currentGroup;
                currentGroup = entry.Group;
                _items.Items.Add("Grupo " + entry.Group + "  •  " + V2Engine.FormatBytes(entry.Size) + "  •  " + entry.Path, canQuarantine);
            }
            var move = new Button { Text = "Mover para quarentena", DialogResult = DialogResult.OK, Size = new Size(190, 38), Location = new Point(670, 526), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, BackColor = Theme.Warning, ForeColor = Theme.ButtonText, FlatStyle = FlatStyle.Flat };
            var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Size = new Size(100, 38), Location = new Point(558, 526), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, BackColor = Theme.Secondary, ForeColor = Theme.ButtonText, FlatStyle = FlatStyle.Flat };
            move.FlatAppearance.BorderSize = cancel.FlatAppearance.BorderSize = 0;
            Controls.Add(_items);
            Controls.Add(cancel);
            Controls.Add(move);
            AcceptButton = move;
            CancelButton = cancel;
        }
    }
}
