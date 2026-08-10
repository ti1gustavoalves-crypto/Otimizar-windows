using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2
    {
        private TabPage BuildGuidedDashboard()
        {
            var page = NewPage("Atendimento");
            var health = DashboardCard(20, 18, 1016, 98);
            health.Controls.Add(new Label { Text = "ESTADO DO COMPUTADOR", Location = new Point(20, 13), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8.5f) });
            _overviewStatus = new Label { Text = "Analisando...", Location = new Point(18, 34), Size = new Size(470, 32), AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 18f) };
            _overviewNote = new Label { Text = "Lendo os indicadores principais", Location = new Point(20, 68), Size = new Size(490, 20), AutoEllipsis = true, ForeColor = Theme.Muted };
            _environmentBadge = new Label { Text = AppPaths.ModeDescription, Location = new Point(520, 34), Size = new Size(300, 28), BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text, Padding = new Padding(9, 5, 9, 4), AutoEllipsis = true };
            _liveAlert = new Label { Text = "Monitorando em tempo real", Location = new Point(520, 67), Size = new Size(300, 20), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, ForeColor = Theme.Success, Font = new Font("Segoe UI Semibold", 8.5f) };
            var refresh = ButtonFactory("Atualizar diagnóstico", 826, 34, 170, Theme.Secondary);
            refresh.Click += async delegate { await RefreshAudit(); };
            health.Controls.Add(_overviewStatus);
            health.Controls.Add(_overviewNote);
            health.Controls.Add(_environmentBadge);
            health.Controls.Add(_liveAlert);
            health.Controls.Add(refresh);

            var memory = MetricCard("Memória disponível", 20, 130, out _memoryValue, out _memoryDetail, out _memoryGauge, out _memoryChart);
            var disk = MetricCard("Espaço no disco C:", 356, 130, out _diskValue, out _diskDetail, out _diskGauge, out _diskChart);
            var cpu = MetricCard("Uso do processador", 692, 130, out _cpuValue, out _cpuDetail, out _cpuGauge, out _cpuChart);

            var service = DashboardCard(20, 260, 1016, 372);
            service.Controls.Add(new Label { Text = "Atendimento rápido", Location = new Point(20, 16), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 12f) });
            service.Controls.Add(new Label { Text = "Escolha o objetivo; o Otimizador prepara somente ações coerentes com o caso.", Location = new Point(20, 44), Size = new Size(590, 22), AutoEllipsis = true, ForeColor = Theme.Muted });

            _serviceProfile = new ComboBox { Location = new Point(20, 78), Size = new Size(270, 29), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text };
            _serviceProfile.Items.AddRange(new object[] { "Manutenção preventiva", "PC lento", "Pouco espaço", "Inicialização lenta" });
            _serviceProfile.SelectedIndex = _initialServiceProfile.HasValue ? Math.Max(0, Math.Min(3, _initialServiceProfile.Value)) : 0;
            _serviceProfile.SelectedIndexChanged += async delegate { await RefreshMaintenancePlanAsync(); };

            _planSummary = new Label { Text = "Preparando o plano...", Location = new Point(20, 124), Size = new Size(270, 66), AutoEllipsis = true, ForeColor = Theme.Muted };
            _comparisonSummary = new Label { Text = ComparisonSummary(), Location = new Point(20, 204), Size = new Size(270, 90), AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 9f) };
            _elevatePlanButton = ButtonFactory("Reabrir como administrador", 20, 305, 270, Theme.Secondary);
            _elevatePlanButton.Visible = false;
            _elevatePlanButton.Click += delegate { RequestPlanElevation(); };

            _issueGrid = Grid(316, 78, 680, 228);
            _issueGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Fazer", Width = 55 });
            _issueGrid.Columns.Add("Severity", "Prioridade");
            _issueGrid.Columns[1].Width = 110;
            _issueGrid.Columns[1].ReadOnly = true;
            _issueGrid.Columns.Add("Title", "Pendência ou ação");
            _issueGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _issueGrid.Columns[2].ReadOnly = true;
            _issueGrid.Columns.Add("Category", "Área");
            _issueGrid.Columns[3].Width = 120;
            _issueGrid.Columns[3].ReadOnly = true;
            _issueGrid.Columns.Add("Detail", "Detalhes");
            _issueGrid.Columns[4].Visible = false;
            _issueGrid.Columns[4].ReadOnly = true;
            _issueGrid.CurrentCellDirtyStateChanged += delegate { if (_issueGrid.IsCurrentCellDirty) _issueGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _issueGrid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e) { if (!_planLoading && e.RowIndex >= 0 && _issueGrid.Columns[e.ColumnIndex].Name == "Selected") SyncPlanSelection(); };
            _issueGrid.CellToolTipTextNeeded += delegate(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
            {
                if (e.RowIndex >= 0) e.ToolTipText = Convert.ToString(_issueGrid.Rows[e.RowIndex].Cells["Detail"].Value);
            };
            _issueGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0) return;
                MaintenanceIssue issue = _issueGrid.Rows[e.RowIndex].Tag as MaintenanceIssue;
                if (issue == null) return;
                if (issue.Id == "drivers" || issue.Id == "programs") _tabs.SelectedIndex = (int)AppSection.Updates;
                else if (issue.Id == "restart") _tabs.SelectedIndex = (int)AppSection.Diagnostics;
            };

            _runPlanButton = ButtonFactory("Executar ações selecionadas", 776, 319, 220, Theme.Success);
            _runPlanButton.Click += async delegate { await ExecuteMaintenancePlanAsync(); };
            var selectSafe = ButtonFactory("Selecionar recomendadas", 566, 319, 198, Theme.Secondary);
            selectSafe.Click += delegate { SelectRecommendedPlanItems(); };

            service.Controls.Add(_serviceProfile);
            service.Controls.Add(_planSummary);
            service.Controls.Add(_comparisonSummary);
            service.Controls.Add(_elevatePlanButton);
            service.Controls.Add(_issueGrid);
            service.Controls.Add(selectSafe);
            service.Controls.Add(_runPlanButton);

            _processCards = new DashboardPanel[0];
            _processNames = new Label[0];
            _processStats = new Label[0];
            _processTags = new Label[0];
            page.Controls.Add(health);
            page.Controls.Add(memory);
            page.Controls.Add(disk);
            page.Controls.Add(cpu);
            page.Controls.Add(service);
            page.Resize += delegate
            {
                int left = Math.Max(20, (page.ClientSize.Width - 1016) / 2);
                health.Left = left;
                memory.Left = left;
                disk.Left = left + 336;
                cpu.Left = left + 672;
                service.Left = left;
            };
            return page;
        }

        private async Task RefreshMaintenancePlanAsync()
        {
            if (_issueGrid == null || _planLoading) return;
            _planLoading = true;
            try
            {
                int selectedProfile = _serviceProfile == null ? 0 : _serviceProfile.SelectedIndex;
                SystemMetrics metrics = _liveMetrics ?? V2Engine.ReadMetrics();
                DiagnosticSnapshot diagnostics = _diagnosticSnapshot;
                List<StartupEntry> startup = await Task.Run(delegate { return V2Engine.ReadStartupEntries(); });
                _maintenancePlan = MaintenanceWorkflow.BuildPlan((ServiceProfile)Math.Max(0, selectedProfile), metrics, diagnostics, startup, _driverUpdates == null ? 0 : _driverUpdates.Count, _programUpdates == null ? 0 : _programUpdates.Count);
                PopulateMaintenancePlan();
            }
            finally { _planLoading = false; }
        }

        private void PopulateMaintenancePlan()
        {
            if (_issueGrid == null || _maintenancePlan == null) return;
            _issueGrid.Rows.Clear();
            foreach (MaintenanceIssue issue in _maintenancePlan.Issues)
            {
                int index = _issueGrid.Rows.Add(issue.Selected, issue.Severity, issue.Title, issue.Category, issue.Detail);
                DataGridViewRow row = _issueGrid.Rows[index];
                row.Tag = issue;
                row.Cells["Selected"].ReadOnly = !issue.CanFix;
                Color priority = issue.Severity == "Crítico" ? Theme.Danger : issue.Severity == "Atenção" ? Theme.Warning : issue.Severity == "Proteção" ? Theme.Success : Theme.Text;
                row.Cells["Severity"].Style.ForeColor = priority;
                if (!issue.CanFix) row.DefaultCellStyle.ForeColor = Theme.Muted;
            }
            UpdatePlanSummary();
        }

        private void SyncPlanSelection()
        {
            if (_maintenancePlan == null || _issueGrid == null) return;
            foreach (DataGridViewRow row in _issueGrid.Rows)
            {
                MaintenanceIssue issue = row.Tag as MaintenanceIssue;
                if (issue != null && issue.CanFix) issue.Selected = Convert.ToBoolean(row.Cells["Selected"].Value ?? false);
            }
            UpdatePlanSummary();
        }

        private void SelectRecommendedPlanItems()
        {
            if (_issueGrid == null) return;
            _planLoading = true;
            foreach (DataGridViewRow row in _issueGrid.Rows)
            {
                MaintenanceIssue issue = row.Tag as MaintenanceIssue;
                if (issue != null && issue.CanFix) row.Cells["Selected"].Value = !string.Equals(issue.Severity, "Opcional", StringComparison.OrdinalIgnoreCase);
            }
            _planLoading = false;
            SyncPlanSelection();
        }

        private void UpdatePlanSummary()
        {
            if (_maintenancePlan == null || _planSummary == null) return;
            int selected = _maintenancePlan.SelectedCount;
            _planSummary.Text = selected == 0 ? "Nenhuma ação selecionada." : selected + (selected == 1 ? " ação pronta" : " ações prontas") + " para " + MaintenanceWorkflow.ProfileName(_maintenancePlan.Profile).ToLowerInvariant() + ".";
            bool needsElevation = _maintenancePlan.RequiresAdministrator && !Optimizer.IsAdministrator();
            _elevatePlanButton.Visible = needsElevation;
            _runPlanButton.Enabled = selected > 0 && !needsElevation;
            SetButtonColor(_runPlanButton, needsElevation ? Theme.Secondary : Theme.Success);
            _runPlanButton.Text = needsElevation ? "Requer administrador" : selected == 0 ? "Selecione uma ação" : "Executar " + selected + (selected == 1 ? " ação" : " ações");
        }

        private async Task ExecuteMaintenancePlanAsync()
        {
            SyncPlanSelection();
            if (_maintenancePlan == null || _maintenancePlan.SelectedCount == 0) return;
            if (_maintenancePlan.RequiresAdministrator && !Optimizer.IsAdministrator())
            {
                RequestPlanElevation();
                return;
            }
            string confirmation = "Executar " + _maintenancePlan.SelectedCount + (_maintenancePlan.SelectedCount == 1 ? " ação" : " ações") + " do perfil " + MaintenanceWorkflow.ProfileName(_maintenancePlan.Profile) + "?\r\n\r\nO relatório antes/depois será salvo automaticamente.";
            if (MessageBox.Show(this, confirmation, "Confirmar manutenção", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string result = await RunWork("Executando manutenção guiada...", delegate(CancellationToken token, IProgress<string> progress) { return MaintenanceWorkflow.Execute(_maintenancePlan, token, progress); });
            _planSummary.Text = FirstResultLine(result, "Manutenção concluída");
            await RefreshAudit();
            _comparisonSummary.Text = ComparisonSummary();
        }

        private void RequestPlanElevation()
        {
            if (Optimizer.IsAdministrator()) return;
            int profile = _serviceProfile == null ? 0 : _serviceProfile.SelectedIndex;
            if (MessageBox.Show(this, "As ações selecionadas precisam de administrador. Reabrir uma única vez com o plano preservado?", "Permissão administrativa", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string arguments = "--wait-for-instance --guided " + profile + (AppPaths.IsPortable ? " --portable" : string.Empty);
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, arguments) { UseShellExecute = true, Verb = "runas" });
                Close();
            }
            catch (Exception ex) { _planSummary.Text = "Elevação cancelada: " + ex.Message; }
        }

        private static string ComparisonSummary()
        {
            PerformanceComparison comparison = AdvancedEngine.ReadComparison();
            if (comparison == null) return "ANTES E DEPOIS\r\nExecute uma manutenção para criar a primeira comparação.";
            string boot = comparison.BootDurationMilliseconds > 0 ? "  •  Inicialização " + TimeSpan.FromMilliseconds(comparison.BootDurationMilliseconds).TotalSeconds.ToString("N1", CultureInfo.CurrentCulture) + " s" : string.Empty;
            return string.Format(CultureInfo.CurrentCulture, "ÚLTIMO RESULTADO\r\n{0}\r\nRAM {1:N1} → {2:N1} GB  •  CPU {3:N0}% → {4:N0}%\r\nDisco {5:N1} → {6:N1} GB{7}", comparison.Operation, comparison.BeforeFreeRamGb, comparison.AfterFreeRamGb, comparison.BeforeCpuPercent, comparison.AfterCpuPercent, comparison.BeforeFreeDiskGb, comparison.AfterFreeDiskGb, boot);
        }
    }
}
