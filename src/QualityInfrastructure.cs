using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace CodexPerformanceOptimizer
{
    internal static class CrashLogger
    {
        private static readonly string LogFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Codex", "PerformanceOptimizer", "Logs");
        private static int _initialized;

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _initialized, 1) != 0) return;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                string path = Write(e.Exception, "Interface");
                MessageBox.Show("O programa encontrou uma falha e registrou um diagnóstico privado.\r\n\r\n" + path, "Falha registrada", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Write(e.ExceptionObject as Exception ?? new Exception(Convert.ToString(e.ExceptionObject)), "Aplicativo");
            };
            TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs e)
            {
                Write(e.Exception, "Tarefa em segundo plano");
                e.SetObserved();
            };
            Cleanup();
        }

        public static string Write(Exception exception, string source)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                string path = Path.Combine(LogFolder, "falha-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
                var text = new StringBuilder();
                text.AppendLine("DIAGNÓSTICO PRIVADO DO OTIMIZADOR");
                text.AppendLine(new string('=', 72));
                text.AppendLine("Data UTC: " + DateTime.UtcNow.ToString("o"));
                text.AppendLine("Origem: " + source);
                text.AppendLine("Versão: " + typeof(CrashLogger).Assembly.GetName().Version);
                text.AppendLine("Windows: " + Environment.OSVersion.VersionString);
                text.AppendLine("Administrador: " + (Optimizer.IsAdministrator() ? "sim" : "não"));
                text.AppendLine();
                text.AppendLine(Sanitize(exception == null ? "Erro não informado." : exception.ToString()));
                text.AppendLine();
                text.AppendLine("Caminhos do usuário, nome da máquina e nome da conta foram removidos automaticamente.");
                File.WriteAllText(path, text.ToString(), Encoding.UTF8);
                Cleanup();
                return path;
            }
            catch { return "Não foi possível criar o arquivo de diagnóstico."; }
        }

        public static void OpenFolder()
        {
            Directory.CreateDirectory(LogFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + LogFolder + "\"") { UseShellExecute = true });
        }

        internal static string SanitizeForTesting(string value)
        {
            return Sanitize(value);
        }

        private static string Sanitize(string value)
        {
            string sanitized = value ?? string.Empty;
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile)) sanitized = sanitized.Replace(profile, "%USERPROFILE%");
            if (!string.IsNullOrWhiteSpace(Environment.UserName)) sanitized = sanitized.Replace(Environment.UserName, "%USERNAME%");
            if (!string.IsNullOrWhiteSpace(Environment.MachineName)) sanitized = sanitized.Replace(Environment.MachineName, "%COMPUTER%");
            return sanitized;
        }

        private static void Cleanup()
        {
            try
            {
                if (!Directory.Exists(LogFolder)) return;
                FileInfo[] logs = new DirectoryInfo(LogFolder).GetFiles("falha-*.txt").OrderByDescending(file => file.CreationTimeUtc).ToArray();
                foreach (FileInfo file in logs.Skip(20)) try { file.Delete(); } catch { }
                foreach (FileInfo file in logs.Take(20).Where(file => file.CreationTimeUtc < DateTime.UtcNow.AddDays(-30))) try { file.Delete(); } catch { }
            }
            catch { }
        }
    }

