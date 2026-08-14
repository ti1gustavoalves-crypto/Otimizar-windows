using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace CodexPerformanceOptimizer
{
    internal sealed class RepairFinding
    {
        public string Id { get; set; }
        public string Area { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string Detail { get; set; }
        public bool Selected { get; set; }
        public bool CanRepair { get; set; }
        public bool RequiresAdministrator { get; set; }
        public bool RestartRequired { get; set; }
        public bool Warning { get; set; }
        public string ActionLabel { get; set; }
    }

    internal static class GeneralRepairEngine
    {
        private static readonly string[] BrowserProcesses = { "chrome", "msedge", "firefox", "brave" };

        public static List<RepairFinding> Scan(CancellationToken token, IProgress<string> progress)
        {
            var findings = new List<RepairFinding>();
            progress.Report("Verificando rede e resolução de nomes...");
            CommandExecution dns = SystemCommand.Execute("nslookup.exe", "microsoft.com", 10000, token);
            findings.Add(New("flush-dns", "Rede", "Cache de DNS", dns.ExitCode == 0 ? "OK" : "Atenção",
                dns.ExitCode == 0 ? "A resolução de nomes respondeu normalmente." : "A resolução de nomes falhou ou demorou demais.",
                dns.ExitCode != 0, true, false, false, "Corrigir"));

            token.ThrowIfCancellationRequested();
            progress.Report("Verificando configurações de proxy...");
            int proxyEnabled = 0;
            string proxyServer = string.Empty;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    proxyEnabled = Convert.ToInt32(key == null ? 0 : key.GetValue("ProxyEnable", 0));
                    proxyServer = Convert.ToString(key == null ? null : key.GetValue("ProxyServer", string.Empty));
                }
            }
            catch { }
            bool proxyConfigured = proxyEnabled != 0 || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HTTP_PROXY")) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HTTPS_PROXY"));
            findings.Add(New("proxy-settings", "Rede", "Proxy do usuário", proxyConfigured ? "Revisar" : "OK",
                proxyConfigured ? "Há um proxy configurado" + (string.IsNullOrWhiteSpace(proxyServer) ? "." : ": " + proxyServer) : "Nenhum proxy manual foi detectado.",
                proxyConfigured, false, false, false, "Abrir"));

            CommandExecution winHttp = SystemCommand.Execute("netsh.exe", "winhttp show proxy", 10000, token);
            findings.Add(New("proxy-settings", "Rede", "Proxy WinHTTP", winHttp.ExitCode == 0 ? "OK" : "Revisar",
                winHttp.ExitCode == 0 ? "A configuração de proxy dos serviços do Windows pôde ser consultada." : "Não foi possível consultar o proxy usado pelos serviços do Windows.",
                false, false, false, false, "Abrir"));

            token.ThrowIfCancellationRequested();
            CommandExecution winsock = SystemCommand.Execute("netsh.exe", "winsock show catalog", 15000, token);
            findings.Add(New("reset-winsock", "Rede", "Catálogo Winsock", winsock.ExitCode == 0 ? "OK" : "Atenção",
                winsock.ExitCode == 0 ? "O catálogo de rede pôde ser consultado." : "O catálogo de rede não respondeu corretamente.",
                winsock.ExitCode != 0, winsock.ExitCode != 0, true, true, "Corrigir"));

            token.ThrowIfCancellationRequested();
            progress.Report("Testando a pasta temporária...");
            bool tempOk = TestTemporaryFolder();
            findings.Add(New("open-temp", "Sistema", "Pasta temporária", tempOk ? "OK" : "Atenção",
                tempOk ? "A pasta temporária aceita criação e remoção de arquivos." : "O Windows não conseguiu gravar na pasta temporária.",
                !tempOk, false, false, false, "Abrir"));

            CommandExecution networkServices = SystemCommand.Execute("sc.exe", "query Dnscache", 10000, token);
            findings.Add(New("open-services", "Rede", "Serviços essenciais", networkServices.ExitCode == 0 ? "OK" : "Revisar",
                networkServices.ExitCode == 0 ? "O serviço de cache DNS está registrado no Windows." : "O serviço de cache DNS não pôde ser consultado.",
                false, false, false, false, "Abrir"));

            token.ThrowIfCancellationRequested();
            progress.Report("Medindo caches dos navegadores...");
            long cacheBytes = BrowserCacheFolders().Where(Directory.Exists).Aggregate(0L, delegate(long total, string folder) { return total + SafeDirectorySize(folder); });
            bool browsersRunning = BrowserProcesses.Any(name => Process.GetProcessesByName(name).Length > 0);
            findings.Add(New("backup-browser-caches", "Navegadores", "Caches dos navegadores", cacheBytes >= 256L * 1024 * 1024 ? "Pode limpar" : "OK",
                V2Engine.FormatBytes(cacheBytes) + " em caches conhecidos" + (browsersRunning ? " • feche os navegadores para limpar" : "."),
                cacheBytes >= 256L * 1024 * 1024 && !browsersRunning, cacheBytes > 0 && !browsersRunning, false, false, "Limpar"));

            findings.Add(New("integrity", "Windows", "Integridade do sistema", "Disponível",
                "Use a verificação oficial DISM, SFC e CHKDSK na seção Integridade.", false, false, false, false, "Abrir"));
            return findings;
        }

        public static string Execute(IEnumerable<string> ids, CancellationToken token, IProgress<string> progress)
        {
            var report = new StringBuilder("CORREÇÕES GERAIS\r\n" + new string('=', 72) + "\r\n");
            foreach (string id in (ids ?? Enumerable.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                if (id == "flush-dns")
                {
                    progress.Report("Limpando o cache de DNS...");
                    CommandExecution result = SystemCommand.Execute("ipconfig.exe", "/flushdns", 15000, token);
                    report.AppendLine(result.ExitCode == 0 ? "✓ Cache de DNS renovado." : "! DNS: " + FirstLine(result.Output));
                }
                else if (id == "reset-winsock")
                {
                    progress.Report("Redefinindo o catálogo Winsock...");
                    CommandExecution result = SystemCommand.Execute("netsh.exe", "winsock reset", 30000, token);
                    report.AppendLine(result.ExitCode == 0 ? "✓ Winsock redefinido; reinicie o computador." : "! Winsock: " + FirstLine(result.Output));
                }
                else if (id == "backup-browser-caches") report.AppendLine(BackupBrowserCaches(token, progress));
            }
            return report.ToString();
        }

        private static RepairFinding New(string id, string area, string title, string status, string detail, bool selected, bool canRepair, bool admin, bool restart, string action)
        {
            return new RepairFinding { Id = id, Area = area, Title = title, Status = status, Detail = detail, Selected = selected && canRepair, CanRepair = canRepair, RequiresAdministrator = admin, RestartRequired = restart, Warning = status == "Atenção" || status == "Revisar" || status == "Pode limpar", ActionLabel = action };
        }

        private static bool TestTemporaryFolder()
        {
            string path = null;
            try { path = Path.Combine(Path.GetTempPath(), "otimizador-" + Guid.NewGuid().ToString("N") + ".tmp"); File.WriteAllText(path, "teste"); File.Delete(path); return true; }
            catch { try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch { } return false; }
        }

        private static IEnumerable<string> BrowserCacheFolders()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            yield return Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Cache");
            yield return Path.Combine(local, "Microsoft", "Edge", "User Data", "Default", "Cache");
            yield return Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache");
            string firefox = Path.Combine(roaming, "Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(firefox))
            {
                string[] folders = new string[0];
                try { folders = Directory.GetDirectories(firefox, "cache2", SearchOption.AllDirectories); } catch { }
                foreach (string folder in folders) yield return folder;
            }
        }

        private static long SafeDirectorySize(string path)
        {
            try { return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(file => { try { return new FileInfo(file).Length; } catch { return 0L; } }); }
            catch { return 0; }
        }

        private static string BackupBrowserCaches(CancellationToken token, IProgress<string> progress)
        {
            if (BrowserProcesses.Any(name => Process.GetProcessesByName(name).Length > 0)) return "! Feche os navegadores antes de limpar os caches.";
            string destination = Path.Combine(AppPaths.RepairBackupsFolder, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(destination);
            int moved = 0;
            foreach (string folder in BrowserCacheFolders().Where(Directory.Exists).ToArray())
            {
                token.ThrowIfCancellationRequested();
                progress.Report("Movendo cache para backup reversível...");
                try
                {
                    string target = Path.Combine(destination, moved.ToString("00") + "-" + new DirectoryInfo(folder).Parent.Name + "-" + Path.GetFileName(folder));
                    Directory.Move(folder, target);
                    moved++;
                }
                catch { }
            }
            return moved == 0 ? "! Nenhum cache pôde ser movido." : "✓ " + moved + " cache(s) movido(s) para backup reversível: " + destination;
        }

        private static string FirstLine(string value)
        {
            return (value ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "falha não detalhada";
        }
    }
}
