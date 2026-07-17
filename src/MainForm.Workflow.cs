using System;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2
    {
        private TabPage BuildUpdatesTab()
        {
            var page = NewPage("Atualizações");
            var driversButton = ButtonFactory("Windows e drivers", 20, 12, 170, Theme.Primary);
            var programsButton = ButtonFactory("Aplicativos", 202, 12, 135, Theme.Secondary);
            var innerTabs = new TabControl
            {
                Location = new Point(-4, 26),
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(1, 24),
                Appearance = TabAppearance.FlatButtons
            };
            innerTabs.TabPages.Add(BuildDriversTab());
            innerTabs.TabPages.Add(BuildProgramUpdatesTab());
            Action updateSelection = delegate
            {
                bool drivers = innerTabs.SelectedIndex == 0;
                driversButton.BackColor = drivers ? Theme.Primary : Theme.Secondary;
                programsButton.BackColor = drivers ? Theme.Secondary : Theme.Primary;
            };
            driversButton.Click += delegate { innerTabs.SelectedIndex = 0; };
            programsButton.Click += delegate { innerTabs.SelectedIndex = 1; };
            innerTabs.SelectedIndexChanged += async delegate
            {
                updateSelection();
                if (innerTabs.SelectedIndex == 0) await LoadDriverInventoryAsync(false);
                else if (!_programUpdatesLoaded) await SearchProgramUpdates();
            };
            page.Controls.Add(innerTabs);
            page.Controls.Add(driversButton);
            page.Controls.Add(programsButton);
            page.Resize += delegate { innerTabs.Size = new Size(page.ClientSize.Width + 8, page.ClientSize.Height - 22); };
            innerTabs.Size = new Size(page.ClientSize.Width + 8, page.ClientSize.Height - 22);
            return page;
        }

        private async System.Threading.Tasks.Task RunEnergyDiagnostic()
        {
            string result = await RunWork("Gerando diagnóstico de energia...", delegate(CancellationToken t, IProgress<string> p) { return WindowsMaintenance.GenerateEnergyReport(t, p); });
            if (!string.IsNullOrWhiteSpace(WindowsMaintenance.LatestEnergyReportPath) && result.IndexOf("relatório criado", StringComparison.OrdinalIgnoreCase) >= 0 &&
                MessageBox.Show(this, "Relatório criado. Abrir agora?", "Diagnóstico de energia", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                WindowsMaintenance.OpenLatestEnergyReport();
        }

        private string BuildTechnicalServiceReport()
        {
            var report = new StringBuilder();
            report.AppendLine("RELATÓRIO TÉCNICO DO ATENDIMENTO");
            report.AppendLine(new string('=', 72));
            report.AppendLine("Gerado em: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            report.AppendLine("Otimizador: " + GetType().Assembly.GetName().Version);
            report.AppendLine("Administrador: " + (Optimizer.IsAdministrator() ? "sim" : "não"));
            if (_liveMetrics != null)
            {
                report.AppendLine();
                report.AppendLine("RECURSOS");
                report.AppendLine("CPU: " + _liveMetrics.CpuUsagePercent.ToString("N0") + "%");
                report.AppendLine("Memória livre: " + _liveMetrics.FreeRamGb.ToString("N1") + " de " + _liveMetrics.TotalRamGb.ToString("N1") + " GB");
                report.AppendLine("Disco C: " + _liveMetrics.FreeDiskGb.ToString("N1") + " de " + _liveMetrics.TotalDiskGb.ToString("N1") + " GB livres");
                report.AppendLine("Energia: " + _liveMetrics.PowerScheme);
            }
            if (_diagnosticSnapshot != null)
            {
                report.AppendLine();
                report.AppendLine(DetailedDiagnosticText(_diagnosticSnapshot));
            }
            if (_importantHardware != null && _importantHardware.Count > 0)
            {
                report.AppendLine();
                report.AppendLine(V2Engine.ImportantHardwareReport(_importantHardware, V2Engine.BuildPerformanceRecommendations()));
            }
            report.AppendLine();
            report.AppendLine("ATUALIZAÇÕES");
            report.AppendLine("Drivers disponíveis: " + (_driverUpdates == null ? 0 : _driverUpdates.Count));
            report.AppendLine("Aplicativos disponíveis: " + (_programUpdates == null ? 0 : _programUpdates.Count));
            report.AppendLine("Os relatórios detalhados de cada operação permanecem na pasta de relatórios do aplicativo.");
            return report.ToString();
        }

        private void ShowTechnicalServiceReport()
        {
            ShowTextDialog("Relatório técnico", BuildTechnicalServiceReport());
        }
    }
}
