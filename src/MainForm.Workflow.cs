using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2
    {
        private TabPage BuildUpdatesTab()
        {
            var page = NewPage("Atualizações");
            page.AutoScroll = true;
            var driversButton = ButtonFactory("Drivers", 20, 12, 120, Theme.Primary);
            var programsButton = ButtonFactory("Aplicativos", 152, 12, 135, Theme.Secondary);
            var content = new Panel { Location = new Point(0, 58), BackColor = Theme.Background, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            Panel drivers = BuildDriversPanel();
            Panel programs = BuildProgramUpdatesPanel();
            drivers.Dock = DockStyle.Fill;
            programs.Dock = DockStyle.Fill;
            programs.Visible = false;
            content.Controls.Add(programs);
            content.Controls.Add(drivers);
            Action updateSelection = delegate
            {
                bool driversVisible = drivers.Visible;
                SetButtonColor(driversButton, driversVisible ? Theme.Primary : Theme.Secondary);
                SetButtonColor(programsButton, driversVisible ? Theme.Secondary : Theme.Primary);
            };
            driversButton.Click += delegate
            {
                programs.Visible = false;
                drivers.Visible = true;
                drivers.BringToFront();
                updateSelection();
            };
            programsButton.Click += async delegate
            {
                drivers.Visible = false;
                programs.Visible = true;
                programs.BringToFront();
                updateSelection();
                if (!_programUpdatesLoaded && !_suppressStartup) await SearchProgramUpdates();
            };
            page.Controls.Add(content);
            page.Controls.Add(driversButton);
            page.Controls.Add(programsButton);
            page.Resize += delegate { content.Size = new Size(page.ClientSize.Width, Math.Max(300, page.ClientSize.Height - content.Top)); };
            content.Size = new Size(page.ClientSize.Width, Math.Max(300, page.ClientSize.Height - content.Top));
            return page;
        }

        private async System.Threading.Tasks.Task RunEnergyDiagnostic()
        {
            string result = await RunWork("Gerando diagnóstico de energia...", delegate(CancellationToken t, IProgress<string> p) { return WindowsMaintenance.GenerateEnergyReport(t, p); });
            if (!string.IsNullOrWhiteSpace(WindowsMaintenance.LatestEnergyReportPath) && result.IndexOf("relatório criado", StringComparison.OrdinalIgnoreCase) >= 0 &&
                MessageBox.Show(this, "Relatório criado. Abrir agora?", "Diagnóstico de energia", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                WindowsMaintenance.OpenLatestEnergyReport();
        }

    }
}
