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
        private TabPage BuildRepairsTab()
        {
            TabPage page = NewPage("Correções");
            _repairSummary = new Label { Text = "Análise ainda não executada", AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11f) };
            Button scan = ButtonFactory("Procurar problemas", 0, 0, 180, Theme.Primary);
            _repairRunButton = ButtonFactory("Executar selecionadas", 0, 0, 190, Theme.Warning);
            _repairRunButton.Visible = false;
            var actions = new ResponsiveActionBar();
            actions.AddAction(scan);
            actions.AddAction(_repairRunButton);

            _repairGrid = Grid(0, 0, 1000, 500);
            _repairGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Fazer", Width = 58 });
            _repairGrid.Columns.Add("Area", "Área");
            _repairGrid.Columns[1].Width = 125;
            _repairGrid.Columns.Add("Title", "Verificação ou correção");
            _repairGrid.Columns[2].Width = 230;
            _repairGrid.Columns.Add("Status", "Status");
            _repairGrid.Columns[3].Width = 105;
            _repairGrid.Columns.Add("Detail", "Detalhes");
            _repairGrid.Columns[4].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            var actionColumn = new DataGridViewButtonColumn { Name = "Action", HeaderText = "Ação", Width = 105, FlatStyle = FlatStyle.Flat, UseColumnTextForButtonValue = false };
            _repairGrid.Columns.Add(actionColumn);
            for (int i = 1; i < 5; i++) _repairGrid.Columns[i].ReadOnly = true;
            _repairGrid.CurrentCellDirtyStateChanged += delegate { if (_repairGrid.IsCurrentCellDirty) _repairGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _repairGrid.CellValueChanged += delegate { UpdateRepairSelection(); };
            _repairGrid.CellContentClick += async delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || _repairGrid.Columns[e.ColumnIndex].Name != "Action") return;
                RepairFinding finding = _repairGrid.Rows[e.RowIndex].Tag as RepairFinding;
                if (finding != null) await ExecuteRepairActionAsync(finding);
            };

            Panel host = new Panel { BackColor = Theme.SurfaceDark };
            _repairGrid.Dock = DockStyle.Fill;
            _repairEmpty = new EmptyStatePanel { Dock = DockStyle.Fill };
            _repairEmpty.SetMessage("Pronto para analisar", "Verifique rede, navegadores e configurações essenciais em uma única etapa.");
            host.Controls.Add(_repairGrid);
            host.Controls.Add(_repairEmpty);
            _repairEmpty.BringToFront();
            scan.Click += async delegate { await LoadRepairsAsync(true); };
            _repairRunButton.Click += async delegate { await ExecuteSelectedRepairsAsync(); };
            page.Controls.Add(_repairSummary);
            page.Controls.Add(host);
            page.Controls.Add(actions);
            page.Resize += delegate
            {
                int width = Math.Max(700, page.ClientSize.Width - 40);
                _repairSummary.SetBounds(20, 18, width, 28);
                int actionHeight = page.ClientSize.Width < 900 ? 84 : 46;
                actions.SetBounds(20, Math.Max(410, page.ClientSize.Height - actionHeight - 12), width, actionHeight);
                host.SetBounds(20, 56, width, Math.Max(300, actions.Top - 68));
            };
            return page;
        }

        private async Task LoadRepairsAsync(bool force)
        {
            if (_repairsLoaded && !force) return;
            List<RepairFinding> results = null;
            _repairSummary.Text = "Verificando rede, navegadores e Windows...";
            await RunWork("Procurando problemas gerais...", delegate(CancellationToken token, IProgress<string> progress)
            {
                results = GeneralRepairEngine.Scan(token, progress);
                return BuildRepairReport(results);
            }, false);
            if (results == null) { _repairSummary.Text = "Não foi possível concluir a análise"; return; }
            _repairFindings = results;
            _repairsLoaded = true;
            PopulateRepairFindings();
        }

        private void PopulateRepairFindings()
        {
            _repairGrid.Rows.Clear();
            foreach (RepairFinding item in _repairFindings)
            {
                int index = _repairGrid.Rows.Add(item.Selected, item.Area, item.Title, item.Status, item.Detail, item.ActionLabel);
                DataGridViewRow row = _repairGrid.Rows[index];
                row.Tag = item;
                row.Cells["Selected"].ReadOnly = !item.CanRepair;
                if (item.Warning) row.DefaultCellStyle.ForeColor = Theme.Warning;
                else if (item.Status == "OK") row.Cells["Status"].Style.ForeColor = Theme.Success;
            }
            int warnings = _repairFindings.Count(item => item.Warning);
            _repairSummary.Text = warnings == 0 ? _repairFindings.Count + " verificações • nenhuma correção necessária" : warnings + (warnings == 1 ? " ponto para revisar" : " pontos para revisar");
            _repairGrid.Visible = _repairGrid.Rows.Count > 0;
            _repairEmpty.Visible = !_repairGrid.Visible;
            UpdateRepairSelection();
        }

        private void UpdateRepairSelection()
        {
            if (_repairGrid == null || _repairRunButton == null) return;
            int count = 0;
            foreach (DataGridViewRow row in _repairGrid.Rows) if (Convert.ToBoolean(row.Cells["Selected"].Value ?? false)) count++;
            _repairRunButton.Visible = count > 0;
            _repairRunButton.Text = count == 1 ? "Executar 1 correção" : "Executar " + count + " correções";
        }

        private async Task ExecuteRepairActionAsync(RepairFinding finding)
        {
            if (finding.Id == "proxy-settings") { OpenSystemUri("ms-settings:network-proxy"); return; }
            if (finding.Id == "open-temp") { try { Process.Start("explorer.exe", Environment.GetEnvironmentVariable("TEMP")); } catch { } return; }
            if (finding.Id == "open-services") { try { Process.Start("services.msc"); } catch { } return; }
            if (finding.Id == "integrity") { NavigateToSystem(1); return; }
            if (!finding.CanRepair) return;
            foreach (DataGridViewRow row in _repairGrid.Rows) row.Cells["Selected"].Value = ReferenceEquals(row.Tag, finding);
            await ExecuteSelectedRepairsAsync();
        }

        private async Task ExecuteSelectedRepairsAsync()
        {
            var selected = new List<RepairFinding>();
            foreach (DataGridViewRow row in _repairGrid.Rows)
            {
                RepairFinding item = row.Tag as RepairFinding;
                if (item != null && item.CanRepair && Convert.ToBoolean(row.Cells["Selected"].Value ?? false)) selected.Add(item);
            }
            if (selected.Count == 0) return;
            if (selected.Any(item => item.RequiresAdministrator) && !Optimizer.IsAdministrator())
            {
                _repairSummary.Text = "A redefinição do Winsock exige administrador; execute o aplicativo elevado para incluí-la.";
                return;
            }
            string names = string.Join("\r\n", selected.Select(item => "• " + item.Title));
            if (MessageBox.Show(this, "Executar as correções selecionadas?\r\n\r\n" + names, "Correções gerais", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string[] ids = selected.Select(item => item.Id).ToArray();
            await RunWork("Executando correções...", delegate(CancellationToken token, IProgress<string> progress) { return GeneralRepairEngine.Execute(ids, token, progress); });
            _repairsLoaded = false;
            await LoadRepairsAsync(true);
        }

        private static string BuildRepairReport(IEnumerable<RepairFinding> items)
        {
            return string.Join(Environment.NewLine, (items ?? Enumerable.Empty<RepairFinding>()).Select(item => item.Area + " | " + item.Title + " | " + item.Status + " | " + item.Detail));
        }

        private static void OpenSystemUri(string uri)
        {
            try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch { }
        }
    }
}
