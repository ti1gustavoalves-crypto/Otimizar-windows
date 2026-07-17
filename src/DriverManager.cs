using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;

namespace CodexPerformanceOptimizer
{
    internal static class DriverManager
    {
        private const int SearchTimeout = 10 * 60 * 1000;
        private const int InstallTimeout = 60 * 60 * 1000;
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
            const string script = @"$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Stop'; $b={param($v) [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$v))}; $session=New-Object -ComObject Microsoft.Update.Session; $searcher=$session.CreateUpdateSearcher(); $result=$searcher.Search(""IsInstalled=0 and Type='Driver' and IsHidden=0""); $lines=@(); foreach($u in $result.Updates){ $lines += ((&$b $u.Title)+'|'+(&$b $u.DriverManufacturer)+'|'+[string]$u.Identity.UpdateID+'|'+[string]$u.MaxDownloadSize+'|'+[string]$u.RebootRequired) }; $lines -join ""`r`n""";
            CommandExecution result = RunPowerShell(script, SearchTimeout, token);
            if (result.ExitCode != 0) throw new InvalidOperationException("O Windows Update não concluiu a busca. " + CompactError(result.Output));
            var updates = new List<DriverUpdate>();
            foreach (string line in (result.Output ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = line.Trim().Split('|');
                long bytes;
                bool reboot;
                if (fields.Length != 5 || !long.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out bytes) || !bool.TryParse(fields[4], out reboot) || !IsValidUpdateId(fields[2])) continue;
                string title = DecodeField(fields[0]);
                string provider = DecodeField(fields[1]);
                string supportName;
                string supportUrl = ResolveOfficialSupport(provider, title, out supportName);
                updates.Add(new DriverUpdate { Selected = true, Title = title, Provider = provider, UpdateId = fields[2], DownloadBytes = bytes, RebootRequired = reboot, SupportName = supportName, SupportUrl = supportUrl });
            }
            return updates.OrderBy(delegate(DriverUpdate item) { return item.Title; }).ToList();
        }

        public static string InstallUpdates(IEnumerable<string> updateIds, CancellationToken token, IProgress<string> progress)
        {
            if (!Optimizer.IsAdministrator()) return "A atualização de drivers exige privilégios de administrador. Use 'Executar como admin'.";
            string[] ids = (updateIds ?? Enumerable.Empty<string>()).Where(IsValidUpdateId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (ids.Length == 0) return "Nenhuma atualização de driver válida foi selecionada.";

            progress.Report("Preparando atualizações de drivers...");
            string wanted = string.Join(",", ids.Select(delegate(string id) { return "'" + id + "'"; }));
            string script = @"$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Stop'; $wanted=@(" + wanted + @"); $session=New-Object -ComObject Microsoft.Update.Session; $searcher=$session.CreateUpdateSearcher(); $result=$searcher.Search(""IsInstalled=0 and Type='Driver' and IsHidden=0""); $selected=New-Object -ComObject Microsoft.Update.UpdateColl; foreach($u in $result.Updates){ if($wanted -contains [string]$u.Identity.UpdateID){ if(-not $u.EulaAccepted){$u.AcceptEula()}; [void]$selected.Add($u) } }; if($selected.Count -eq 0){ throw 'As atualizações selecionadas não estão mais disponíveis.' }; try { Checkpoint-Computer -Description 'Antes de atualizar drivers pelo Otimizador' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop } catch {}; $downloader=$session.CreateUpdateDownloader(); $downloader.Updates=$selected; $download=$downloader.Download(); $ready=New-Object -ComObject Microsoft.Update.UpdateColl; foreach($u in $selected){ if($u.IsDownloaded){[void]$ready.Add($u)} }; if($ready.Count -eq 0){ throw 'Nenhum driver foi baixado pelo Windows Update.' }; $installer=$session.CreateUpdateInstaller(); $installer.Updates=$ready; $install=$installer.Install(); $lines=@('ATUALIZAÇÃO DE DRIVERS','========================================================================','Origem: Windows Update','Drivers selecionados: '+$selected.Count,'Drivers baixados: '+$ready.Count,'Resultado geral: '+$install.ResultCode,'Reinicialização necessária: '+$(if($install.RebootRequired){'sim'}else{'não'})); for($i=0;$i -lt $ready.Count;$i++){ $item=$install.GetUpdateResult($i); $lines += $ready.Item($i).Title+' | resultado '+$item.ResultCode+' | HRESULT 0x'+('{0:X8}' -f ($item.HResult -band 0xffffffffL)) }; $lines -join ""`r`n""";
            CommandExecution result = RunPowerShell(script, InstallTimeout, token);
            if (result.ExitCode != 0) return "ATUALIZAÇÃO DE DRIVERS\r\n" + new string('=', 72) + "\r\nFalha: " + CompactError(result.Output);
            return (result.Output ?? string.Empty).Trim();
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
            AddBios(items);
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT DeviceName, Manufacturer, DriverVersion, DriverDate, DeviceClass, InfName FROM Win32_PnPSignedDriver WHERE DeviceName IS NOT NULL AND DriverVersion IS NOT NULL"))
                using (ManagementObjectCollection results = searcher.Get())
                foreach (ManagementObject driver in results)
                {
                    string deviceClass = Convert.ToString(driver["DeviceClass"]);
                    string device = Convert.ToString(driver["DeviceName"]);
                    string provider = Convert.ToString(driver["Manufacturer"]);
                    string category = DriverCategory(deviceClass, device, provider);
                    if (string.IsNullOrEmpty(category)) continue;
                    items.Add(new DriverInventoryItem
                    {
                        Category = category,
                        Device = device,
                        Provider = string.IsNullOrWhiteSpace(provider) ? "Não informado" : provider,
                        Version = Convert.ToString(driver["DriverVersion"]),
                        Date = FormatDriverDate(Convert.ToString(driver["DriverDate"])),
                        InfName = Convert.ToString(driver["InfName"])
                    });
                }
            }
            catch { }
            return items
                .GroupBy(delegate(DriverInventoryItem item) { return item.Category + "|" + item.Device + "|" + item.Version; }, StringComparer.OrdinalIgnoreCase)
                .Select(delegate(IGrouping<string, DriverInventoryItem> group) { return group.First(); })
                .OrderBy(delegate(DriverInventoryItem item) { return CategoryOrder(item.Category); })
                .ThenBy(delegate(DriverInventoryItem item) { return item.Device; }, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
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

        private static bool IsOfficialSupportUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host)) return false;
            return OfficialSupportDomains.Any(delegate(string domain) { return uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase); });
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
                    items.Add(new DriverInventoryItem { Category = "BIOS", Device = "BIOS / UEFI do sistema", Provider = Convert.ToString(bios["Manufacturer"]), Version = version, Date = FormatDriverDate(Convert.ToString(bios["ReleaseDate"])), InfName = "Firmware da placa-mãe" });
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
            string[] order = { "Vídeo", "BIOS", "Firmware", "Chipset / sistema", "Áudio", "Rede", "Armazenamento", "Bluetooth", "USB" };
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
