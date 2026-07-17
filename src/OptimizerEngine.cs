using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace CodexPerformanceOptimizer
{
    internal static class V2Engine
    {
        private const string AppKey = @"Software\Codex\PerformanceOptimizerV2";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string DisabledKey = AppKey + @"\DisabledStartup";
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string VisualKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
        private const string ExplorerKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string OfficeKey = @"Software\Microsoft\Office\16.0\Common";
        private const string BackgroundAppsKey = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";
        private const string GameConfigKey = @"System\GameConfigStore";
        private const string GameDvrKey = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
        private const string ContentDeliveryKey = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        private const string EngagementKey = @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement";
        private const string EdgePolicyKey = @"Software\Policies\Microsoft\Edge";
        private static readonly string[] NonEssentialBackgroundPackages =
        {
            "MicrosoftWindows.Client.WebExperience_cw5n1h2txyewy",
            "Microsoft.XboxGamingOverlay_8wekyb3d8bbwe",
            "Microsoft.GamingApp_8wekyb3d8bbwe",
            "Microsoft.BingNews_8wekyb3d8bbwe",
            "Microsoft.BingWeather_8wekyb3d8bbwe",
            "Clipchamp.Clipchamp_yxz26nhyzhsrt",
            "Microsoft.WindowsFeedbackHub_8wekyb3d8bbwe",
            "Microsoft.GetHelp_8wekyb3d8bbwe",
            "Microsoft.MicrosoftSolitaireCollection_8wekyb3d8bbwe",
            "Microsoft.YourPhone_8wekyb3d8bbwe"
        };
        private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Codex", "PerformanceOptimizer");
        public static readonly string SnapshotPath = Path.Combine(AppFolder, "state-v2.json");
        private static readonly string ReportsFolder = Path.Combine(AppFolder, "Reports");

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetCompressedFileSizeW(string fileName, out uint high);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);

        public static string BuildFullAudit()
        {
            return BuildFullAudit(CancellationToken.None, new Progress<string>());
        }

        public static string BuildFullAudit(CancellationToken token, IProgress<string> progress)
        {
            progress.Report("Lendo hardware e políticas...");
            token.ThrowIfCancellationRequested();
            var sb = new StringBuilder();
            SystemMetrics m = ReadMetrics();
            sb.AppendLine("AUDITORIA 3.3");
            sb.AppendLine(new string('=', 72));
            sb.AppendLine("Data: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            sb.AppendLine("Administrador: " + (Optimizer.IsAdministrator() ? "sim" : "não"));
            sb.AppendLine("Ambiente: " + DetectManagedEnvironmentLong());
            sb.AppendLine(string.Format(CultureInfo.CurrentCulture, "Memória: {0:N1} GB livres de {1:N1} GB", m.FreeRamGb, m.TotalRamGb));
            sb.AppendLine(string.Format(CultureInfo.CurrentCulture, "Processador: {0:N0}% em uso", m.CpuUsagePercent));
            sb.AppendLine(string.Format(CultureInfo.CurrentCulture, "Disco C: {0:N1} GB livres de {1:N1} GB ({2:N1}%)", m.FreeDiskGb, m.TotalDiskGb, m.FreeDiskPercent));
            sb.AppendLine("Energia: " + m.PowerScheme);
            sb.AppendLine("Tema: " + (ReadRegistryValue(PersonalizeKey, "AppsUseLightTheme") == "0" ? "escuro" : "claro ou padrão"));
            sb.AppendLine("Backup base: " + (File.Exists(SnapshotPath) ? SnapshotPath : "será criado antes da primeira alteração"));
            sb.AppendLine();
            sb.AppendLine("INICIALIZAÇÃO DO USUÁRIO");
            foreach (StartupEntry item in ReadStartupEntries()) sb.AppendLine(string.Format("{0,-7} {1,-36} impacto: {2}", item.Enabled ? "ativo" : "inativo", item.Name, item.Impact));
            sb.AppendLine();
            sb.AppendLine("Protegidos: OneDrive, arquivos pessoais, Veeam, Defender, Intune e políticas corporativas.");
            return sb.ToString();
        }

        public static SystemMetrics ReadMetrics()
        {
            var m = new SystemMetrics();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem"))
                foreach (ManagementObject os in searcher.Get())
                {
                    m.TotalRamGb = Convert.ToDouble(os["TotalVisibleMemorySize"], CultureInfo.InvariantCulture) / 1048576.0;
                    m.FreeRamGb = Convert.ToDouble(os["FreePhysicalMemory"], CultureInfo.InvariantCulture) / 1048576.0;
                }
            }
            catch { }
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor"))
                foreach (ManagementObject cpu in searcher.Get())
                {
                    m.CpuCores += Convert.ToInt32(cpu["NumberOfCores"] ?? 0, CultureInfo.InvariantCulture);
                    m.CpuThreads += Convert.ToInt32(cpu["NumberOfLogicalProcessors"] ?? 0, CultureInfo.InvariantCulture);
                }
                using (var searcher = new ManagementObjectSearcher("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'"))
                foreach (ManagementObject cpu in searcher.Get())
                    m.CpuUsagePercent = Math.Max(0, Math.Min(100, Convert.ToDouble(cpu["PercentProcessorTime"] ?? 0, CultureInfo.InvariantCulture)));
            }
            catch { }
            try
            {
                var d = new DriveInfo("C");
                m.FreeDiskGb = d.AvailableFreeSpace / 1073741824.0;
                m.TotalDiskGb = d.TotalSize / 1073741824.0;
                m.FreeDiskPercent = d.AvailableFreeSpace * 100.0 / d.TotalSize;
            }
            catch { }
            m.PowerScheme = OneLine(Run("powercfg.exe", "/getactivescheme", 15000));
            return m;
        }

        public static List<ImportantHardware> ReadImportantHardware(CancellationToken token, IProgress<string> progress)
        {
            var items = new List<ImportantHardware>();
            progress.Report("Lendo processador...");
            token.ThrowIfCancellationRequested();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed,L3CacheSize,SocketDesignation FROM Win32_Processor"))
                foreach (ManagementObject cpu in searcher.Get())
                    items.Add(new ImportantHardware { Component = "PROCESSADOR", Model = Convert.ToString(cpu["Name"]).Trim(), Specifications = cpu["NumberOfCores"] + " núcleos • " + cpu["NumberOfLogicalProcessors"] + " threads • L3 " + (Convert.ToDouble(cpu["L3CacheSize"] ?? 0) / 1024.0).ToString("N0", CultureInfo.CurrentCulture) + " MB • soquete " + cpu["SocketDesignation"], Status = "Funcionando normalmente" });
            }
            catch { }

            progress.Report("Lendo memória...");
            token.ThrowIfCancellationRequested();
            try
            {
                int slots = 0;
                int modules = 0;
                long total = 0;
                int speed = 0;
                var parts = new List<string>();
                using (var searcher = new ManagementObjectSearcher("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray")) foreach (ManagementObject array in searcher.Get()) slots = Math.Max(slots, Convert.ToInt32(array["MemoryDevices"] ?? 0));
                using (var searcher = new ManagementObjectSearcher("SELECT Capacity,Speed,ConfiguredClockSpeed,PartNumber FROM Win32_PhysicalMemory"))
                foreach (ManagementObject memory in searcher.Get())
                {
                    modules++;
                    total += Convert.ToInt64(memory["Capacity"] ?? 0);
                    speed = Math.Max(speed, Convert.ToInt32(memory["ConfiguredClockSpeed"] ?? memory["Speed"] ?? 0));
                    string part = Convert.ToString(memory["PartNumber"]).Trim();
                    if (!string.IsNullOrEmpty(part)) parts.Add(part);
                }
                bool warning = modules == 1 && slots > 1;
                items.Add(new ImportantHardware { Component = "MEMÓRIA", Model = FormatBytes(total) + " DDR4", Specifications = modules + " módulo(s) • " + modules + " de " + slots + " slots • " + speed + " MT/s" + (parts.Count > 0 ? " • " + string.Join(", ", parts.ToArray()) : string.Empty), Status = warning ? "Adicionar outro módulo igual habilita dual-channel" : "Configuração adequada", Warning = warning });
            }
            catch { }

            progress.Report("Lendo vídeo...");
            token.ThrowIfCancellationRequested();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name,AdapterRAM,DriverVersion,DriverDate,CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate FROM Win32_VideoController"))
                foreach (ManagementObject gpu in searcher.Get())
                {
                    string resolution = gpu["CurrentHorizontalResolution"] == null ? "" : " • " + gpu["CurrentHorizontalResolution"] + "×" + gpu["CurrentVerticalResolution"] + " @ " + gpu["CurrentRefreshRate"] + " Hz";
                    string driverDate = FormatWmiValue(gpu["DriverDate"]);
                    if (!string.IsNullOrEmpty(driverDate)) driverDate = driverDate.Split(' ')[0];
                    items.Add(new ImportantHardware { Component = "VÍDEO", Model = Convert.ToString(gpu["Name"]), Specifications = "Memória reservada " + FormatBytes(Convert.ToInt64(gpu["AdapterRAM"] ?? 0)) + resolution + " • driver " + gpu["DriverVersion"], Status = string.IsNullOrEmpty(driverDate) ? "Driver instalado" : "Driver de " + driverDate });
                }
            }
            catch { }

            progress.Report("Lendo armazenamento...");
            token.ThrowIfCancellationRequested();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Model,Size,InterfaceType,MediaType,FirmwareRevision,Status FROM Win32_DiskDrive"))
                foreach (ManagementObject disk in searcher.Get())
                {
                    double lowestFree = ReadVolumes().Count == 0 ? 100 : ReadVolumes().Min(delegate(VolumeEntry v) { return 100.0 - v.UsagePercent; });
                    bool warning = lowestFree < 15;
                    string model = Convert.ToString(disk["Model"]);
                    string bus = model.IndexOf("NVMe", StringComparison.OrdinalIgnoreCase) >= 0 ? "NVMe" : Convert.ToString(disk["InterfaceType"]);
                    items.Add(new ImportantHardware { Component = "ARMAZENAMENTO", Model = model, Specifications = FormatBytes(Convert.ToInt64(disk["Size"] ?? 0)) + " • " + bus + " • firmware " + disk["FirmwareRevision"], Status = warning ? "Saudável • pouco espaço livre" : "Saudável", Warning = warning });
                }
            }
            catch { }

            progress.Report("Lendo placa-mãe e BIOS...");
            token.ThrowIfCancellationRequested();
            try
            {
                string board = "";
                string boardSpecs = "";
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer,Product,Version FROM Win32_BaseBoard")) foreach (ManagementObject value in searcher.Get()) { board = Convert.ToString(value["Manufacturer"]) + " " + value["Product"]; boardSpecs = "Revisão " + value["Version"]; }
                using (var searcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion,ReleaseDate FROM Win32_BIOS")) foreach (ManagementObject bios in searcher.Get()) boardSpecs += " • BIOS " + bios["SMBIOSBIOSVersion"] + " • " + FormatWmiValue(bios["ReleaseDate"]).Split(' ')[0];
                items.Add(new ImportantHardware { Component = "PLACA-MÃE / BIOS", Model = board.Trim(), Specifications = boardSpecs, Status = "BIOS detectada" });
            }
            catch { }

            progress.Report("Lendo rede...");
            token.ThrowIfCancellationRequested();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name,Speed,NetConnectionID,NetEnabled FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True AND NetEnabled=True"))
                foreach (ManagementObject network in searcher.Get())
                {
                    string name = Convert.ToString(network["Name"]);
                    if (Regex.IsMatch(name, "Virtual|Wi-Fi Direct|Bluetooth", RegexOptions.IgnoreCase)) continue;
                    double rawSpeed = Convert.ToDouble(network["Speed"] ?? 0);
                    double speed = rawSpeed > 0 && rawSpeed < 100000000000.0 ? rawSpeed / 1000000000.0 : 0;
                    items.Add(new ImportantHardware { Component = "REDE", Model = name, Specifications = (speed > 0 ? speed.ToString("N1", CultureInfo.CurrentCulture) + " Gbps" : "Velocidade não informada") + " • " + network["NetConnectionID"], Status = "Conectada" });
                }
            }
            catch { }
            return items;
        }

        public static string ImportantHardwareReport(List<ImportantHardware> items, string recommendations)
        {
            var sb = new StringBuilder("HARDWARE PRINCIPAL\r\n" + new string('=', 72) + "\r\n" + recommendations + "\r\n\r\n");
            foreach (ImportantHardware item in items)
            {
                sb.AppendLine(item.Component + ": " + item.Model);
                sb.AppendLine("  " + item.Specifications);
                sb.AppendLine("  " + item.Status);
            }
            return sb.ToString();
        }

        public static List<VolumeEntry> ReadVolumes()
        {
            var volumes = new List<VolumeEntry>();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                    long total = drive.TotalSize;
                    long free = drive.AvailableFreeSpace;
                    volumes.Add(new VolumeEntry { Drive = drive.Name.TrimEnd('\\'), Label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Disco local" : drive.VolumeLabel, FileSystem = drive.DriveFormat, Health = "OK", TotalBytes = total, FreeBytes = free, UsedBytes = total - free, UsagePercent = total == 0 ? 0 : (total - free) * 100.0 / total });
                }
                catch { }
            }
            return volumes.OrderBy(delegate(VolumeEntry volume) { return volume.Drive; }).ToList();
        }

        public static List<StorageEntry> ScanVolume(string drive, CancellationToken token, IProgress<string> progress, Action<StorageEntry> onEntry)
        {
            string root = Path.GetFullPath(drive.EndsWith("\\") ? drive : drive + "\\");
            var rows = new List<StorageEntry>();
            string[] directories = new string[0];
            try { directories = Directory.GetDirectories(root); } catch { }
            foreach (string directory in directories)
            {
                token.ThrowIfCancellationRequested();
                try { if ((new DirectoryInfo(directory).Attributes & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                progress.Report("Medindo " + directory + "...");
                long logical;
                long allocated;
                MeasureDirectory(directory, token, out logical, out allocated);
                if (allocated < 1048576) continue;
                var entry = new StorageEntry { Kind = "Pasta", Path = directory, LogicalBytes = logical, AllocatedBytes = allocated };
                rows.Add(entry);
                if (onEntry != null) onEntry(entry);
            }
            return rows.OrderByDescending(delegate(StorageEntry row) { return row.AllocatedBytes; }).ToList();
        }

        public static List<CleanupTarget> GetCleanupTargets(CancellationToken token, IProgress<string> progress)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var targets = new List<CleanupTarget>
            {
                new CleanupTarget { Name = "Temporários do usuário", Path = Path.GetTempPath(), DefaultSelected = true },
                new CleanupTarget { Name = "Temporários do Windows", Path = @"C:\Windows\Temp", DefaultSelected = Optimizer.IsAdministrator() },
                new CleanupTarget { Name = "Cache do Chrome", Path = Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Cache"), DefaultSelected = true },
                new CleanupTarget { Name = "Cache de código do Chrome", Path = Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Code Cache"), DefaultSelected = true },
                new CleanupTarget { Name = "Cache do Edge", Path = Path.Combine(local, "Microsoft", "Edge", "User Data", "Default", "Cache"), DefaultSelected = true },
                new CleanupTarget { Name = "Shaders DirectX", Path = Path.Combine(local, "D3DSCache"), DefaultSelected = true },
                new CleanupTarget { Name = "Relatórios de falha", Path = Path.Combine(local, "CrashDumps"), DefaultSelected = true },
                new CleanupTarget { Name = "Miniaturas do Explorer", Path = Path.Combine(local, "Microsoft", "Windows", "Explorer"), Pattern = "thumbcache_*.db", DefaultSelected = true }
            };
            foreach (CleanupTarget target in targets)
            {
                token.ThrowIfCancellationRequested();
                progress.Report("Calculando " + target.Name + "...");
                target.SizeBytes = MeasureCleanupTarget(target, token);
            }
            return targets.Where(delegate(CleanupTarget target) { return target.SizeBytes > 0 || Directory.Exists(target.Path); }).ToList();
        }

        public static string CleanTargets(List<CleanupTarget> targets, CancellationToken token, IProgress<string> progress)
        {
            long freed = 0;
            int removed = 0;
            var sb = new StringBuilder("LIMPEZA SEGURA\r\n" + new string('=', 72) + "\r\n");
            foreach (CleanupTarget target in targets)
            {
                token.ThrowIfCancellationRequested();
                progress.Report("Limpando " + target.Name + "...");
                long before = freed;
                foreach (string file in EnumerateCleanupFiles(target, token))
                {
                    try { long size = new FileInfo(file).Length; File.Delete(file); if (!File.Exists(file)) { freed += size; removed++; } } catch { }
                }
                sb.AppendLine("✓ " + target.Name + ": " + FormatBytes(freed - before));
            }
            sb.AppendLine("\r\nLiberado: " + FormatBytes(freed) + " • arquivos removidos: " + removed);
            return sb.ToString();
        }

        private static long MeasureCleanupTarget(CleanupTarget target, CancellationToken token)
        {
            long total = 0;
            foreach (string file in EnumerateCleanupFiles(target, token)) try { total += new FileInfo(file).Length; } catch { }
            return total;
        }

        private static IEnumerable<string> EnumerateCleanupFiles(CleanupTarget target, CancellationToken token)
        {
            if (!Directory.Exists(target.Path)) yield break;
            string root = Path.GetFullPath(target.Path).TrimEnd(Path.DirectorySeparatorChar);
            string local = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).TrimEnd(Path.DirectorySeparatorChar);
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            string windowsTemp = Path.GetFullPath(@"C:\Windows\Temp").TrimEnd(Path.DirectorySeparatorChar);
            bool allowed = root.Equals(temp, StringComparison.OrdinalIgnoreCase) || root.Equals(windowsTemp, StringComparison.OrdinalIgnoreCase) || root.StartsWith(local + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (!allowed) yield break;
            if (!string.IsNullOrWhiteSpace(target.Pattern))
            {
                string[] files = new string[0];
                try { files = Directory.GetFiles(root, target.Pattern, SearchOption.TopDirectoryOnly); } catch { }
                foreach (string file in files) { token.ThrowIfCancellationRequested(); yield return file; }
                yield break;
            }
            foreach (string file in EnumerateFilesSafe(root, token)) yield return file;
        }

        public static string BuildPerformanceRecommendations()
        {
            var recommendations = new List<string>();
            try
            {
                int modules = 0;
                int slots = 0;
                double capacity = 0;
                int speed = 0;
                using (var searcher = new ManagementObjectSearcher("SELECT Capacity,Speed FROM Win32_PhysicalMemory"))
                foreach (ManagementObject memory in searcher.Get())
                {
                    modules++;
                    capacity += Convert.ToDouble(memory["Capacity"], CultureInfo.InvariantCulture) / 1073741824.0;
                    speed = Math.Max(speed, Convert.ToInt32(memory["Speed"] ?? 0));
                }
                using (var searcher = new ManagementObjectSearcher("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray"))
                foreach (ManagementObject array in searcher.Get()) slots = Math.Max(slots, Convert.ToInt32(array["MemoryDevices"] ?? 0));
                if (modules == 1 && slots >= 2) recommendations.Add("RAM +" + capacity.ToString("N0", CultureInfo.CurrentCulture) + " GB / " + speed + " MT/s");
            }
            catch { }
            SystemMetrics metrics = ReadMetrics();
            if (metrics.TotalRamGb > 0 && metrics.FreeRamGb / metrics.TotalRamGb < 0.2) recommendations.Add("RAM em uso alto");
            try
            {
                var drive = new DriveInfo("C");
                if (metrics.FreeDiskPercent < 15)
                {
                    double target = (drive.TotalSize * 0.15 - drive.AvailableFreeSpace) / 1073741824.0;
                    recommendations.Add("Liberar " + Math.Ceiling(Math.Max(0, target)).ToString("N0", CultureInfo.CurrentCulture) + " GB no SSD");
                }
            }
            catch { }
            if (ReadStartupEntries().Any(delegate(StartupEntry e) { return e.Enabled && e.Name.StartsWith("MicrosoftEdgeAutoLaunch_", StringComparison.OrdinalIgnoreCase); })) recommendations.Add("Tirar Edge da inicialização");
            return recommendations.Count == 0 ? "Nenhuma prioridade crítica" : "Prioridades: " + string.Join("  •  ", recommendations.ToArray());
        }

        private static string FormatWmiValue(object value)
        {
            if (value == null) return string.Empty;
            Array array = value as Array;
            if (array != null)
            {
                var parts = new List<string>();
                int count = 0;
                foreach (object item in array)
                {
                    if (count++ >= 128) { parts.Add("…"); break; }
                    parts.Add(Convert.ToString(item, CultureInfo.CurrentCulture));
                }
                return string.Join(", ", parts.ToArray());
            }
            string text = Convert.ToString(value, CultureInfo.CurrentCulture);
            if (Regex.IsMatch(text, @"^\d{14}\.\d{6}[+-]\d{3}$"))
            {
                try { return ManagementDateTimeConverter.ToDateTime(text).ToString("dd/MM/yyyy HH:mm:ss"); } catch { }
            }
            return text;
        }

        public static string Apply(ApplyOptions options, CancellationToken token, IProgress<string> progress)
        {
            EnsureSnapshot();
            SystemMetrics before = ReadMetrics();
            var log = new StringBuilder();
            log.AppendLine("APLICAÇÃO DE PERFIL 3.3");
            log.AppendLine(new string('=', 72));
            log.AppendLine("Perfil: " + ProfileName(options.Profile));
            if (options.CreateRestorePoint)
            {
                progress.Report("Criando ponto de restauração...");
                if (Optimizer.IsAdministrator())
                {
                    string result = Run("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description 'Antes do Otimizador 3.3' -RestorePointType MODIFY_SETTINGS\"", 120000);
                    log.AppendLine(result.IndexOf("erro", StringComparison.OrdinalIgnoreCase) >= 0 || result.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ? "! O Windows não criou o ponto de restauração: " + OneLine(result) : "✓ Ponto de restauração solicitado.");
                }
                else log.AppendLine("! Ponto de restauração ignorado: reabra como administrador.");
            }
            token.ThrowIfCancellationRequested();
            progress.Report("Configurando energia...");
            log.AppendLine(ApplyPowerProfile(options.Profile));
            token.ThrowIfCancellationRequested();
            if (options.DarkMode)
            {
                progress.Report("Aplicando modo escuro...");
                SetDword(PersonalizeKey, "AppsUseLightTheme", 0);
                SetDword(PersonalizeKey, "SystemUsesLightTheme", 0);
                SetDword(PersonalizeKey, "EnableTransparency", 0);
                if (Registry.CurrentUser.OpenSubKey(OfficeKey) != null) SetDword(OfficeKey, "UI Theme", 4);
                BroadcastTheme();
                log.AppendLine("✓ Modo escuro aplicado ao Windows e Office.");
            }
            if (options.ReduceVisuals)
            {
                SetDword(VisualKey, "VisualFXSetting", options.Profile == 1 ? 1 : 2);
                SetDword(ExplorerKey, "TaskbarAnimations", options.Profile == 1 ? 1 : 0);
                log.AppendLine(options.Profile == 1 ? "✓ Efeitos visuais mantidos no padrão equilibrado." : "✓ Efeitos visuais reduzidos.");
            }
            if (options.OptimizeStartup)
            {
                progress.Report("Otimizando inicialização...");
                DisableStartup("Teams", log);
                DisableStartup("Adobe Acrobat Synchronizer", log);
                DisableStartupPrefix("MicrosoftEdgeAutoLaunch_", log);
                log.AppendLine("✓ OneDrive e componentes corporativos preservados.");
            }
            if (options.BackgroundEfficiency)
            {
                progress.Report("Reduzindo atividade em segundo plano...");
                log.Append(ApplyBackgroundEfficiencyCore(token));
            }
            if (options.CleanupTemp)
            {
                progress.Report("Limpando temporários antigos...");
                long freed = DeleteOldFiles(Path.GetTempPath(), DateTime.Now.AddDays(-7), token);
                if (Optimizer.IsAdministrator()) freed += DeleteOldFiles(@"C:\Windows\Temp", DateTime.Now.AddDays(-7), token);
                log.AppendLine("✓ Temporários antigos removidos: " + FormatBytes(freed));
            }
            progress.Report("Otimizando unidade do sistema...");
            string volumeOptimization = WindowsMaintenance.OptimizeVolume("C:", token, progress);
            log.AppendLine(volumeOptimization.IndexOf("Resultado: concluído", StringComparison.OrdinalIgnoreCase) >= 0
                ? "✓ Otimização correta para o tipo de unidade concluída."
                : "! " + OneLine(volumeOptimization));
            SystemMetrics after = ReadMetrics();
            AdvancedEngine.SaveComparison(before, after, "Perfil " + ProfileName(options.Profile));
            log.AppendLine();
            log.AppendLine("ANTES E DEPOIS");
            log.AppendLine(string.Format(CultureInfo.CurrentCulture, "Memória livre: {0:N1} GB → {1:N1} GB ({2:+0.0;-0.0;0.0} GB)", before.FreeRamGb, after.FreeRamGb, after.FreeRamGb - before.FreeRamGb));
            log.AppendLine(string.Format(CultureInfo.CurrentCulture, "Disco livre: {0:N1} GB → {1:N1} GB ({2:+0.0;-0.0;0.0} GB)", before.FreeDiskGb, after.FreeDiskGb, after.FreeDiskGb - before.FreeDiskGb));
            log.AppendLine("Energia: " + after.PowerScheme);
            log.AppendLine();
            log.AppendLine("Reinicie ou saia da conta para aplicar integralmente inicialização, Office e efeitos visuais.");
            return log.ToString();
        }

        public static string ApplyBackgroundEfficiency(CancellationToken token, IProgress<string> progress)
        {
            EnsureSnapshot();
            progress.Report("Reduzindo atividade em segundo plano...");
            var log = new StringBuilder("EFICIÊNCIA EM SEGUNDO PLANO\r\n" + new string('=', 72) + "\r\n");
            log.Append(ApplyBackgroundEfficiencyCore(token));
            log.AppendLine("\r\nAs alterações entram por completo após sair da conta ou reiniciar.");
            return log.ToString();
        }

        private static string ApplyBackgroundEfficiencyCore(CancellationToken token)
        {
            var log = new StringBuilder();
            bool widgetButton = TrySetDword(ExplorerKey, "TaskbarDa", 0);
            bool gameCapture = TrySetDword(GameConfigKey, "GameDVR_Enabled", 0) | TrySetDword(GameDvrKey, "AppCaptureEnabled", 0);
            bool edgeBackground = TrySetDword(EdgePolicyKey, "BackgroundModeEnabled", 0);
            bool edgeBoost = TrySetDword(EdgePolicyKey, "StartupBoostEnabled", 0);
            bool suggestions = TrySetDword(ContentDeliveryKey, "SoftLandingEnabled", 0);
            suggestions = TrySetDword(ContentDeliveryKey, "SystemPaneSuggestionsEnabled", 0) | suggestions;
            suggestions = TrySetDword(ContentDeliveryKey, "SubscribedContent-338389Enabled", 0) | suggestions;
            suggestions = TrySetDword(ContentDeliveryKey, "SubscribedContent-353694Enabled", 0) | suggestions;
            suggestions = TrySetDword(ContentDeliveryKey, "SubscribedContent-353696Enabled", 0) | suggestions;
            suggestions = TrySetDword(EngagementKey, "ScoobeSystemSettingEnabled", 0) | suggestions;
            int limited = 0;
            foreach (string package in NonEssentialBackgroundPackages)
            {
                token.ThrowIfCancellationRequested();
                string key = BackgroundAppsKey + "\\" + package;
                bool disabled = TrySetDword(key, "Disabled", 1);
                bool disabledByUser = TrySetDword(key, "DisabledByUser", 1);
                if (disabled || disabledByUser) limited++;
            }
            log.AppendLine("✓ Widgets e " + limited + " aplicativos de consumo limitados em segundo plano.");
            if (widgetButton) log.AppendLine("✓ Botão de Widgets ocultado.");
            if (gameCapture) log.AppendLine("✓ Captura automática de jogos desativada.");
            if (edgeBackground && edgeBoost) log.AppendLine("✓ Edge impedido de permanecer aberto ou pré-carregar.");
            else log.AppendLine("! Controle do Edge protegido por política corporativa; os demais ajustes foram aplicados.");
            if (suggestions) log.AppendLine("✓ Sugestões e experiências promocionais reduzidas.");
            log.AppendLine("✓ Teams, Outlook, OneDrive, Defender, Intune e Veeam preservados.");
            return log.ToString();
        }

        public static string Restore(CancellationToken token, IProgress<string> progress)
        {
            if (!File.Exists(SnapshotPath)) return "Nenhum backup base foi encontrado.";
            progress.Report("Lendo backup base...");
            BackupSnapshot snapshot = ReadSnapshot();
            var log = new StringBuilder();
            log.AppendLine("RESTAURAÇÃO DO ESTADO BASE");
            log.AppendLine(new string('=', 72));
            if (!string.IsNullOrEmpty(snapshot.PowerScheme)) Run("powercfg.exe", "/setactive " + snapshot.PowerScheme, 15000);
            foreach (KeyValuePair<string, string> pair in snapshot.RegistryValues)
            {
                token.ThrowIfCancellationRequested();
                string[] parts = pair.Key.Split(new[] { '|' }, 2);
                RestoreRegistryValue(parts[0], parts[1], pair.Value);
            }
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (run != null)
                {
                    foreach (KeyValuePair<string, string> pair in snapshot.StartupValues) run.SetValue(pair.Key, pair.Value, RegistryValueKind.String);
                }
            }
            using (RegistryKey disabled = Registry.CurrentUser.OpenSubKey(DisabledKey, true))
            {
                if (disabled != null) foreach (string name in disabled.GetValueNames()) if (snapshot.StartupValues.ContainsKey(name)) disabled.DeleteValue(name, false);
            }
            BroadcastTheme();
            log.AppendLine("✓ Plano de energia restaurado: " + snapshot.PowerScheme);
            log.AppendLine("✓ Tema, transparência, efeitos, segundo plano e Office restaurados.");
            log.AppendLine("✓ Itens de inicialização registrados no backup foram restaurados.");
            log.AppendLine("Backup criado em: " + snapshot.CreatedUtc);
            log.AppendLine("Arquivos excluídos por limpezas não são recriados.");
            return log.ToString();
        }

        public static string RestoreSection(string section, CancellationToken token, IProgress<string> progress)
        {
            if (!File.Exists(SnapshotPath)) return "Nenhum backup base foi encontrado.";
            BackupSnapshot snapshot = ReadSnapshot();
            var log = new StringBuilder("RESTAURAÇÃO SELETIVA\r\n" + new string('=', 72) + "\r\n");
            progress.Report("Restaurando " + section.ToLowerInvariant() + "...");
            if (section.Equals("Energia", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(snapshot.PowerScheme)) Run("powercfg.exe", "/setactive " + snapshot.PowerScheme, 15000);
                log.AppendLine("✓ Plano de energia original restaurado.");
            }
            else if (section.Equals("Tema", StringComparison.OrdinalIgnoreCase))
            {
                RestoreSnapshotValue(snapshot, PersonalizeKey, "AppsUseLightTheme");
                RestoreSnapshotValue(snapshot, PersonalizeKey, "SystemUsesLightTheme");
                RestoreSnapshotValue(snapshot, PersonalizeKey, "EnableTransparency");
                RestoreSnapshotValue(snapshot, OfficeKey, "UI Theme");
                BroadcastTheme();
                log.AppendLine("✓ Tema, transparência e tema do Office restaurados.");
            }
            else if (section.Equals("Efeitos visuais", StringComparison.OrdinalIgnoreCase))
            {
                RestoreSnapshotValue(snapshot, VisualKey, "VisualFXSetting");
                RestoreSnapshotValue(snapshot, ExplorerKey, "TaskbarAnimations");
                log.AppendLine("✓ Efeitos visuais restaurados.");
            }
            else if (section.Equals("Segundo plano", StringComparison.OrdinalIgnoreCase))
            {
                foreach (KeyValuePair<string, string> value in EfficiencyRegistryValues())
                {
                    token.ThrowIfCancellationRequested();
                    RestoreSnapshotValue(snapshot, value.Key, value.Value);
                }
                log.AppendLine("✓ Limites de segundo plano, Widgets, capturas e sugestões restaurados.");
            }
            else if (section.Equals("Inicialização", StringComparison.OrdinalIgnoreCase))
            {
                using (RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKey))
                    foreach (KeyValuePair<string, string> pair in snapshot.StartupValues) run.SetValue(pair.Key, pair.Value, RegistryValueKind.String);
                using (RegistryKey disabled = Registry.CurrentUser.OpenSubKey(DisabledKey, true))
                    if (disabled != null) foreach (string name in disabled.GetValueNames()) if (snapshot.StartupValues.ContainsKey(name)) disabled.DeleteValue(name, false);
                log.AppendLine("✓ Itens de inicialização do backup restaurados.");
            }
            else return "Categoria de restauração desconhecida.";
            log.AppendLine("Arquivos excluídos por limpezas não são recriados.");
            return log.ToString();
        }

        private static void RestoreSnapshotValue(BackupSnapshot snapshot, string key, string name)
        {
            string value;
            if (snapshot.RegistryValues != null && snapshot.RegistryValues.TryGetValue(key + "|" + name, out value)) RestoreRegistryValue(key, name, value);
        }

        public static void EnsureSnapshot()
        {
            Directory.CreateDirectory(AppFolder);
            if (File.Exists(SnapshotPath))
            {
                BackupSnapshot existing = ReadSnapshot();
                if (existing.RegistryValues == null) existing.RegistryValues = new Dictionary<string, string>();
                if (CaptureEfficiencySettings(existing, true))
                    File.WriteAllText(SnapshotPath, new JavaScriptSerializer().Serialize(existing), Encoding.UTF8);
                return;
            }
            var snapshot = new BackupSnapshot
            {
                CreatedUtc = DateTime.UtcNow.ToString("o"),
                Computer = Environment.MachineName,
                User = Environment.UserName,
                PowerScheme = ExtractGuid(Run("powercfg.exe", "/getactivescheme", 15000)),
                RegistryValues = new Dictionary<string, string>(),
                StartupValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
            string legacy = @"Software\Codex\PerformanceBackup\Run";
            bool legacyExists = Registry.CurrentUser.OpenSubKey(legacy) != null;
            if (legacyExists && IsMaximumScheme()) snapshot.PowerScheme = "381b4222-f694-41f0-9685-ff5bb260df2e";
            Capture(snapshot, PersonalizeKey, "AppsUseLightTheme");
            Capture(snapshot, PersonalizeKey, "SystemUsesLightTheme");
            Capture(snapshot, PersonalizeKey, "EnableTransparency");
            Capture(snapshot, VisualKey, "VisualFXSetting");
            Capture(snapshot, ExplorerKey, "TaskbarAnimations");
            Capture(snapshot, OfficeKey, "UI Theme");
            CaptureEfficiencySettings(snapshot, false);
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKey))
            if (run != null) foreach (string name in run.GetValueNames()) snapshot.StartupValues[name] = Convert.ToString(run.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames));
            using (RegistryKey old = Registry.CurrentUser.OpenSubKey(legacy))
            if (old != null) foreach (string name in old.GetValueNames()) if (!snapshot.StartupValues.ContainsKey(name)) snapshot.StartupValues[name] = Convert.ToString(old.GetValue(name));
            File.WriteAllText(SnapshotPath, new JavaScriptSerializer().Serialize(snapshot), Encoding.UTF8);
        }

        public static List<StartupEntry> ReadStartupEntries()
        {
            var rows = new Dictionary<string, StartupEntry>(StringComparer.OrdinalIgnoreCase);
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKey))
            if (run != null) foreach (string name in run.GetValueNames()) rows[name] = new StartupEntry { Enabled = true, Name = name, Command = Convert.ToString(run.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)), Impact = EstimateImpact(name) };
            using (RegistryKey disabled = Registry.CurrentUser.OpenSubKey(DisabledKey))
            if (disabled != null) foreach (string name in disabled.GetValueNames()) if (!rows.ContainsKey(name)) rows[name] = new StartupEntry { Enabled = false, Name = name, Command = Convert.ToString(disabled.GetValue(name)), Impact = EstimateImpact(name) };
            using (RegistryKey legacy = Registry.CurrentUser.OpenSubKey(@"Software\Codex\PerformanceBackup\Run"))
            if (legacy != null) foreach (string name in legacy.GetValueNames()) if (!rows.ContainsKey(name)) rows[name] = new StartupEntry { Enabled = false, Name = name, Command = Convert.ToString(legacy.GetValue(name)), Impact = EstimateImpact(name) };
            return rows.Values.OrderByDescending(delegate(StartupEntry e) { return e.Enabled; }).ThenBy(delegate(StartupEntry e) { return e.Name; }).ToList();
        }

        public static string ApplyStartupEntries(List<StartupEntry> entries, CancellationToken token, IProgress<string> progress)
        {
            EnsureSnapshot();
            var log = new StringBuilder("INICIALIZAÇÃO\r\n" + new string('=', 72) + "\r\n");
            using (RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKey))
            using (RegistryKey disabled = Registry.CurrentUser.CreateSubKey(DisabledKey))
            {
                foreach (StartupEntry entry in entries)
                {
                    token.ThrowIfCancellationRequested();
                    progress.Report("Atualizando " + entry.Name + "...");
                    object current = run == null ? null : run.GetValue(entry.Name);
                    if (entry.Enabled && current == null)
                    {
                        run.SetValue(entry.Name, entry.Command, RegistryValueKind.String);
                        disabled.DeleteValue(entry.Name, false);
                        log.AppendLine("✓ Ativado: " + entry.Name);
                    }
                    else if (!entry.Enabled && current != null)
                    {
                        disabled.SetValue(entry.Name, Convert.ToString(current), RegistryValueKind.String);
                        run.DeleteValue(entry.Name, false);
                        log.AppendLine("✓ Desativado: " + entry.Name);
                    }
                }
            }
            return log.ToString();
        }

        public static List<DuplicateEntry> FindDuplicates(string root, CancellationToken token, IProgress<string> progress)
        {
            var bySize = new Dictionary<long, List<string>>();
            foreach (string file in EnumerateFilesSafe(root, token))
            {
                try
                {
                    long size = new FileInfo(file).Length;
                    if (size < 1048576) continue;
                    List<string> list;
                    if (!bySize.TryGetValue(size, out list)) { list = new List<string>(); bySize[size] = list; }
                    list.Add(file);
                }
                catch { }
            }
            var result = new List<DuplicateEntry>();
            int group = 0;
            using (SHA256 sha = SHA256.Create())
            foreach (KeyValuePair<long, List<string>> sizeGroup in bySize.Where(delegate(KeyValuePair<long, List<string>> p) { return p.Value.Count > 1; }))
            {
                var hashes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (string file in sizeGroup.Value)
                {
                    token.ThrowIfCancellationRequested();
                    progress.Report("Comparando " + Path.GetFileName(file) + "...");
                    try
                    {
                        string hash;
                        using (FileStream stream = File.OpenRead(file)) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                        List<string> files;
                        if (!hashes.TryGetValue(hash, out files)) { files = new List<string>(); hashes[hash] = files; }
                        files.Add(file);
                    }
                    catch { }
                }
                foreach (KeyValuePair<string, List<string>> hashGroup in hashes.Where(delegate(KeyValuePair<string, List<string>> p) { return p.Value.Count > 1; }))
                {
                    group++;
                    foreach (string file in hashGroup.Value) result.Add(new DuplicateEntry { Group = group, Path = file, Size = sizeGroup.Key, Hash = hashGroup.Key });
                }
            }
            return result;
        }

        public static string StorageReport(List<StorageEntry> rows)
        {
            var sb = new StringBuilder("ANÁLISE DE ARMAZENAMENTO\r\n" + new string('=', 72) + "\r\n");
            foreach (StorageEntry row in rows) sb.AppendLine(string.Format("{0,-10} {1,10} no disco | {2}", row.Kind, FormatBytes(row.AllocatedBytes), row.Path));
            sb.AppendLine("\r\nArquivos sob demanda podem ter tamanho lógico alto e ocupar pouco espaço físico.");
            return sb.ToString();
        }

        public static string DuplicateReport(string root, List<DuplicateEntry> rows)
        {
            var sb = new StringBuilder("ARQUIVOS DUPLICADOS VERIFICADOS POR SHA-256\r\n" + new string('=', 72) + "\r\nPasta: " + root + "\r\n");
            foreach (DuplicateEntry row in rows) sb.AppendLine(string.Format("Grupo {0} | {1} | {2}", row.Group, FormatBytes(row.Size), row.Path));
            if (rows.Count == 0) sb.AppendLine("Nenhum duplicado maior que 1 MB foi encontrado.");
            sb.AppendLine("\r\nO programa não exclui duplicados automaticamente.");
            return sb.ToString();
        }

        public static string ConfigureSchedule(int index)
        {
            Directory.CreateDirectory(AppFolder);
            const string task = "Codex Otimizador - Manutencao";
            if (index == 0)
            {
                Run("schtasks.exe", "/Delete /TN \"" + task + "\" /F", 30000);
                SetDword(AppKey, "Schedule", 0);
                return "Manutenção agendada desativada.";
            }
            string schedule = index == 1 ? "/SC WEEKLY /D MON /ST 12:00" : "/SC MONTHLY /D 1 /ST 12:00";
            string command = "\"" + Application.ExecutablePath + "\" --maintenance";
            CommandExecution taskResult = Execute("schtasks.exe", "/Create /TN \"" + task + "\" /TR \"" + command.Replace("\"", "\\\"") + "\" " + schedule + " /F /RL LIMITED", 30000);
            if (taskResult.ExitCode != 0)
                return "O Windows não criou a tarefa. Reabra como administrador e tente novamente.\r\n" + taskResult.Output;
            SetDword(AppKey, "Schedule", index);
            return "Manutenção " + (index == 1 ? "semanal" : "mensal") + " agendada para 12:00.\r\n" + taskResult.Output;
        }

        public static int ReadScheduleIndex()
        {
            string value = ReadRegistryValue(AppKey, "Schedule");
            int parsed;
            return int.TryParse(value, out parsed) && parsed >= 0 && parsed <= 2 ? parsed : 0;
        }

        public static void RunMaintenance()
        {
            SaveReport(MaintenanceReport(CancellationToken.None, new Progress<string>()));
        }

        public static string MaintenanceReport(CancellationToken token, IProgress<string> progress)
        {
            progress.Report("Limpando temporários antigos...");
            long freed = DeleteOldFiles(Path.GetTempPath(), DateTime.Now.AddDays(-14), token);
            if (Optimizer.IsAdministrator()) freed += DeleteOldFiles(@"C:\Windows\Temp", DateTime.Now.AddDays(-14), token);
            string optimization = WindowsMaintenance.OptimizeVolume("C:", token, progress);
            return "MANUTENÇÃO SEGURA\r\n" + new string('=', 72) + "\r\nData: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + "\r\nTemporários removidos: " + FormatBytes(freed) + "\r\n\r\n" + optimization;
        }

        public static string SaveReport(string content)
        {
            try
            {
                Directory.CreateDirectory(ReportsFolder);
                string path = Path.Combine(ReportsFolder, "relatorio-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt");
                File.WriteAllText(path, content, Encoding.UTF8);
                return path;
            }
            catch { return string.Empty; }
        }

        public static List<ReportSummary> ReadReportHistory(int maximumItems)
        {
            var reports = new List<ReportSummary>();
            if (!Directory.Exists(ReportsFolder)) return reports;
            IEnumerable<FileInfo> files;
            try
            {
                files = new DirectoryInfo(ReportsFolder).GetFiles("relatorio-*.txt")
                    .OrderByDescending(delegate(FileInfo file) { return file.LastWriteTime; })
                    .Take(Math.Max(1, maximumItems))
                    .ToArray();
            }
            catch { return reports; }

            foreach (FileInfo file in files)
            {
                try
                {
                    string content = ReadReportPreview(file.FullName, 32768);
                    string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    reports.Add(new ReportSummary
                    {
                        Path = file.FullName,
                        Created = file.LastWriteTime,
                        Category = ReportCategory(lines),
                        Summary = ReportDescription(lines)
                    });
                }
                catch { }
            }
            return reports;
        }

        private static string ReadReportPreview(string path, int maximumCharacters)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                var buffer = new char[maximumCharacters];
                int read = reader.ReadBlock(buffer, 0, buffer.Length);
                return new string(buffer, 0, read);
            }
        }

        private static string ReportCategory(string[] lines)
        {
            string header = lines.FirstOrDefault(delegate(string line) { return !string.IsNullOrWhiteSpace(line); }) ?? "Atividade do sistema";
            string upper = header.ToUpperInvariant();
            if (upper.Contains("AUDITORIA")) return "Análise do sistema";
            if (upper.Contains("APLICAÇÃO DE PERFIL")) return "Perfil aplicado";
            if (upper.Contains("EFICIÊNCIA EM SEGUNDO PLANO")) return "Eficiência em segundo plano";
            if (upper.Contains("HARDWARE")) return "Inventário de hardware";
            if (upper.Contains("ARMAZENAMENTO")) return "Análise de armazenamento";
            if (upper.Contains("DUPLICADOS")) return "Arquivos duplicados";
            if (upper.Contains("MANUTENÇÃO")) return "Manutenção";
            if (upper.Contains("OTIMIZAÇÃO INTELIGENTE")) return "Otimização da unidade";
            if (upper.Contains("COMPONENTES DO WINDOWS")) return "Componentes do Windows";
            if (upper.Contains("DIAGNÓSTICO DE ENERGIA")) return "Diagnóstico de energia";
            if (upper.Contains("LIMPEZA")) return "Limpeza";
            if (upper.Contains("INICIALIZAÇÃO")) return "Inicialização";
            if (upper.Contains("RESTAURAÇÃO")) return "Restauração";
            return ShortReportText(header);
        }

        private static string ReportDescription(string[] lines)
        {
            string header = lines.FirstOrDefault(delegate(string line) { return !string.IsNullOrWhiteSpace(line); }) ?? string.Empty;
            string upper = header.ToUpperInvariant();
            var preferredPrefixes = new List<string>();
            if (upper.Contains("AUDITORIA")) preferredPrefixes.AddRange(new[] { "Processador:", "Memória:", "Disco C:" });
            else if (upper.Contains("APLICAÇÃO DE PERFIL")) preferredPrefixes.AddRange(new[] { "Perfil:", "Disco livre:" });
            else if (upper.Contains("HARDWARE")) preferredPrefixes.AddRange(new[] { "PROCESSADOR:", "MEMÓRIA:" });
            else if (upper.Contains("MANUTENÇÃO")) preferredPrefixes.AddRange(new[] { "Temporários removidos:", "TRIM:" });
            else if (upper.Contains("OTIMIZAÇÃO INTELIGENTE")) preferredPrefixes.AddRange(new[] { "Unidade:", "Resultado:" });
            else if (upper.Contains("COMPONENTES DO WINDOWS")) preferredPrefixes.AddRange(new[] { "Resultado:", "Variação de espaço livre:" });
            else if (upper.Contains("DIAGNÓSTICO DE ENERGIA")) preferredPrefixes.AddRange(new[] { "Resultado:", "Arquivo:" });
            else if (upper.Contains("LIMPEZA")) preferredPrefixes.AddRange(new[] { "Liberado:", "✓" });
            else if (upper.Contains("EFICIÊNCIA")) preferredPrefixes.AddRange(new[] { "✓ Widgets", "! Controle" });
            else preferredPrefixes.Add("✓");

            var matches = new List<string>();
            foreach (string prefix in preferredPrefixes)
            {
                string match = lines.FirstOrDefault(delegate(string line) { return line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase); });
                if (!string.IsNullOrWhiteSpace(match) && !matches.Contains(match.Trim())) matches.Add(match.Trim());
                if (matches.Count == 2) break;
            }
            if (matches.Count == 0)
            {
                foreach (string line in lines)
                {
                    string value = line.Trim();
                    if (string.IsNullOrWhiteSpace(value) || value == header.Trim() || value.All(delegate(char c) { return c == '=' || c == '-'; })) continue;
                    if (value.StartsWith("Data:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("Administrador:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("Backup", StringComparison.OrdinalIgnoreCase)) continue;
                    matches.Add(value);
                    break;
                }
            }
            return ShortReportText(matches.Count == 0 ? "Atividade concluída" : string.Join("  •  ", matches.ToArray()));
        }

        private static string ShortReportText(string text)
        {
            string value = Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
            return value.Length <= 180 ? value : value.Substring(0, 177) + "...";
        }

        public static void OpenReportsFolder()
        {
            Directory.CreateDirectory(ReportsFolder);
            Process.Start("explorer.exe", ReportsFolder);
        }

        public static string DetectManagedEnvironmentShort()
        {
            string value = DetectManagedEnvironmentLong();
            return value.IndexOf("gerenciado", StringComparison.OrdinalIgnoreCase) >= 0 ? "Gerenciado / corporativo" : "Pessoal ou não detectado";
        }

        public static string DetectManagedEnvironmentLong()
        {
            var markers = new List<string>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT PartOfDomain,Domain FROM Win32_ComputerSystem"))
                foreach (ManagementObject cs in searcher.Get()) if (Convert.ToBoolean(cs["PartOfDomain"])) markers.Add("domínio " + cs["Domain"]);
            }
            catch { }
            string[] services = { "IntuneManagementExtension", "Sense", "VeeamBackupSvc" };
            foreach (string service in services) try { using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + service)) if (key != null) markers.Add(service); } catch { }
            try { using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Enrollments")) if (key != null && key.GetSubKeyNames().Length > 0) markers.Add("MDM"); } catch { }
            return markers.Count == 0 ? "nenhum gerenciamento corporativo detectado" : "gerenciado: " + string.Join(", ", markers.Distinct().ToArray());
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
            return value.ToString(unit >= 3 ? "N2" : "N1", CultureInfo.CurrentCulture) + " " + units[unit];
        }

        private static string ApplyPowerProfile(int profile)
        {
            if (profile == 1)
            {
                Run("powercfg.exe", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e", 15000);
                return "✓ Plano Equilibrado ativado.";
            }
            if (profile == 2)
            {
                string guid = GetOrCreateScheme("Notebook eficiente", "381b4222-f694-41f0-9685-ff5bb260df2e");
                Run("powercfg.exe", "/setacvalueindex " + guid + " SUB_PROCESSOR PROCTHROTTLEMIN 5", 15000);
                Run("powercfg.exe", "/setacvalueindex " + guid + " SUB_PROCESSOR PROCTHROTTLEMAX 100", 15000);
                Run("powercfg.exe", "/setdcvalueindex " + guid + " SUB_PROCESSOR PROCTHROTTLEMIN 5", 15000);
                Run("powercfg.exe", "/setdcvalueindex " + guid + " SUB_PROCESSOR PROCTHROTTLEMAX 80", 15000);
                Run("powercfg.exe", "/setdcvalueindex " + guid + " SUB_PROCESSOR PERFEPP 60", 15000);
                Run("powercfg.exe", "/setactive " + guid, 15000);
                return "✓ Perfil Notebook ativado: máximo na tomada e limite de 80% na bateria.";
            }
            string maximum = GetOrCreateScheme("Desempenho Máximo", "e9a42b02-d5df-448d-aa00-03f14749eb61");
            Run("powercfg.exe", "/setacvalueindex " + maximum + " SUB_PROCESSOR PROCTHROTTLEMIN 100", 15000);
            Run("powercfg.exe", "/setacvalueindex " + maximum + " SUB_PROCESSOR PROCTHROTTLEMAX 100", 15000);
            Run("powercfg.exe", "/setacvalueindex " + maximum + " SUB_PROCESSOR SYSCOOLPOL 1", 15000);
            Run("powercfg.exe", "/setacvalueindex " + maximum + " SUB_PROCESSOR PERFEPP 0", 15000);
            Run("powercfg.exe", "/setacvalueindex " + maximum + " SUB_PCIEXPRESS ASPM 0", 15000);
            Run("powercfg.exe", "/setactive " + maximum, 15000);
            return "✓ Perfil de desempenho máximo ativado.";
        }

        private static string GetOrCreateScheme(string name, string baseGuid)
        {
            string list = Run("powercfg.exe", "/list", 15000);
            foreach (string line in list.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) if (line.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) { string found = ExtractGuid(line); if (!string.IsNullOrEmpty(found)) return found; }
            string guid = ExtractGuid(Run("powercfg.exe", "/duplicatescheme " + baseGuid, 15000));
            if (!string.IsNullOrEmpty(guid)) Run("powercfg.exe", "/changename " + guid + " \"" + name + "\"", 15000);
            return string.IsNullOrEmpty(guid) ? baseGuid : guid;
        }

        private static void DisableStartup(string name, StringBuilder log)
        {
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKey, true))
            using (RegistryKey disabled = Registry.CurrentUser.CreateSubKey(DisabledKey))
            {
                object value = run == null ? null : run.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (value == null) return;
                disabled.SetValue(name, Convert.ToString(value), RegistryValueKind.String);
                run.DeleteValue(name, false);
                log.AppendLine("✓ Inicialização desativada: " + name);
            }
        }

        private static void DisableStartupPrefix(string prefix, StringBuilder log)
        {
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKey, true))
            using (RegistryKey disabled = Registry.CurrentUser.CreateSubKey(DisabledKey))
            {
                if (run == null) return;
                foreach (string name in run.GetValueNames().Where(delegate(string valueName) { return valueName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase); }).ToArray())
                {
                    object value = run.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (value == null) continue;
                    disabled.SetValue(name, Convert.ToString(value), RegistryValueKind.String);
                    run.DeleteValue(name, false);
                    log.AppendLine("✓ Inicialização desativada: " + name);
                }
            }
        }

        private static string ProfileName(int profile)
        {
            return profile == 1 ? "Equilibrado" : profile == 2 ? "Notebook / eficiência" : "Máximo desempenho";
        }

        private static void Capture(BackupSnapshot snapshot, string key, string name)
        {
            snapshot.RegistryValues[key + "|" + name] = ReadRegistryValue(key, name);
        }

        private static bool CaptureEfficiencySettings(BackupSnapshot snapshot, bool onlyMissing)
        {
            bool changed = false;
            foreach (KeyValuePair<string, string> value in EfficiencyRegistryValues())
            {
                string composite = value.Key + "|" + value.Value;
                if (onlyMissing && snapshot.RegistryValues.ContainsKey(composite)) continue;
                Capture(snapshot, value.Key, value.Value);
                changed = true;
            }
            return changed;
        }

        private static IEnumerable<KeyValuePair<string, string>> EfficiencyRegistryValues()
        {
            yield return new KeyValuePair<string, string>(ExplorerKey, "TaskbarDa");
            yield return new KeyValuePair<string, string>(GameConfigKey, "GameDVR_Enabled");
            yield return new KeyValuePair<string, string>(GameDvrKey, "AppCaptureEnabled");
            yield return new KeyValuePair<string, string>(EdgePolicyKey, "BackgroundModeEnabled");
            yield return new KeyValuePair<string, string>(EdgePolicyKey, "StartupBoostEnabled");
            yield return new KeyValuePair<string, string>(ContentDeliveryKey, "SoftLandingEnabled");
            yield return new KeyValuePair<string, string>(ContentDeliveryKey, "SystemPaneSuggestionsEnabled");
            yield return new KeyValuePair<string, string>(ContentDeliveryKey, "SubscribedContent-338389Enabled");
            yield return new KeyValuePair<string, string>(ContentDeliveryKey, "SubscribedContent-353694Enabled");
            yield return new KeyValuePair<string, string>(ContentDeliveryKey, "SubscribedContent-353696Enabled");
            yield return new KeyValuePair<string, string>(EngagementKey, "ScoobeSystemSettingEnabled");
            foreach (string package in NonEssentialBackgroundPackages)
            {
                string key = BackgroundAppsKey + "\\" + package;
                yield return new KeyValuePair<string, string>(key, "Disabled");
                yield return new KeyValuePair<string, string>(key, "DisabledByUser");
            }
        }

        private static BackupSnapshot ReadSnapshot()
        {
            return new JavaScriptSerializer().Deserialize<BackupSnapshot>(File.ReadAllText(SnapshotPath, Encoding.UTF8));
        }

        private static string ReadRegistryValue(string keyPath, string name)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath))
            {
                object value = key == null ? null : key.GetValue(name);
                return value == null ? "<missing>" : Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static void RestoreRegistryValue(string keyPath, string name, string value)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                if (value == "<missing>") key.DeleteValue(name, false);
                else { int parsed; if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) key.SetValue(name, parsed, RegistryValueKind.DWord); else key.SetValue(name, value, RegistryValueKind.String); }
            }
        }

        private static void SetDword(string keyPath, string name, int value)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath)) key.SetValue(name, value, RegistryValueKind.DWord);
        }

        private static bool TrySetDword(string keyPath, string name, int value)
        {
            try { SetDword(keyPath, name, value); return true; }
            catch { return false; }
        }

        private static string EstimateImpact(string name)
        {
            if (Regex.IsMatch(name, "Teams|Adobe|Edge|WhatsApp", RegexOptions.IgnoreCase)) return "alto";
            if (Regex.IsMatch(name, "OneDrive|Security|Audio|Waves|Realtek", RegexOptions.IgnoreCase)) return "funcional";
            return "médio";
        }

        private static long DeleteOldFiles(string root, DateTime cutoff, CancellationToken token)
        {
            long freed = 0;
            foreach (string file in EnumerateFilesSafe(root, token))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime < cutoff) { long length = info.Length; info.Delete(); freed += length; }
                }
                catch { }
            }
            return freed;
        }

        private static IEnumerable<string> EnumerateFilesSafe(string root, CancellationToken token)
        {
            var pending = new Stack<string>();
            if (Directory.Exists(root)) pending.Push(root);
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string current = pending.Pop();
                string[] files = new string[0];
                string[] dirs = new string[0];
                try { files = Directory.GetFiles(current); } catch { }
                foreach (string file in files) yield return file;
                try { dirs = Directory.GetDirectories(current); } catch { }
                foreach (string dir in dirs)
                {
                    try { if ((new DirectoryInfo(dir).Attributes & FileAttributes.ReparsePoint) == 0) pending.Push(dir); } catch { }
                }
            }
        }

        private static void MeasureDirectory(string root, CancellationToken token, out long logical, out long allocated)
        {
            logical = 0;
            allocated = 0;
            foreach (string file in EnumerateFilesSafe(root, token))
            {
                try
                {
                    logical += new FileInfo(file).Length;
                    uint high;
                    uint low = GetCompressedFileSizeW(file, out high);
                    int error = Marshal.GetLastWin32Error();
                    if (low != uint.MaxValue || error == 0) allocated += (long)(((ulong)high << 32) + low);
                }
                catch { }
            }
        }

        private static CommandExecution Execute(string file, string args, int timeout)
        {
            return SystemCommand.Execute(file, args, timeout);
        }

        private static string Run(string file, string args, int timeout)
        {
            return Execute(file, args, timeout).Output;
        }

        private static string ExtractGuid(string text)
        {
            Match m = Regex.Match(text ?? string.Empty, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return m.Success ? m.Value : string.Empty;
        }

        private static bool IsMaximumScheme()
        {
            string active = Run("powercfg.exe", "/getactivescheme", 15000);
            return active.IndexOf("Desempenho Máximo", StringComparison.OrdinalIgnoreCase) >= 0 || active.IndexOf("Ultimate Performance", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string OneLine(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "não disponível" : Regex.Replace(text.Trim(), @"\s+", " ");
        }

        private static void BroadcastTheme()
        {
            UIntPtr result;
            SendMessageTimeout(new IntPtr(0xffff), 0x001A, UIntPtr.Zero, "ImmersiveColorSet", 2, 5000, out result);
        }

    }
}