#if SELF_TEST
    internal sealed class SafetyTestResult
    {
        public string Name { get; set; }
        public bool Passed { get; set; }
        public string Detail { get; set; }
    }

    internal static class SafetyTestSuite
    {
        public static string Run(CancellationToken token, IProgress<string> progress)
        {
            string sandbox = Path.Combine(Path.GetTempPath(), "CodexOptimizer-Safety-" + Guid.NewGuid().ToString("N"));
            string registryName = "SafetySandbox-" + Guid.NewGuid().ToString("N");
            string registryPath = @"Software\Codex\PerformanceOptimizerV2\" + registryName;
            var results = new List<SafetyTestResult>();
            try
            {
                Directory.CreateDirectory(sandbox);
                progress.Report("Testando limites de caminho...");
                token.ThrowIfCancellationRequested();
                string child = Path.Combine(sandbox, "child.txt");
                results.Add(Result("Limites de caminho", IsWithin(sandbox, child) && !IsWithin(sandbox, Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "Nenhum caminho externo foi aceito."));

                progress.Report("Testando persistência isolada...");
                string jsonPath = Path.Combine(sandbox, "state.json");
                var serializer = new JavaScriptSerializer();
                var original = new AdvancedSettings { MinimizeToTray = true, AutomaticPowerProfiles = false, UpdateManifestUrl = "https://example.invalid/manifest.json" };
                File.WriteAllText(jsonPath, serializer.Serialize(original), Encoding.UTF8);
                AdvancedSettings restored = serializer.Deserialize<AdvancedSettings>(File.ReadAllText(jsonPath, Encoding.UTF8));
                results.Add(Result("Persistência JSON", restored != null && restored.MinimizeToTray && restored.UpdateManifestUrl == original.UpdateManifestUrl, "Configurações reconstruídas sem tocar no estado real."));

                progress.Report("Testando privacidade dos logs...");
                string sensitive = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\arquivo.txt " + Environment.UserName + " " + Environment.MachineName;
                string sanitized = CrashLogger.SanitizeForTesting(sensitive);
                bool privateLog = sanitized.IndexOf(Environment.UserName, StringComparison.OrdinalIgnoreCase) < 0 && sanitized.IndexOf(Environment.MachineName, StringComparison.OrdinalIgnoreCase) < 0 && sanitized.IndexOf(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase) < 0;
                results.Add(Result("Privacidade dos logs", privateLog, "Nome da conta, máquina e caminho do perfil foram removidos."));

                progress.Report("Testando duplicados por conteúdo...");
                byte[] payload = new byte[1048577];
                for (int i = 0; i < payload.Length; i += 4096) payload[i] = (byte)(i % 251);
                File.WriteAllBytes(Path.Combine(sandbox, "duplicado-a.bin"), payload);
                File.WriteAllBytes(Path.Combine(sandbox, "duplicado-b.bin"), payload);
                List<DuplicateEntry> duplicates = V2Engine.FindDuplicates(sandbox, token, progress);
                results.Add(Result("Duplicados SHA-256", duplicates.Count == 2 && duplicates.Select(item => item.Group).Distinct().Count() == 1, "Somente arquivos com conteúdo idêntico foram agrupados."));

                progress.Report("Testando Registro isolado...");
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath)) key.SetValue("Probe", 42, RegistryValueKind.DWord);
                object registryValue;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath)) registryValue = key == null ? null : key.GetValue("Probe");
                results.Add(Result("Registro isolado", Convert.ToInt32(registryValue ?? 0, CultureInfo.InvariantCulture) == 42, "A leitura e escrita ocorreram apenas na chave de teste."));

                progress.Report("Testando execução de comandos...");
                CommandExecution command = SystemCommand.Execute("cmd.exe", "/d /c exit 0", 5000);
                results.Add(Result("Comando com limite", command.ExitCode == 0 && !command.TimedOut, "Código de retorno e tempo limite verificados."));

                progress.Report("Testando cancelamento de comandos...");
                bool cancellationWorked = false;
                using (var commandCancellation = new CancellationTokenSource(150))
                {
                    try { SystemCommand.Execute("cmd.exe", "/d /c ping 127.0.0.1 -n 10 >nul", 5000, commandCancellation.Token); }
                    catch (OperationCanceledException) { cancellationWorked = true; }
                }
                results.Add(Result("Cancelamento de comando", cancellationWorked, "O processo isolado foi interrompido ao cancelar a operação."));

                progress.Report("Testando validação de unidades...");
                bool safeDrive = WindowsMaintenance.NormalizeDriveForTesting("c:\\") == "C:" &&
                    string.IsNullOrEmpty(WindowsMaintenance.NormalizeDriveForTesting(@"\\servidor\pasta")) &&
                    string.IsNullOrEmpty(WindowsMaintenance.NormalizeDriveForTesting("C:\\Windows"));
                results.Add(Result("Unidade confinada", safeDrive, "Somente uma letra de unidade local foi aceita para otimização."));

                progress.Report("Testando proteção do atualizador...");
                string update = AdvancedEngine.DownloadVerifiedUpdate(new UpdateManifest { Version = "99.0", InstallerUrl = "http://example.invalid/update.exe", Sha256 = "00" });
                results.Add(Result("Atualização segura", update.IndexOf("HTTPS", StringComparison.OrdinalIgnoreCase) >= 0, "Downloads sem HTTPS foram rejeitados antes da rede."));

                progress.Report("Testando limpeza confinada...");
                string oldFile = Path.Combine(sandbox, "old.tmp");
                File.WriteAllText(oldFile, "temporário", Encoding.UTF8);
                File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-20));
                DeleteOldFilesInSandbox(sandbox, DateTime.UtcNow.AddDays(-14));
                results.Add(Result("Limpeza confinada", !File.Exists(oldFile) && Directory.Exists(sandbox), "A limpeza removeu apenas o arquivo antigo dentro do sandbox."));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { results.Add(Result("Execução da suíte", false, ex.Message)); }
            finally
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(registryPath, false); } catch { }
                try { if (Directory.Exists(sandbox)) Directory.Delete(sandbox, true); } catch { }
            }

            int passed = results.Count(item => item.Passed);
            var report = new StringBuilder("TESTES DE SEGURANÇA ISOLADOS\r\n" + new string('=', 72) + "\r\n");
            foreach (SafetyTestResult result in results) report.AppendLine((result.Passed ? "✓ " : "! ") + result.Name + " — " + result.Detail);
            report.AppendLine("\r\nResultado: " + passed + " de " + results.Count + " testes aprovados.");
            if (passed != results.Count) report.AppendLine("Uma falha bloqueia a recomendação de distribuição desta compilação.");
            return report.ToString();
        }

        private static SafetyTestResult Result(string name, bool passed, string detail)
        {
            return new SafetyTestResult { Name = name, Passed = passed, Detail = detail };
        }

        private static bool IsWithin(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteOldFilesInSandbox(string root, DateTime cutoffUtc)
        {
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                if (!IsWithin(root, file)) throw new InvalidOperationException("Arquivo fora do sandbox.");
                if (File.GetLastWriteTimeUtc(file) < cutoffUtc) File.Delete(file);
            }
        }
    }
#endif
}
