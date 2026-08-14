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
            var refresh = ButtonFactory("↻", 954, 34, 42, Theme.Secondary);
            _toolTip.SetToolTip(refresh, "Atualizar diagnóstico");
            refresh.Click += async delegate { await RefreshAudit(); };
            health.Controls.Add(_overviewStatus);
            health.Controls.Add(_overviewNote);
            health.Controls.Add(_environmentBadge);
            health.Controls.Add(_liveAlert);
            health.Controls.Add(refresh);

            var memory = MetricCard("Memória disponível", 20, 130, out _memoryValue, out _memoryDetail, out _memoryGauge, out _memoryChart);
            var disk = MetricCard("Espaço no disco C:", 356, 130, out _diskValue, out _diskDetail, out _diskGauge, out _diskChart);
            var cpu = MetricCard("Uso do processador", 692, 130, out _cpuValue, out _cpuDetail, out _cpuGauge, out _cpuChart);

            var service = DashboardCard(20, 260, 1016, 422);
            service.Controls.Add(new Label { Text = "Fila de atendimento", Location = new Point(20, 16), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 12f) });
            service.Controls.Add(new Label { Text = "Escolha o objetivo e revise as ações que serão executadas em ordem.", Location = new Point(20, 44), Size = new Size(590, 22), AutoEllipsis = true, ForeColor = Theme.Muted });

            _serviceProfile = new ComboBox { Location = new Point(20, 78), Size = new Size(270, 29), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text };
            _serviceProfile.Items.AddRange(new object[] { "Manutenção preventiva", "PC lento", "Pouco espaço", "Inicialização lenta", "Atendimento completo" });
            _serviceProfile.SelectedIndex = _initialServiceProfile.HasValue ? Math.Max(0, Math.Min(4, _initialServiceProfile.Value)) : 0;
            _serviceProfile.SelectedIndexChanged += async delegate { await RefreshMaintenancePlanAsync(); };

            _planSummary = new Label { Text = "Preparando o plano...", Location = new Point(20, 124), Size = new Size(270, 66), AutoEllipsis = true, ForeColor = Theme.Muted };
            string comparison = ComparisonSummary();
            _comparisonToggle = ButtonFactory("Último resultado  ▾", 20, 200, 170, Theme.Secondary);
            _comparisonToggle.Visible = !string.IsNullOrEmpty(comparison);
            _comparisonSummary = new Label { Text = comparison, Location = new Point(20, 240), Size = new Size(270, 74), AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 9f), Visible = false };
            _comparisonToggle.Click += delegate
            {
                _comparisonSummary.Visible = !_comparisonSummary.Visible;
                _comparisonToggle.Text = _comparisonSummary.Visible ? "Último resultado  ▴" : "Último resultado  ▾";
            };
            _issueGrid = Grid(316, 78, 680, 278);
            _issueGrid.RowTemplate.Height = 30;
            _issueGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Executar", Width = 70 });
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
                if (issue.Id == "drivers" || issue.Id == "programs") NavigateTo(AppSection.Updates);
                else if (issue.Id == "restart") NavigateToSystem(0);
            };

            _runPlanButton = ButtonFactory("Executar atendimento", 756, 369, 240, Theme.Primary);
            _runPlanButton.Click += async delegate { await ExecuteCompleteTechnicalServiceAsync(false); };
            _fullServiceButton = _runPlanButton;

            service.Controls.Add(_serviceProfile);
            service.Controls.Add(_planSummary);
            service.Controls.Add(_comparisonToggle);
            service.Controls.Add(_comparisonSummary);
            service.Controls.Add(_issueGrid);
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
            page.Resize += delegate { LayoutGuidedDashboard(page, health, memory, disk, cpu, service, refresh); };
            LayoutGuidedDashboard(page, health, memory, disk, cpu, service, refresh);
            return page;
        }

        private void LayoutGuidedDashboard(TabPage page, DashboardPanel health, DashboardPanel memory, DashboardPanel disk, DashboardPanel cpu, DashboardPanel service, Button refresh)
        {
            int available = Math.Max(780, Math.Min(1160, page.ClientSize.Width - 40));
            int left = Math.Max(20, (page.ClientSize.Width - available) / 2);
            health.Location = new Point(left, 18);
            health.Size = new Size(available, 98);
            int rightZone = Math.Max(430, available / 2);
            int rightLeft = available - rightZone + 20;
            _overviewStatus.Size = new Size(Math.Max(280, rightLeft - 38), 32);
            _overviewNote.Size = new Size(Math.Max(280, rightLeft - 36), 20);
            refresh.Location = new Point(available - 62, 34);
            _environmentBadge.Location = new Point(rightLeft, 34);
            _environmentBadge.Size = new Size(Math.Max(230, rightZone - 82), 28);
            _liveAlert.Location = new Point(rightLeft, 67);
            _liveAlert.Size = new Size(Math.Max(230, rightZone - 40), 20);

            int gap = 12;
            int metricWidth = (available - (gap * 2)) / 3;
            ResizeMetricCard(memory, metricWidth);
            ResizeMetricCard(disk, metricWidth);
            ResizeMetricCard(cpu, metricWidth);
            LayoutMetricText(_memoryValue, _memoryDetail, metricWidth);
            LayoutMetricText(_diskValue, _diskDetail, metricWidth);
            LayoutMetricText(_cpuValue, _cpuDetail, metricWidth);
            memory.Location = new Point(left, 130);
            disk.Location = new Point(left + metricWidth + gap, 130);
            cpu.Location = new Point(left + (metricWidth + gap) * 2, 130);

            service.Location = new Point(left, 260);
            service.Size = new Size(available, 422);
            _issueGrid.Location = new Point(316, 78);
            _issueGrid.Size = new Size(Math.Max(430, available - 336), 278);
            _runPlanButton.Location = new Point(available - 260, 369);
            _runPlanButton.Size = new Size(240, 38);
        }

        private static void ResizeMetricCard(DashboardPanel card, int width)
        {
            card.Width = width;
            foreach (Control control in card.Controls)
            {
                var chart = control as SparklineChart;
                var gauge = control as ModernProgressBar;
                if (chart != null || gauge != null) control.Width = Math.Max(120, width - 36);
            }
        }

        private static void LayoutMetricText(Label value, Label detail, int width)
        {
            int detailWidth = Math.Min(155, Math.Max(105, width / 2));
            detail.Location = new Point(width - detailWidth - 17, 25);
            detail.Size = new Size(detailWidth, 38);
            value.Size = new Size(Math.Max(90, detail.Left - 28), 30);
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

        private void UpdatePlanSummary()
        {
            if (_maintenancePlan == null || _planSummary == null) return;
            int selected = _maintenancePlan.SelectedCount;
            _planSummary.Text = selected == 0 ? "Nenhuma ação selecionada." : selected + (selected == 1 ? " ação pronta" : " ações prontas") + " para " + MaintenanceWorkflow.ProfileName(_maintenancePlan.Profile).ToLowerInvariant() + ".";
            _runPlanButton.Enabled = selected > 0 && !_fullServiceRunning;
            SetButtonColor(_runPlanButton, selected > 0 ? Theme.Primary : Theme.Secondary);
            _runPlanButton.Text = _fullServiceRunning ? "Atendimento em andamento..." : selected == 0 ? "Nenhuma ação disponível" : "Executar atendimento";
        }

        private async Task ExecuteCompleteTechnicalServiceAsync(bool alreadyConfirmed)
        {
            if (_fullServiceRunning || _maintenancePlan == null) return;
            SyncPlanSelection();
            if (_maintenancePlan.RequiresAdministrator && !Optimizer.IsAdministrator())
            {
                RequestPlanElevation(true);
                return;
            }
            if (!alreadyConfirmed)
            {
                string message = "Executar o atendimento?\r\n\r\nO fluxo registrará o estado inicial, aplicará as ações selecionadas, verificará problemas gerais, consultará atualizações e medirá o resultado. Atualizações não serão instaladas sem confirmação.";
                if (MessageBox.Show(this, message, "Atendimento técnico completo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            }

            _fullServiceRunning = true;
            UpdatePlanSummary();
            TechnicalServiceResult service = null;
            string operation = await RunWork("Iniciando atendimento técnico...", delegate(CancellationToken token, IProgress<string> progress)
            {
                service = TechnicalServiceWorkflow.Execute(_maintenancePlan, _lastProcessActivities, token, progress);
                return service.Report;
            });
            _fullServiceRunning = false;
            UpdatePlanSummary();
            if (service == null)
            {
                _planSummary.Text = FirstResultLine(operation, "Atendimento não concluído");
                return;
            }

            PopulateDriverUpdates(service.DriverUpdates);
            string wingetVersion = await Task.Run(delegate { return ProgramUpdater.ReadVersion(); });
            PopulateProgramUpdates(service.ProgramUpdates, wingetVersion);
            _repairFindings = service.RepairFindings ?? new List<RepairFinding>();
            _repairsLoaded = true;
            if (_repairGrid != null) PopulateRepairFindings();
            _liveMetrics = V2Engine.ReadMetrics();
            _diagnosticSnapshot = CachedAnalysis.ReadDiagnostics(false);
            _diagnosticsLoaded = true;
            UpdateMetricCards(_liveMetrics);
            PopulateDiagnostics(_diagnosticSnapshot);
            await RefreshMaintenancePlanAsync();
            _comparisonSummary.Text = ComparisonSummary();
            _comparisonToggle.Visible = !string.IsNullOrEmpty(_comparisonSummary.Text);
            _comparisonSummary.Visible = _comparisonToggle.Visible;
            _comparisonToggle.Text = "Último resultado  ▴";
            _planSummary.Text = "Atendimento concluído • saúde " + service.BeforeHealth.Score + " → " + service.AfterHealth.Score + "/100";

            int pending = service.DriverUpdates.Count + service.ProgramUpdates.Count;
            string summary = "Atendimento concluído.\r\n\r\nSaúde: " + service.BeforeHealth.Score + " → " + service.AfterHealth.Score + "/100\r\nCausa provável: " + service.Cause.Title + "\r\nAtualizações para revisar: " + pending;
            if (pending > 0 && MessageBox.Show(this, summary + "\r\n\r\nAbrir a área de Atualizações?", "Resultado do atendimento", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                NavigateTo(AppSection.Updates);
            else if (pending == 0) MessageBox.Show(this, summary, "Resultado do atendimento", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RequestPlanElevation(bool fullService = false)
        {
            if (Optimizer.IsAdministrator()) return;
            int profile = _serviceProfile == null ? 0 : _serviceProfile.SelectedIndex;
            if (MessageBox.Show(this, "As ações selecionadas precisam de administrador. Reabrir uma única vez com o plano preservado?", "Permissão administrativa", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string arguments = "--wait-for-instance --guided " + profile + (fullService ? " --full-service" : string.Empty) + (AppPaths.IsPortable ? " --portable" : string.Empty);
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
            if (comparison == null) return string.Empty;
            string boot = comparison.BootDurationMilliseconds > 0 ? "  •  Inicialização " + TimeSpan.FromMilliseconds(comparison.BootDurationMilliseconds).TotalSeconds.ToString("N1", CultureInfo.CurrentCulture) + " s" : string.Empty;
            return string.Format(CultureInfo.CurrentCulture, "ÚLTIMO RESULTADO\r\n{0}\r\nRAM {1:N1} → {2:N1} GB  •  CPU {3:N0}% → {4:N0}%\r\nDisco {5:N1} → {6:N1} GB{7}", comparison.Operation, comparison.BeforeFreeRamGb, comparison.AfterFreeRamGb, comparison.BeforeCpuPercent, comparison.AfterCpuPercent, comparison.BeforeFreeDiskGb, comparison.AfterFreeDiskGb, boot);
        }
    }
}
