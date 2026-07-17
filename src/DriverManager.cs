using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexPerformanceOptimizer
{
    internal static class DriverManager
    {
        private const int SearchTimeout = 10 * 60 * 1000;
        private const int InstallTimeout = 60 * 60 * 1000;
        private static readonly string DriverBackupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Codex", "PerformanceOptimizer", "DriverBackups");
        private static readonly string[] OfficialSupportDomains =
        {
            "intel.com", "nvidia.com", "amd.com", "dell.com", "hp.com", "lenovo.com", "asus.com", "acer.com",
            "msi.com", "gigabyte.com", "realtek.com", "samsung.com", "qualcomm.com", "broadcom.com", "catalog.update.microsoft.com"
        };

        public static List<DriverUpdate> SearchUpdates(CancellationToken token)
        {
            return SearchUpdates(token, new Progress<string>());
        }

        public static List<DriverUpdate> SearchUpdates(CancellationToken token, IProgress<string> progress)
        {
            progress.Report("Consultando drivers no Windows Update...");
            const string script = @"$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Stop'; $b={param($v) [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$v))}; $session=New-Object -ComObject Microsoft.Update.Session; $searcher=$session.CreateUpdateSearcher(); $result=$searcher.Search(""IsInstalled=0 and Type='Driver' and IsHidden=0""); $lines=@(); foreach($u in $result.Updates){ $date=''; try{$date=$u.DriverVerDate.ToString('o')}catch{}; $lines += ((&$b $u.Title)+'|'+(&$b $u.DriverManufacturer)+'|'+(&$b $u.DriverProvider)+'|'+(&$b $u.DriverModel)+'|'+(&$b $u.DriverHardwareID)+'|'+[string]$u.Identity.UpdateID+'|'+[string]$u.MaxDownloadSize+'|'+[string]$u.RebootRequired+'|'+(&$b $date)+'|'+[string]$u.BrowseOnly+'|'+[string]$u.AutoSelectOnWebSites+'|'+[string]$u.IsMandatory+'|'+[string]$u.DeviceProblemNumber) }; $lines -join ""`r`n""";
            CommandExecution result = RunPowerShell(script, SearchTimeout, token);
            if (result.ExitCode != 0) throw new InvalidOperationException("O Windows Update não concluiu a busca. " + CompactError(result.Output));
            var updates = new List<DriverUpdate>();
            List<DriverInventoryItem> installed = ReadInstalledDrivers();
            foreach (string line in (result.Output ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = line.Trim().Split('|');
                long bytes;
                bool reboot, browseOnly, automatic, mandatory;
                int problemCode;
                if (fields.Length != 13 || !long.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out bytes) || !bool.TryParse(fields[7], out reboot) || !IsValidUpdateId(fields[5])) continue;
                if (!bool.TryParse(fields[9], out browseOnly)) browseOnly = false;
                if (!bool.TryParse(fields[10], out automatic)) automatic = false;
                if (!bool.TryParse(fields[11], out mandatory)) mandatory = false;
                if (!int.TryParse(fields[12], out problemCode)) problemCode = 0;
                string title = DecodeField(fields[0]);
                string manufacturer = DecodeField(fields[1]);
                string provider = DecodeField(fields[2]);
                if (string.IsNullOrWhiteSpace(provider)) provider = manufacturer;
                string model = DecodeField(fields[3]);
                string hardwareId = DecodeField(fields[4]);
                string availableDate = FormatIsoDate(DecodeField(fields[8]));
                string availableVersion = ExtractVersion(title);
                DriverInventoryItem current = FindInstalledDriver(installed, hardwareId, model, provider);
                bool firmware = ContainsAny(title + " " + model, "BIOS", "Firmware", "UEFI");
                bool olderRisk = IsPossiblyOlder(current, availableVersion, availableDate);
                bool alreadyInstalled = IsSameVersion(current, availableVersion);
                string classification = alreadyInstalled ? "Já instalada" : olderRisk ? "Possivelmente antiga" : firmware ? "Firmware / BIOS" : mandatory ? "Obrigatória" : browseOnly ? "Opcional" : automatic ? "Recomendada" : "Disponível";
                string supportName;
                string supportUrl = ResolveOfficialSupport(provider, title, out supportName);
                updates.Add(new DriverUpdate
                {
                    Selected = !olderRisk && !alreadyInstalled,
                    Title = title,
                    Provider = provider,
                    UpdateId = fields[5],
                    DownloadBytes = bytes,
                    RebootRequired = reboot,
                    SupportName = supportName,
                    SupportUrl = supportUrl,
                    CatalogUrl = BuildCatalogUrl(hardwareId, title),
                    HardwareId = hardwareId,
                    Model = model,
                    AvailableVersion = availableVersion,
                    AvailableDate = availableDate,
                    InstalledVersion = current == null ? string.Empty : current.Version,
                    Comparison = BuildComparison(current, availableVersion, availableDate),
                    Classification = classification,
                    IsFirmware = firmware,
                    IsOlderRisk = olderRisk
                });
            }
            return updates.OrderBy(delegate(DriverUpdate item) { return item.IsOlderRisk; }).ThenBy(delegate(DriverUpdate item) { return item.Classification; }).ThenBy(delegate(DriverUpdate item) { return item.Title; }).ToList();
        }

        public static string InstallUpdates(IEnumerable<string> updateIds, CancellationToken token, IProgress<string> progress)
        {
            if (!Optimizer.IsAdministrator()) return "A atualização de drivers exige privilégios de administrador. Use 'Executar como admin'.";
            string[] ids = (updateIds ?? Enumerable.Empty<string>()).Where(IsValidUpdateId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (ids.Length == 0) return "Nenhuma atualização de driver válida foi selecionada.";

            string backup = CreateDriverBackup(token, progress);
            if (backup.StartsWith("Falha", StringComparison.OrdinalIgnoreCase)) return "ATUALIZAÇÃO DE DRIVERS\r\n" + new string('=', 72) + "\r\n" + backup;

            progress.Report("Preparando atualizações de drivers...");
            string wanted = string.Join(",", ids.Select(delegate(string id) { return "'" + id + "'"; }));
            string script = @"$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Stop'; $wanted=@(" + wanted + @"); $session=New-Object -ComObject Microsoft.Update.Session; $searcher=$session.CreateUpdateSearcher(); $result=$searcher.Search(""IsInstalled=0 and Type='Driver' and IsHidden=0""); $selected=New-Object -ComObject Microsoft.Update.UpdateColl; foreach($u in $result.Updates){ if($wanted -contains [string]$u.Identity.UpdateID){ if(-not $u.EulaAccepted){$u.AcceptEula()}; [void]$selected.Add($u) } }; if($selected.Count -eq 0){ throw 'As atualizações selecionadas não estão mais disponíveis.' }; try { Checkpoint-Computer -Description 'Antes de atualizar drivers pelo Otimizador' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop } catch {}; $downloader=$session.CreateUpdateDownloader(); $downloader.Updates=$selected; $download=$downloader.Download(); $ready=New-Object -ComObject Microsoft.Update.UpdateColl; foreach($u in $selected){ if($u.IsDownloaded){[void]$ready.Add($u)} }; if($ready.Count -eq 0){ throw 'Nenhum driver foi baixado pelo Windows Update.' }; $installer=$session.CreateUpdateInstaller(); $installer.Updates=$ready; $install=$installer.Install(); $lines=@('ATUALIZAÇÃO DE DRIVERS','========================================================================','Origem: Windows Update','Drivers selecionados: '+$selected.Count,'Drivers baixados: '+$ready.Count,'Resultado geral: '+$install.ResultCode,'Reinicialização necessária: '+$(if($install.RebootRequired){'sim'}else{'não'})); for($i=0;$i -lt $ready.Count;$i++){ $item=$install.GetUpdateResult($i); $lines += $ready.Item($i).Title+' | resultado '+$item.ResultCode+' | HRESULT 0x'+('{0:X8}' -f ($item.HResult -band 0xffffffffL)) }; $lines -join ""`r`n""";
            CommandExecution result = RunPowerShell(script, InstallTimeout, token);
            if (result.ExitCode != 0) return "ATUALIZAÇÃO DE DRIVERS\r\n" + new string('=', 72) + "\r\nFalha: " + CompactError(result.Output);
            return backup + "\r\n\r\n" + (result.Output ?? string.Empty).Trim();
        }

        public static int CountInstalledDrivers()
        {
            try
            {
                int count = 0;
                using (var searcher = new ManagementObjectSearcher("SELECT DeviceName FROM Win32_PnPSignedDriver WHERE DeviceName IS NOT NULL"))
                using (ManagementObjectCollection results = searcher.Get()) foreach (ManagementObject ignored in results) count++;
                return count;
            }
            catch { return 0; }
        }

        public static List<DriverInventoryItem> ReadInstalledDrivers()
        {
            var items = new List<DriverInventoryItem>();
            Dictionary<string, DeviceProblem> problems = ReadDeviceProblems();
            AddBios(items);
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT DeviceName, DeviceID, HardwareID, Manufacturer, DriverVersion, DriverDate, DeviceClass, InfName, IsSigned FROM Win32_PnPSignedDriver WHERE DeviceName IS NOT NULL AND DriverVersion IS NOT NULL"))
                using (ManagementObjectCollection results = searcher.Get())
                foreach (ManagementObject driver in results)
                {
                    string deviceClass = Convert.ToString(driver["DeviceClass"]);
                    string device = Convert.ToString(driver["DeviceName"]);
                    string deviceId = Convert.ToString(driver["DeviceID"]);
                    string provider = Convert.ToString(driver["Manufacturer"]);
                    string category = DriverCategory(deviceClass, device, provider);
                    bool signed = Convert.ToBoolean(driver["IsSigned"] ?? false, CultureInfo.InvariantCulture);
                    DeviceProblem problem;
                    problems.TryGetValue(deviceId ?? string.Empty, out problem);
                    if (string.IsNullOrEmpty(category) && !signed) category = "Sem assinatura";
                    if (string.IsNullOrEmpty(category) && problem != null && problem.Code != 0) category = "Problema";
                    if (string.IsNullOrEmpty(category)) continue;
                    int problemCode = problem == null ? 0 : problem.Code;
                    items.Add(new DriverInventoryItem
                    {
                        Category = category,
                        Device = device,
                        Provider = string.IsNullOrWhiteSpace(provider) ? "Não informado" : provider,
                        Version = Convert.ToString(driver["DriverVersion"]),
                        Date = FormatDriverDate(Convert.ToString(driver["DriverDate"])),
                        InfName = Convert.ToString(driver["InfName"]),
                        DeviceId = deviceId,
                        HardwareId = Convert.ToString(driver["HardwareID"]),
                        Signed = signed,
                        ProblemCode = problemCode,
                        HasProblem = problemCode != 0 || !signed,
                        Status = DriverStatus(problemCode, signed)
                    });
                }
            }
            catch { }
            foreach (DeviceProblem problem in problems.Values.Where(delegate(DeviceProblem item) { return item.Code != 0 && !items.Any(delegate(DriverInventoryItem driver) { return string.Equals(driver.DeviceId, item.DeviceId, StringComparison.OrdinalIgnoreCase); }); }))
                items.Add(new DriverInventoryItem { Category = "Problema", Device = problem.Name, Provider = problem.DeviceClass, Version = "Driver ausente ou indisponível", Date = "—", InfName = "—", DeviceId = problem.DeviceId, HardwareId = problem.DeviceId, Signed = false, ProblemCode = problem.Code, HasProblem = true, Status = DriverStatus(problem.Code, false) });
            return items
                .GroupBy(delegate(DriverInventoryItem item) { return item.Category + "|" + item.Device + "|" + item.Version; }, StringComparer.OrdinalIgnoreCase)
                .Select(delegate(IGrouping<string, DriverInventoryItem> group) { return group.First(); })
                .OrderBy(delegate(DriverInventoryItem item) { return CategoryOrder(item.Category); })
                .ThenBy(delegate(DriverInventoryItem item) { return item.Device; }, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static string CreateDriverBackup(CancellationToken token, IProgress<string> progress)
        {
            if (!Optimizer.IsAdministrator()) return "Falha: o backup de drivers exige privilégios de administrador.";
            string folder = Path.Combine(DriverBackupFolder, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            try
            {
                Directory.CreateDirectory(folder);
                progress.Report("Exportando drivers atuais...");
                CommandExecution export = SystemCommand.Execute("pnputil.exe", "/export-driver * \"" + folder + "\"", 30 * 60 * 1000, token);
                if (export.ExitCode != 0) return "Falha: o Windows não concluiu o backup dos drivers. " + CompactError(export.Output);
                List<DriverInventoryItem> inventory = ReadInstalledDrivers();
                File.WriteAllLines(Path.Combine(folder, "inventario.txt"), new[] { "BACKUP DE DRIVERS", "Data: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), "Computador: " + Environment.MachineName, string.Empty }.Concat(inventory.Select(delegate(DriverInventoryItem item) { return item.Category + "\t" + item.Device + "\t" + item.Version + "\t" + item.InfName; })).ToArray(), Encoding.UTF8);
                return "Backup criado: " + folder;
            }
            catch (Exception ex) { return "Falha: " + ex.Message; }
        }

        public static string RestoreLatestDriverBackup(CancellationToken token, IProgress<string> progress)
        {
            if (!Optimizer.IsAdministrator()) return "A restauração de drivers exige privilégios de administrador.";
            string folder = LatestBackupFolder();
            if (string.IsNullOrEmpty(folder)) return "Nenhum backup de drivers foi encontrado.";
            progress.Report("Reinstalando drivers do backup...");
            CommandExecution restore = SystemCommand.Execute("pnputil.exe", "/add-driver \"" + Path.Combine(folder, "*.inf") + "\" /subdirs /install", 45 * 60 * 1000, token);
            var report = new StringBuilder("RESTAURAÇÃO DE DRIVERS\r\n" + new string('=', 72) + "\r\n");
            report.AppendLine("Backup: " + folder);
            report.AppendLine(restore.ExitCode == 0 ? "Resultado: pacotes reaplicados pelo Windows." : "Resultado: não concluído (código " + restore.ExitCode + ").");
            report.AppendLine((restore.Output ?? string.Empty).Trim());
            return report.ToString();
        }

        public static void OpenDriverBackups()
        {
            Directory.CreateDirectory(DriverBackupFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + DriverBackupFolder + "\"") { UseShellExecute = true });
        }

        public static DriverSafetyStatus ReadSafetyStatus()
        {
            var status = new DriverSafetyStatus { IsAdministrator = Optimizer.IsAdministrator(), BatteryPercent = -1 };
            try
            {
                PowerStatus power = SystemInformation.PowerStatus;
                status.AcConnected = power.PowerLineStatus == PowerLineStatus.Online;
                status.HasBattery = (power.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0;
                if (status.HasBattery && power.BatteryLifePercent >= 0) status.BatteryPercent = (int)Math.Round(power.BatteryLifePercent * 100);
            }
            catch { status.AcConnected = false; }
            ReadComputerIdentity(status);
            status.PendingRestart = HasPendingRestart();
            try
            {
                const string script = @"$ErrorActionPreference='Stop'; $v=Get-BitLockerVolume -MountPoint $env:SystemDrive; [string][int]$v.ProtectionStatus";
                CommandExecution bitLocker = RunPowerShell(script, 30000, CancellationToken.None);
                int protection;
                if (bitLocker.ExitCode == 0 && int.TryParse((bitLocker.Output ?? string.Empty).Trim(), out protection))
                {
                    status.BitLockerKnown = true;
                    status.BitLockerProtectionOn = protection == 1;
                }
            }
            catch { }
            bool batterySafe = !status.HasBattery || status.BatteryPercent < 0 || status.BatteryPercent >= 50;
            status.FirmwareSafe = status.IsAdministrator && status.AcConnected && batterySafe && !status.PendingRestart && status.BitLockerKnown && !status.BitLockerProtectionOn;
            status.Summary = BuildSafetySummary(status);
            return status;
        }

        public static string ValidateFirmwareSelection(IEnumerable<DriverUpdate> updates, DriverSafetyStatus status)
        {
            DriverUpdate[] firmware = (updates ?? Enumerable.Empty<DriverUpdate>()).Where(delegate(DriverUpdate item) { return item.IsFirmware; }).ToArray();
            if (firmware.Length == 0) return string.Empty;
            if (status == null || !status.FirmwareSafe) return "A atualização de BIOS/firmware foi bloqueada.\r\n\r\n" + (status == null ? "Não foi possível verificar a segurança do sistema." : status.Summary);
            string maker = NormalizeManufacturer(status.Manufacturer);
            if (!string.IsNullOrEmpty(maker) && firmware.Any(delegate(DriverUpdate item) { return !FirmwareMatchesManufacturer(item, maker); }))
                return "A atualização de firmware não corresponde claramente ao fabricante do computador (" + status.Manufacturer + ").";
            return string.Empty;
        }

        public static string BuildSearchReport(ICollection<DriverUpdate> updates)
        {
            var report = new StringBuilder("VERIFICAÇÃO DE DRIVERS\r\n" + new string('=', 72) + "\r\n");
            report.AppendLine("Origem: Windows Update");
            report.AppendLine("Atualizações encontradas: " + (updates == null ? 0 : updates.Count));
            if (updates != null) foreach (DriverUpdate update in updates) report.AppendLine("• " + update.Title);
            return report.ToString();
        }

        public static void OpenWindowsUpdate()
        {
            Process.Start(new ProcessStartInfo("ms-settings:windowsupdate-optionalupdates") { UseShellExecute = true });
        }

        public static void OpenOfficialSupport(string url)
        {
            if (!IsOfficialSupportUrl(url)) throw new InvalidOperationException("O endereço de suporte não pertence a um fabricante autorizado.");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        internal static bool IsValidUpdateIdForTesting(string value)
        {
            return IsValidUpdateId(value);
        }

        internal static string ResolveOfficialSupportForTesting(string provider, string title)
        {
            string name;
            return ResolveOfficialSupport(provider, title, out name);
        }

        internal static bool IsOfficialSupportUrlForTesting(string value)
        {
            return IsOfficialSupportUrl(value);
        }

        internal static string BuildCatalogUrlForTesting(string hardwareId, string title)
        {
            return BuildCatalogUrl(hardwareId, title);
        }

        private static bool IsValidUpdateId(string value)
        {
            Guid parsed;
            return Guid.TryParse(value, out parsed);
        }

        private static CommandExecution RunPowerShell(string script, int timeout, CancellationToken token)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            return SystemCommand.Execute("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, timeout, token);
        }

        private static string CompactError(string output)
        {
            string value = (output ?? string.Empty).Trim().Replace("\r", " ").Replace("\n", " ");
            while (value.Contains("  ")) value = value.Replace("  ", " ");
            if (value.Length > 500) value = value.Substring(0, 500) + "...";
            return string.IsNullOrEmpty(value) ? "erro não informado." : value;
        }

        private static string DecodeField(string value)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
            catch { return string.Empty; }
        }

        private static string ResolveOfficialSupport(string provider, string title, out string supportName)
        {
            string value = (provider ?? string.Empty) + " " + (title ?? string.Empty);
            if (ContainsAny(value, "Intel")) { supportName = "Intel"; return "https://www.intel.com/content/www/us/en/support/detect.html"; }
            if (ContainsAny(value, "NVIDIA")) { supportName = "NVIDIA"; return "https://www.nvidia.com/en-us/drivers/"; }
            if (ContainsAny(value, "AMD Radeon", "AMD Software", "Advanced Micro Devices", "ATI Technologies", "Radeon")) { supportName = "AMD"; return "https://www.amd.com/en/support/download/drivers.html"; }
            if (ContainsAny(value, "Dell")) { supportName = "Dell"; return "https://www.dell.com/support/home/en-us?app=drivers"; }
            if (ContainsAny(value, "Hewlett-Packard", "Hewlett Packard", "HP Inc", " HP ")) { supportName = "HP"; return "https://support.hp.com/us-en/drivers"; }
            if (ContainsAny(value, "Lenovo")) { supportName = "Lenovo"; return "https://pcsupport.lenovo.com/us/en/"; }
            if (ContainsAny(value, "ASUS", "ASUSTeK")) { supportName = "ASUS"; return "https://www.asus.com/support/download-center/"; }
            if (ContainsAny(value, "Acer")) { supportName = "Acer"; return "https://www.acer.com/us-en/support/drivers-and-manuals"; }
            if (ContainsAny(value, "Micro-Star", "MSI")) { supportName = "MSI"; return "https://www.msi.com/support/download"; }
            if (ContainsAny(value, "Gigabyte")) { supportName = "Gigabyte"; return "https://www.gigabyte.com/Support"; }
            if (ContainsAny(value, "Realtek")) { supportName = "Realtek"; return "https://www.realtek.com/Download/Index"; }
            if (ContainsAny(value, "Samsung")) { supportName = "Samsung"; return "https://www.samsung.com/us/support/downloads/"; }
            if (ContainsAny(value, "Qualcomm")) { supportName = "Qualcomm"; return "https://www.qualcomm.com/support"; }
            if (ContainsAny(value, "Broadcom")) { supportName = "Broadcom"; return "https://www.broadcom.com/support/download-search"; }
            supportName = "Catálogo Microsoft";
            return "https://www.catalog.update.microsoft.com/Search.aspx?q=" + Uri.EscapeDataString(title ?? string.Empty);
        }

        private static string BuildCatalogUrl(string hardwareId, string title)
        {
            string query = string.IsNullOrWhiteSpace(hardwareId) ? title : hardwareId;
            return "https://www.catalog.update.microsoft.com/Search.aspx?q=" + Uri.EscapeDataString(query ?? string.Empty);
        }

        private static bool IsOfficialSupportUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host)) return false;
            return OfficialSupportDomains.Any(delegate(string domain) { return uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase); });
        }

        private sealed class DeviceProblem
        {
            public string DeviceId { get; set; }
            public string Name { get; set; }
            public string DeviceClass { get; set; }
            public int Code { get; set; }
        }

        private static Dictionary<string, DeviceProblem> ReadDeviceProblems()
        {
            var items = new Dictionary<string, DeviceProblem>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, DeviceID, PNPClass, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE DeviceID IS NOT NULL"))
                using (ManagementObjectCollection results = searcher.Get())
                foreach (ManagementObject device in results)
                {
                    string id = Convert.ToString(device["DeviceID"]);
                    int code = Convert.ToInt32(device["ConfigManagerErrorCode"] ?? 0, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(id)) items[id] = new DeviceProblem { DeviceId = id, Name = Convert.ToString(device["Name"]), DeviceClass = Convert.ToString(device["PNPClass"]), Code = code };
                }
            }
            catch { }
            return items;
        }

        private static string DriverStatus(int problemCode, bool signed)
        {
            if (problemCode == 22) return "Desativado";
            if (problemCode == 28) return "Driver ausente";
            if (problemCode != 0) return "Erro " + problemCode;
            return signed ? "OK" : "Sem assinatura";
        }

        private static DriverInventoryItem FindInstalledDriver(IEnumerable<DriverInventoryItem> installed, string hardwareId, string model, string provider)
        {
            string normalized = NormalizeHardwareId(hardwareId);
            DriverInventoryItem exact = installed.FirstOrDefault(delegate(DriverInventoryItem item) { return !string.IsNullOrEmpty(normalized) && NormalizeHardwareId(item.HardwareId) == normalized; });
            if (exact != null) return exact;
            return installed.FirstOrDefault(delegate(DriverInventoryItem item)
            {
                bool modelMatch = !string.IsNullOrWhiteSpace(model) && item.Device.IndexOf(model, StringComparison.OrdinalIgnoreCase) >= 0;
                bool providerMatch = !string.IsNullOrWhiteSpace(provider) && item.Provider.IndexOf(provider.Split(' ')[0], StringComparison.OrdinalIgnoreCase) >= 0;
                return modelMatch && providerMatch;
            });
        }

        private static string NormalizeHardwareId(string value)
        {
            return Regex.Replace(value ?? string.Empty, @"\s+", string.Empty).Trim().ToUpperInvariant();
        }

        private static string ExtractVersion(string title)
        {
            MatchCollection matches = Regex.Matches(title ?? string.Empty, @"(?<!\d)(\d+(?:\.\d+){1,4})(?!\d)");
            return matches.Count == 0 ? string.Empty : matches[matches.Count - 1].Groups[1].Value;
        }

        private static string FormatIsoDate(string value)
        {
            DateTime date;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date) ? DisplayDriverDate(date) : string.Empty;
        }

        private static bool IsPossiblyOlder(DriverInventoryItem current, string availableVersion, string availableDate)
        {
            if (current == null) return false;
            Version installedVersion, offeredVersion;
            if (Version.TryParse(current.Version, out installedVersion) && Version.TryParse(availableVersion, out offeredVersion)) return offeredVersion < installedVersion;
            DateTime installedDate, offeredDate;
            return DateTime.TryParseExact(current.Date, "dd/MM/yyyy", CultureInfo.CurrentCulture, DateTimeStyles.None, out installedDate) && DateTime.TryParseExact(availableDate, "dd/MM/yyyy", CultureInfo.CurrentCulture, DateTimeStyles.None, out offeredDate) && offeredDate < installedDate;
        }

        private static bool IsSameVersion(DriverInventoryItem current, string availableVersion)
        {
            Version installed, offered;
            return current != null && Version.TryParse(current.Version, out installed) && Version.TryParse(availableVersion, out offered) && installed == offered;
        }

        private static string BuildComparison(DriverInventoryItem current, string availableVersion, string availableDate)
        {
            if (current == null) return string.IsNullOrWhiteSpace(availableVersion) ? "Não instalada" : "Disponível " + availableVersion;
            if (!string.IsNullOrWhiteSpace(availableVersion)) return current.Version + " → " + availableVersion;
            return string.IsNullOrWhiteSpace(availableDate) ? "Instalada " + current.Version : "Instalada " + current.Version + " • pacote " + availableDate;
        }

        private static string LatestBackupFolder()
        {
            try { return Directory.Exists(DriverBackupFolder) ? Directory.GetDirectories(DriverBackupFolder).OrderByDescending(delegate(string path) { return path; }, StringComparer.OrdinalIgnoreCase).FirstOrDefault() : null; }
            catch { return null; }
        }

        private static void ReadComputerIdentity(DriverSafetyStatus status)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
                using (ManagementObjectCollection results = searcher.Get())
                foreach (ManagementObject system in results) { status.Manufacturer = Convert.ToString(system["Manufacturer"]); status.Model = Convert.ToString(system["Model"]); break; }
            }
            catch { }
        }

        private static bool HasPendingRestart()
        {
            try
            {
                using (RegistryKey servicing = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending")) if (servicing != null) return true;
                using (RegistryKey update = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")) if (update != null) return true;
                using (RegistryKey session = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager")) return session != null && session.GetValue("PendingFileRenameOperations") != null;
            }
            catch { return false; }
        }

        private static string BuildSafetySummary(DriverSafetyStatus status)
        {
            var lines = new List<string>
            {
                "Computador: " + (string.IsNullOrWhiteSpace(status.Manufacturer) ? "não identificado" : status.Manufacturer + " " + status.Model),
                "Energia: " + (status.AcConnected ? "conectado à tomada" : "sem alimentação externa"),
                "Bateria: " + (!status.HasBattery ? "não aplicável" : status.BatteryPercent < 0 ? "não informada" : status.BatteryPercent + "%"),
                "BitLocker: " + (!status.BitLockerKnown ? "não foi possível verificar" : status.BitLockerProtectionOn ? "proteção ativa" : "proteção suspensa ou inativa"),
                "Reinicialização pendente: " + (status.PendingRestart ? "sim" : "não"),
                "Administrador: " + (status.IsAdministrator ? "sim" : "não")
            };
            return string.Join("\r\n", lines.ToArray());
        }

        private static string NormalizeManufacturer(string manufacturer)
        {
            string value = manufacturer ?? string.Empty;
            if (ContainsAny(value, "Dell")) return "Dell";
            if (ContainsAny(value, "HP", "Hewlett")) return "HP";
            if (ContainsAny(value, "Lenovo")) return "Lenovo";
            if (ContainsAny(value, "ASUS", "ASUSTeK")) return "ASUS";
            if (ContainsAny(value, "Acer")) return "Acer";
            if (ContainsAny(value, "MSI", "Micro-Star")) return "MSI";
            if (ContainsAny(value, "Gigabyte")) return "Gigabyte";
            return string.Empty;
        }

        private static bool FirmwareMatchesManufacturer(DriverUpdate update, string manufacturer)
        {
            string value = (update.Provider ?? string.Empty) + " " + (update.Title ?? string.Empty) + " " + (update.Model ?? string.Empty);
            if (value.IndexOf(manufacturer, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return !string.IsNullOrWhiteSpace(update.HardwareId) && update.HardwareId.StartsWith("UEFI", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddBios(ICollection<DriverInventoryItem> items)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"))
                using (ManagementObjectCollection results = searcher.Get())
                foreach (ManagementObject bios in results)
                {
                    string version = Convert.ToString(bios["SMBIOSBIOSVersion"]);
                    if (string.IsNullOrWhiteSpace(version)) continue;
                    items.Add(new DriverInventoryItem { Category = "BIOS", Device = "BIOS / UEFI do sistema", Provider = Convert.ToString(bios["Manufacturer"]), Version = version, Date = FormatDriverDate(Convert.ToString(bios["ReleaseDate"])), InfName = "Firmware da placa-mãe", Signed = true, Status = "OK" });
                }
            }
            catch { }
        }

        private static string DriverCategory(string deviceClass, string device, string provider)
        {
            string kind = (deviceClass ?? string.Empty).ToUpperInvariant();
            string name = device ?? string.Empty;
            string maker = provider ?? string.Empty;
            if (kind == "DISPLAY") return "Vídeo";
            if (kind == "FIRMWARE") return "Firmware";
            if (kind == "MEDIA") return "Áudio";
            if (kind == "SCSIADAPTER" || kind == "HDC") return "Armazenamento";
            if (kind == "BLUETOOTH") return "Bluetooth";
            if (kind == "NET")
            {
                if (ContainsAny(name, "WAN Miniport", "Virtual", "Kernel Debug", "Bluetooth Device")) return string.Empty;
                return "Rede";
            }
            if (kind == "USB")
            {
                if (maker.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0) return string.Empty;
                return "USB";
            }
            if (kind == "SYSTEM" && ContainsAny(name, "Chipset", "SMBus", "PCI Express", "PCIe", "LPC", "Management Engine", "Serial IO", "GPIO", "Dynamic Tuning", "Platform", "Host Bridge", "Root Port", "Thermal")) return "Chipset / sistema";
            return string.Empty;
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            foreach (string term in terms) if ((value ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static int CategoryOrder(string category)
        {
            string[] order = { "Problema", "Sem assinatura", "Vídeo", "BIOS", "Firmware", "Chipset / sistema", "Áudio", "Rede", "Armazenamento", "Bluetooth", "USB" };
            int index = Array.IndexOf(order, category);
            return index < 0 ? order.Length : index;
        }

        private static string FormatDriverDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Não informada";
            try { return DisplayDriverDate(ManagementDateTimeConverter.ToDateTime(value)); }
            catch
            {
                DateTime parsed;
                return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed) ? DisplayDriverDate(parsed) : "Não informada";
            }
        }

        private static string DisplayDriverDate(DateTime value)
        {
            return value.Year < 2010 ? "Data padrão" : value.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);
        }
    }
}
