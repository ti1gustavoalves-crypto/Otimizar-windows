using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2
    {
        private TabPage BuildIntegrityTab()
        {
            TabPage page = NewPage("Integridade");
            _integritySummary = new Label { Text = "Verificação ainda não executada", AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11f) };
            Button quick = ButtonFactory("Verificar agora", 0, 0, 155, Theme.Primary);
            Button deep = ButtonFactory("Verificação profunda", 0, 0, 190, Theme.Secondary);
            _integrityRepairButton = ButtonFactory("Reparar Windows", 0, 0, 165, Theme.Warning);
            _integrityRepairButton.Visible = false;
            var actions = new ResponsiveActionBar();
            actions.AddAction(quick);
            actions.AddAction(deep);
            actions.AddAction(_integrityRepairButton);

            _integrityGrid = Grid(0, 0, 1000, 500);
            _integrityGrid.Columns.Add("Area", "Área");
            _integrityGrid.Columns[0].Width = 135;
            _integrityGrid.Columns.Add("Check", "Verificação");
            _integrityGrid.Columns[1].Width = 230;
            _integrityGrid.Columns.Add("Status", "Status");
            _integrityGrid.Columns[2].Width = 105;
            _integrityGrid.Columns.Add("Detail", "Detalhes");
            _integrityGrid.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _integrityGrid.ReadOnly = true;
            _integrityGrid.CellToolTipTextNeeded += delegate(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
            {
                if (e.RowIndex >= 0) e.ToolTipText = Convert.ToString(_integrityGrid.Rows[e.RowIndex].Cells["Detail"].Value);
            };

            Panel host = new Panel { BackColor = Theme.SurfaceDark };
            _integrityGrid.Dock = DockStyle.Fill;
            _integrityEmpty = new EmptyStatePanel { Dock = DockStyle.Fill };
            _integrityEmpty.SetMessage("Verificação ainda não executada", "A análise rápida verifica estabilidade, espaço, dispositivos e serviços essenciais.");
            host.Controls.Add(_integrityGrid);
            host.Controls.Add(_integrityEmpty);
            _integrityEmpty.BringToFront();

            quick.Click += async delegate { await LoadIntegrityAsync(true); };
            deep.Click += async delegate { await RunDeepIntegrityAsync(false); };
            _integrityRepairButton.Click += async delegate { await RepairIntegrityAsync(); };
            page.Controls.Add(_integritySummary);
            page.Controls.Add(host);
            page.Controls.Add(actions);
            page.Resize += delegate { LayoutIntegrityTab(page, host, actions); };
            LayoutIntegrityTab(page, host, actions);
            return page;
        }

        private void LayoutIntegrityTab(TabPage page, Panel host, ResponsiveActionBar actions)
        {
            int margin = 20;
            int width = Math.Max(700, page.ClientSize.Width - (margin * 2));
            _integritySummary.SetBounds(margin, 18, width, 28);
            int actionsHeight = page.ClientSize.Width < 900 ? 84 : 46;
            actions.SetBounds(margin, Math.Max(410, page.ClientSize.Height - actionsHeight - 12), width, actionsHeight);
            host.SetBounds(margin, 56, width, Math.Max(300, actions.Top - 68));
        }

        private async Task LoadIntegrityAsync(bool force)
        {
            if (_integrityLoaded && !force) return;
            List<IntegrityCheckResult> results = null;
            _integritySummary.Text = "Verificando estabilidade, dispositivos e serviços...";
            await RunWork("Verificando integridade do sistema...", delegate(CancellationToken token, IProgress<string> progress)
            {
                progress.Report("Executando verificações rápidas...");
                token.ThrowIfCancellationRequested();
                results = SystemIntegrityEngine.QuickScan();
                return SystemIntegrityEngine.BuildReport(results);
            }, false);
            if (results == null)
            {
                _integritySummary.Text = "Não foi possível concluir a verificação";
                _integrityEmpty.SetMessage("Verificação não concluída", "Tente novamente ou consulte os Arquivos técnicos em Ajustes.");
                return;
            }
            _integrityResults = results;
            _integrityLoaded = true;
            PopulateIntegrityResults();
        }

        private async Task RunDeepIntegrityAsync(bool alreadyConfirmed)
        {
            if (!Optimizer.IsAdministrator())
            {
                if (MessageBox.Show(this, "A verificação profunda usa DISM, SFC e CHKDSK em modo de diagnóstico. Reabrir como administrador?", "Integridade do sistema", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    RequestIntegrityElevation();
                return;
            }
            if (!alreadyConfirmed && MessageBox.Show(this, "Executar a verificação profunda?\r\n\r\nDISM, SFC e CHKDSK serão usados somente para diagnóstico. Nenhum reparo será aplicado nesta etapa.", "Integridade do sistema", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;

            List<IntegrityCheckResult> results = null;
            await RunWork("Iniciando verificação profunda...", delegate(CancellationToken token, IProgress<string> progress)
            {
                results = SystemIntegrityEngine.QuickScan();
                results.AddRange(SystemIntegrityEngine.DeepScan(token, progress));
                return SystemIntegrityEngine.BuildReport(results);
            });
            if (results == null) return;
            _integrityResults = results;
            _integrityLoaded = true;
            PopulateIntegrityResults();
        }

        private async Task RepairIntegrityAsync()
        {
            if (!_integrityResults.Any(item => item.CanRepair)) return;
            if (!Optimizer.IsAdministrator()) { RequestIntegrityElevation(); return; }
            string warning = "Reparar componentes e arquivos do Windows?\r\n\r\nSerá tentado um ponto de restauração e depois serão executados DISM RestoreHealth e SFC ScanNow. O processo pode demorar e não deve ser interrompido.";
            if (MessageBox.Show(this, warning, "Reparar integridade", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            string result = await RunWork("Reparando a integridade do Windows...", delegate(CancellationToken token, IProgress<string> progress) { return SystemIntegrityEngine.RepairWindows(token, progress); });
            _integritySummary.Text = result.IndexOf("não concluído", StringComparison.OrdinalIgnoreCase) >= 0 ? "Reparo concluído com ressalvas • verifique novamente" : "Reparo concluído • verificando novamente...";
            _integrityLoaded = false;
            await RunDeepIntegrityAsync(true);
        }

        private void PopulateIntegrityResults()
        {
            _integrityGrid.Rows.Clear();
            foreach (IntegrityCheckResult item in _integrityResults)
            {
                int index = _integrityGrid.Rows.Add(item.Area, item.Check, item.Status, item.Detail);
                DataGridViewRow row = _integrityGrid.Rows[index];
                if (item.Warning) row.DefaultCellStyle.ForeColor = Theme.Warning;
                else if (item.Status == "OK") row.Cells["Status"].Style.ForeColor = Theme.Success;
            }
            int warnings = _integrityResults.Count(item => item.Warning);
            _integritySummary.Text = warnings == 0 ? _integrityResults.Count + " verificações • nenhum problema importante" : _integrityResults.Count + " verificações • " + warnings + (warnings == 1 ? " ponto de atenção" : " pontos de atenção");
            bool hasRows = _integrityGrid.Rows.Count > 0;
            _integrityGrid.Visible = hasRows;
            _integrityEmpty.Visible = !hasRows;
            _integrityRepairButton.Visible = _integrityResults.Any(item => item.CanRepair);
        }

        private void RequestIntegrityElevation()
        {
            string arguments = "--wait-for-instance --integrity-scan" + (AppPaths.IsPortable ? " --portable" : string.Empty);
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, arguments) { UseShellExecute = true, Verb = "runas" });
                Close();
            }
            catch (Exception ex) { _integritySummary.Text = "Permissão não concedida: " + ex.Message; }
        }
    }
}
