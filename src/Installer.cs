using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Instalador do Otimizador 3.1")]
[assembly: AssemblyDescription("Instala o Otimizador de Desempenho e Tema 3.1 para o usuário atual.")]
[assembly: AssemblyCompany("Codex")]
[assembly: AssemblyProduct("Otimizador de Desempenho e Tema")]
[assembly: AssemblyVersion("3.1.0.0")]
[assembly: AssemblyFileVersion("3.1.0.0")]

namespace CodexPerformanceOptimizerInstaller
{
    internal static class InstallerProgram
    {
        private static readonly string InstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "OtimizadorDeDesempenho");
        private static readonly string AppPath = Path.Combine(InstallDir, "OtimizadorDeDesempenho.exe");
        private static readonly string RollbackPath = Path.Combine(InstallDir, "OtimizadorDeDesempenho.rollback.exe");
        private static readonly string UninstallPath = Path.Combine(InstallDir, "Desinstalar.exe");
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CodexPerformanceOptimizer";

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                StartUninstallWorker();
                return;
            }
            if (args.Length > 1 && string.Equals(args[0], "--uninstall-worker", StringComparison.OrdinalIgnoreCase))
            {
                UninstallWorker(args[1]);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }

        private static void StartUninstallWorker()
        {
            string temp = Path.Combine(Path.GetTempPath(), "DesinstalarOtimizador-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Application.ExecutablePath, temp, true);
            using (Process process = Process.Start(new ProcessStartInfo(temp, "--uninstall-worker \"" + InstallDir + "\"") { UseShellExecute = true })) { }
        }

        private static void UninstallWorker(string requestedPath)
        {
            Thread.Sleep(800);
            try
            {
                string expected = Path.GetFullPath(InstallDir).TrimEnd(Path.DirectorySeparatorChar);
                string actual = Path.GetFullPath(requestedPath).TrimEnd(Path.DirectorySeparatorChar);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("O caminho não corresponde à instalação esperada.");
                if (Directory.Exists(actual) && (new DirectoryInfo(actual).Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("A pasta de instalação é um redirecionamento e não pode ser removida automaticamente.");

                RunHidden("schtasks.exe", "/Delete /TN \"Codex Otimizador - Manutencao\" /F");
                using (RegistryKey runOnce = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\RunOnce", true))
                    if (runOnce != null) runOnce.DeleteValue("CodexPerformanceOptimizerBenchmark", false);
                DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Otimizador de Desempenho 2.0.lnk"));
                DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Otimizador de Desempenho 2.0.lnk"));
                DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Otimizador de Desempenho 3.0.lnk"));
                DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Otimizador de Desempenho 3.0.lnk"));
                DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Otimizador de Desempenho 3.1.lnk"));
                DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Otimizador de Desempenho 3.1.lnk"));
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
                if (Directory.Exists(actual)) Directory.Delete(actual, true);
                MessageBox.Show("Otimizador removido. Os relatórios e o backup foram preservados em AppData\\Local\\Codex.", "Desinstalação concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível concluir a desinstalação: " + ex.Message, "Falha", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal static void Install(bool desktopShortcut)
        {
            Directory.CreateDirectory(InstallDir);
            string stagedApp = AppPath + ".new";
            try
            {
                ExtractRequiredResource("OptimizerBinary", stagedApp);
                Version stagedVersion = AssemblyName.GetAssemblyName(stagedApp).Version;
                if (stagedVersion == null || stagedVersion.Major < 3) throw new InvalidOperationException("A versão incorporada não é válida.");
                if (File.Exists(AppPath))
                {
                    try { File.Replace(stagedApp, AppPath, RollbackPath, true); }
                    catch
                    {
                        File.Copy(AppPath, RollbackPath, true);
                        File.Copy(stagedApp, AppPath, true);
                        File.Delete(stagedApp);
                    }
                }
                else File.Move(stagedApp, AppPath);
                if (AssemblyName.GetAssemblyName(AppPath).Version != stagedVersion) throw new InvalidOperationException("A verificação da versão instalada falhou.");
                ExtractOptionalResourceAtomic("ReleaseNotes", Path.Combine(InstallDir, "release-notes.txt"));
                ExtractOptionalResourceAtomic("UpdateManifest", Path.Combine(InstallDir, "update-manifest.json"));
                ExtractOptionalResourceAtomic("ReleaseChannel", Path.Combine(InstallDir, "release-channel.json"));
                File.Copy(Application.ExecutablePath, UninstallPath, true);
            }
            catch
            {
                try { if (File.Exists(stagedApp)) File.Delete(stagedApp); } catch { }
                RestoreRollback();
                throw;
            }
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Otimizador de Desempenho 2.0.lnk"));
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Otimizador de Desempenho 2.0.lnk"));
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Otimizador de Desempenho 3.0.lnk"));
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Otimizador de Desempenho 3.0.lnk"));
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Otimizador de Desempenho 3.1.lnk"), AppPath);
            if (desktopShortcut) CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Otimizador de Desempenho 3.1.lnk"), AppPath);
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                key.SetValue("DisplayName", "Otimizador de Desempenho e Tema 3.1");
                key.SetValue("DisplayVersion", "3.1.0");
                key.SetValue("Publisher", "Codex");
                key.SetValue("InstallLocation", InstallDir);
                key.SetValue("DisplayIcon", AppPath);
                key.SetValue("UninstallString", "\"" + UninstallPath + "\" --uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        internal static void LaunchInstalled()
        {
            try { Process.Start(new ProcessStartInfo(AppPath) { UseShellExecute = true }); }
            catch
            {
                RestoreRollback();
                throw;
            }
        }

        internal static bool EnsureApplicationClosed(IWin32Window owner)
        {
            List<Process> running = RunningInstalledApplication();
            if (running.Count == 0) return true;
            if (MessageBox.Show(owner, "O Otimizador está aberto. Fechá-lo para continuar a atualização ou reparação?", "Programa em execução", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;
            foreach (Process process in running)
            {
                try { process.CloseMainWindow(); } catch { }
            }
            foreach (Process process in running)
            {
                try { process.WaitForExit(5000); } catch { }
                finally { process.Dispose(); }
            }
            List<Process> remaining = RunningInstalledApplication();
            if (remaining.Count == 0) return true;
            foreach (Process process in remaining) process.Dispose();
            MessageBox.Show(owner, "O programa ainda está em execução. Feche-o manualmente e tente novamente. Nenhum processo foi encerrado à força.", "Atualização pausada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        internal static Version InstalledVersion()
        {
            try { return File.Exists(AppPath) ? AssemblyName.GetAssemblyName(AppPath).Version : null; }
            catch { return null; }
        }

        private static List<Process> RunningInstalledApplication()
        {
            var result = new List<Process>();
            foreach (Process process in Process.GetProcessesByName("OtimizadorDeDesempenho"))
            {
                try
                {
                    string path = process.MainModule == null ? string.Empty : process.MainModule.FileName;
                    if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(AppPath), StringComparison.OrdinalIgnoreCase)) result.Add(process);
                    else process.Dispose();
                }
                catch { process.Dispose(); }
            }
            return result;
        }

        private static void RestoreRollback()
        {
            try { if (File.Exists(RollbackPath)) File.Copy(RollbackPath, AppPath, true); } catch { }
        }

        private static void ExtractRequiredResource(string resourceName, string path)
        {
            using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (source == null) throw new InvalidOperationException("O recurso " + resourceName + " não foi encontrado.");
                using (FileStream target = File.Create(path)) source.CopyTo(target);
            }
        }

        private static void ExtractOptionalResourceAtomic(string resourceName, string path)
        {
            string staged = path + ".new";
            using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (source == null) return;
                using (FileStream target = File.Create(staged)) source.CopyTo(target);
            }
            if (File.Exists(path)) File.Replace(staged, path, null, true);
            else File.Move(staged, path);
        }

        private static void CreateShortcut(string shortcutPath, string targetPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
            Type type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) throw new InvalidOperationException("O mecanismo de atalhos do Windows não está disponível.");
            object shell = null;
            object shortcut = null;
            try
            {
                shell = Activator.CreateInstance(type);
                shortcut = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetPath) });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Otimizador de Desempenho e Tema 3.1" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
                if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
            }
        }

        private static void DeleteShortcut(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void RunHidden(string file, string args)
        {
            try
            {
                using (Process process = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true }))
                    if (process != null) process.WaitForExit(15000);
            }
            catch { }
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly CheckBox _desktop;
        private readonly Button _install;
        private readonly Label _status;

        public InstallerForm()
        {
            Text = "Instalar Otimizador 3.1";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(620, 360);
            BackColor = Color.FromArgb(28, 30, 34);
            ForeColor = Color.WhiteSmoke;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AccessibleName = "Instalador do Otimizador 3.1";
            Controls.Add(new Label { Text = "Otimizador 3.1", Font = new Font("Segoe UI Semibold", 22f), AutoSize = true, Location = new Point(30, 26) });
            Version installed = InstallerProgram.InstalledVersion();
            Version package = Assembly.GetExecutingAssembly().GetName().Version;
            string operation = installed == null ? "Instalação por usuário — não requer privilégios administrativos" : installed == package ? "Reparar a versão " + installed + " sem perder configurações" : "Atualizar da versão " + installed + " para " + package;
            Controls.Add(new Label { Text = operation, AutoSize = true, Location = new Point(34, 74), ForeColor = Color.Gainsboro });
            Controls.Add(new Label { Text = "Será instalado em:\r\n" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "OtimizadorDeDesempenho") + "\r\n\r\nO instalador adiciona um atalho no Menu Iniciar e uma entrada de desinstalação. Backups e relatórios ficam separados em AppData\\Local\\Codex.", Location = new Point(34, 115), Size = new Size(550, 110), ForeColor = Color.Gainsboro });
            _desktop = new CheckBox { Text = "Criar atalho na Área de Trabalho", AutoSize = true, Checked = true, Location = new Point(34, 236) };
            _install = new Button { Text = "Instalar", Location = new Point(34, 280), Size = new Size(150, 42), BackColor = Color.FromArgb(0, 120, 212), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            if (installed != null) _install.Text = installed == package ? "Reparar" : "Atualizar";
            _install.FlatAppearance.BorderSize = 0;
            _status = new Label { Text = "Pronto", AutoSize = true, Location = new Point(204, 293), ForeColor = Color.Gainsboro };
            _install.Click += InstallClicked;
            Controls.Add(_desktop);
            Controls.Add(_install);
            Controls.Add(_status);
        }

        private void InstallClicked(object sender, EventArgs e)
        {
            if (!InstallerProgram.EnsureApplicationClosed(this)) return;
            _install.Enabled = false;
            UseWaitCursor = true;
            _status.Text = _install.Text + "...";
            try
            {
                InstallerProgram.Install(_desktop.Checked);
                _status.Text = "Operação concluída";
                if (MessageBox.Show(this, "Operação concluída. Abrir o Otimizador agora?", "Concluído", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) InstallerProgram.LaunchInstalled();
                Close();
            }
            catch (Exception ex)
            {
                _status.Text = "Falha";
                MessageBox.Show(this, "Não foi possível instalar: " + ex.Message, "Falha", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _install.Enabled = true;
            }
            finally { UseWaitCursor = false; }
        }
    }
}
