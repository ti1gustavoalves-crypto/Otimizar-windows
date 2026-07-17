using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Otimizador de Desempenho")]
[assembly: AssemblyDescription("Aplica otimizações seguras de desempenho, modo escuro e limpeza controlada no Windows.")]
[assembly: AssemblyCompany("Codex")]
[assembly: AssemblyProduct("Otimizador de Desempenho")]
[assembly: AssemblyCopyright("2026")]
[assembly: AssemblyVersion("4.1.0.0")]
[assembly: AssemblyFileVersion("4.1.0.0")]
[assembly: ComVisible(false)]

namespace CodexPerformanceOptimizer
{
    internal static class Program
    {
        private const string SingleInstanceName = @"Local\CodexPerformanceOptimizer";

        [STAThread]
        private static void Main(string[] args)
        {
            CrashLogger.Initialize();
            if (HasArgument(args, "--maintenance"))
            {
                V2Engine.RunMaintenance();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool firstInstance;
            using (var instance = new Mutex(true, SingleInstanceName, out firstInstance))
            {
                bool ownsInstance = firstInstance;
                if (!ownsInstance && HasArgument(args, "--wait-for-instance"))
                {
                    try { ownsInstance = instance.WaitOne(TimeSpan.FromSeconds(15)); }
                    catch (AbandonedMutexException) { ownsInstance = true; }
                }
                if (!ownsInstance)
                {
                    MessageBox.Show("O Otimizador já está aberto.", "Otimizador de Desempenho", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try { Application.Run(new MainFormV2()); }
                finally { instance.ReleaseMutex(); }
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            return args.Length > 0 && string.Equals(args[0], expected, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class CleanupForm : Form
    {
        private readonly CheckBox _recycle;
        private readonly CheckBox _oldWindows;

        public bool EmptyRecycleBin { get { return _recycle.Checked; } }
        public bool RemoveWindowsOld { get { return _oldWindows.Checked; } }

        public CleanupForm()
        {
            Text = "Limpeza avançada";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(530, 230);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);

            Controls.Add(new Label
            {
                Text = "Estas ações são irreversíveis. Selecione somente o que deseja excluir:",
                AutoSize = true,
                Location = new Point(22, 20)
            });
            _recycle = new CheckBox
            {
                Text = "Esvaziar a Lixeira (arquivos não poderão ser restaurados)",
                AutoSize = true,
                Location = new Point(25, 65),
                FlatStyle = FlatStyle.Flat
            };
            _oldWindows = new CheckBox
            {
                Text = "Remover Windows.old (elimina a reversão da atualização do Windows)",
                AutoSize = true,
                Location = new Point(25, 100),
                FlatStyle = FlatStyle.Flat
            };
            var confirm = DialogButton("Continuar", DialogResult.OK, 320, Theme.Warning);
            var cancel = DialogButton("Cancelar", DialogResult.Cancel, 420, Theme.Secondary);
            cancel.Size = new Size(85, 34);
            Controls.Add(_recycle);
            Controls.Add(_oldWindows);
            Controls.Add(confirm);
            Controls.Add(cancel);
            AcceptButton = confirm;
            CancelButton = cancel;
        }

        private static Button DialogButton(string text, DialogResult result, int x, Color color)
        {
            var button = new Button
            {
                Text = text,
                DialogResult = result,
                Location = new Point(x, 165),
                Size = new Size(90, 34),
                BackColor = color,
                ForeColor = Theme.ButtonText,
                FlatStyle = FlatStyle.Flat
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }

    internal static class Optimizer
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string rootPath, uint flags);

        public static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static string AdvancedCleanup(bool recycle, bool removeWindowsOld)
        {
            var log = new StringBuilder("LIMPEZA AVANÇADA\r\n" + new string('=', 72) + "\r\n");
            if (recycle)
            {
                const uint noConfirmation = 0x00000001;
                const uint noProgress = 0x00000002;
                const uint noSound = 0x00000004;
                uint result = SHEmptyRecycleBin(IntPtr.Zero, null, noConfirmation | noProgress | noSound);
                log.AppendLine(result == 0 ? "✓ Lixeira esvaziada." : "! A Lixeira não foi totalmente esvaziada. Código: " + result);
            }

            if (removeWindowsOld)
            {
                if (!IsAdministrator())
                    log.AppendLine("! Windows.old exige privilégios administrativos. Reabra como administrador.");
                else if (!Directory.Exists(@"C:\Windows.old"))
                    log.AppendLine("✓ Windows.old não existe.");
                else
                {
                    string result = RunCommand("cleanmgr.exe", "/AUTOCLEAN /D C:", 120000);
                    log.AppendLine(Directory.Exists(@"C:\Windows.old")
                        ? "! A limpeza foi solicitada, mas Windows.old ainda existe. Use Armazenamento > Arquivos temporários."
                        : "✓ Windows.old removido.");
                    if (!string.IsNullOrWhiteSpace(result)) log.AppendLine(result);
                }
            }

            log.AppendLine();
            log.Append(V2Engine.BuildFullAudit());
            return log.ToString();
        }

        private static string RunCommand(string fileName, string arguments, int timeoutMilliseconds)
        {
            CommandExecution result = SystemCommand.Execute(fileName, arguments, timeoutMilliseconds);
            return result.ExitCode == 0 ? string.Empty : result.Output;
        }
    }
}
