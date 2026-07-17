using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2 : Form
    {
        private TabControl _tabs;
        private ComboBox _profile;
        private CheckBox _dark;
        private CheckBox _visuals;
        private CheckBox _startup;
        private CheckBox _cleanup;
        private CheckBox _restorePoint;
        private CheckBox _backgroundEfficiency;
        private Label _overviewStatus;
        private Label _overviewNote;
        private Label _environmentBadge;
        private Label _profileDescription;
        private Label _memoryValue;
        private Label _memoryDetail;
        private Label _diskValue;
        private Label _diskDetail;
        private Label _cpuValue;
        private Label _cpuDetail;
        private ModernProgressBar _memoryGauge;
        private ModernProgressBar _diskGauge;
        private ModernProgressBar _cpuGauge;
        private SparklineChart _memoryChart;
        private SparklineChart _diskChart;
        private SparklineChart _cpuChart;
        private Label _liveAlert;
        private DashboardPanel[] _processCards;
        private Label[] _processNames;
        private Label[] _processStats;
        private Label[] _processTags;
        private ProcessHistoryTracker _processHistory;
        private DataGridView _processHistoryGrid;
        private FlowLayoutPanel _diagnosticCards;
        private Label _diagnosticStatus;
        private DiagnosticSnapshot _diagnosticSnapshot;
        private bool _diagnosticsLoaded;
        private CheckBox _minimizeToTray;
        private CheckBox _automaticProfiles;
        private Label _updateStatus;
        private NotifyIcon _trayIcon;
        private AdvancedSettings _advancedSettings;
        private PowerLineStatus? _lastPowerLineStatus;
        private DataGridView _startupGrid;
        private DataGridView _storageGrid;
        private FlowLayoutPanel _hardwareCards;
        private Label _hardwareSummary;
        private List<ImportantHardware> _importantHardware;
        private bool _hardwareLoaded;
        private DataGridView _volumeGrid;
        private Label _folderSummary;
        private string _selectedDrive;
        private Label _storageSummary;
        private ComboBox _schedule;
        private FlowLayoutPanel _reportCards;
        private Label _reportEmpty;
        private string _selectedReportPath;
        private ProgressBar _progress;
        private Label _status;
        private Button _cancel;
        private CancellationTokenSource _cts;
        private System.Windows.Forms.Timer _liveMetricsTimer;
        private SystemActivitySampler _activitySampler;
        private ProcessActivitySampler _processSampler;
        private SustainedAlertMonitor _alertMonitor;
        private SystemMetrics _liveMetrics;
        private List<ProcessActivity> _lastProcessActivities = new List<ProcessActivity>();
        private int _historyWriteInProgress;
        private bool _managedEnvironment;
        private int _liveMetricTicks;

        public MainFormV2()
        {
            Text = "Otimizador de Desempenho e Tema 3.3";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 720);
            Size = new Size(1120, 780);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AccessibleName = "Otimizador de Desempenho e Tema 3.3";
            _advancedSettings = AdvancedEngine.ReadSettings();
            _processHistory = new ProcessHistoryTracker();

            _tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 6), DrawMode = TabDrawMode.OwnerDrawFixed, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(150, 36) };
            _tabs.DrawItem += DrawTab;
            _tabs.Resize += delegate { ResizeTabs(); };
            _tabs.TabPages.Add(BuildDashboard());
            _tabs.TabPages.Add(BuildHardwareTab());
            _tabs.TabPages.Add(BuildStartupTab());
            _tabs.TabPages.Add(BuildStorageTab());
            _tabs.TabPages.Add(BuildDiagnosticsTab());
            _tabs.TabPages.Add(BuildMaintenanceTab());
            _tabs.TabPages.Add(BuildControlTab());
            _tabs.TabPages.Add(BuildReportTab());

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Theme.Header };
            _progress = new ProgressBar { Location = new Point(20, 17), Size = new Size(155, 10), Style = ProgressBarStyle.Continuous, Visible = false };
            _status = new Label { Text = "Pronto", AutoSize = true, Location = new Point(20, 12), ForeColor = Theme.Muted };
            _cancel = ButtonFactory("Cancelar", 0, 0, 130, Theme.Secondary);
            _cancel.Size = new Size(100, 28);
            _cancel.Location = new Point(8, 8);
            _cancel.Enabled = false;
            _cancel.Click += delegate { if (_cts != null) _cts.Cancel(); };
            var cancelArea = new Panel { Dock = DockStyle.Right, Width = 116, BackColor = Theme.Header };
            cancelArea.Controls.Add(_cancel);
            footer.Controls.Add(_progress);
            footer.Controls.Add(_status);
            footer.Controls.Add(cancelArea);

            Controls.Add(_tabs);
            Controls.Add(footer);
            _activitySampler = new SystemActivitySampler();
            _processSampler = new ProcessActivitySampler();
            _alertMonitor = new SustainedAlertMonitor(TimeSpan.FromSeconds(20));
            ConfigureTrayIcon();
            _liveMetricsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _liveMetricsTimer.Tick += delegate { RefreshLiveMetrics(); };
            Shown += async delegate
            {
                _activitySampler.Prime();
                _processSampler.Prime();
                _liveMetricsTimer.Start();
                await RefreshAudit();
                await TryCompletePendingBenchmark();
                BeginAutomaticUpdateCheck();
            };
            FormClosed += delegate
            {
                _liveMetricsTimer.Stop();
                _liveMetricsTimer.Dispose();
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            };
        }

        private TabPage BuildDashboard()
        {
            var page = NewPage("Visão geral");
            var healthCard = DashboardCard(20, 18, 640, 142);
            healthCard.Controls.Add(new Label { Text = "ESTADO DO PC", Location = new Point(20, 15), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8.5f) });
            _overviewStatus = new Label { Text = "Analisando...", Location = new Point(18, 39), Size = new Size(590, 38), AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 20f) };
            _overviewNote = new Label { Text = "Lendo os indicadores principais", Location = new Point(21, 79), Size = new Size(590, 22), AutoEllipsis = true, ForeColor = Theme.Muted };
            _environmentBadge = new Label { Text = "Preparando ambiente", Location = new Point(20, 107), Size = new Size(374, 23), BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text, Padding = new Padding(8, 3, 8, 3), AutoEllipsis = true };
            var analyze = ButtonFactory("Analisar de novo", 464, 103, 154, Theme.Secondary);
            analyze.Size = new Size(154, 30);
            analyze.Click += async delegate { await RefreshAudit(); };
            healthCard.Controls.Add(_overviewStatus);
            healthCard.Controls.Add(_overviewNote);
            healthCard.Controls.Add(_environmentBadge);
            healthCard.Controls.Add(analyze);

            var profileCard = DashboardCard(676, 18, 360, 142);
            profileCard.Controls.Add(new Label { Text = "PERFIL DE DESEMPENHO", Location = new Point(20, 15), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8.5f) });
            _profile = new ComboBox { Location = new Point(20, 39), Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text };
            _profile.Items.AddRange(new object[] { "Máximo desempenho", "Equilibrado", "Notebook / eficiência" });
            _profile.SelectedIndex = 0;
            _profileDescription = new Label { Location = new Point(21, 70), Size = new Size(318, 19), AutoEllipsis = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) };
            _profile.SelectedIndexChanged += delegate { UpdateProfileDescription(); };
            var apply = ButtonFactory("Aplicar perfil", 20, 98, 320, Theme.Primary);
            apply.Size = new Size(320, 31);
            profileCard.Controls.Add(_profile);
            profileCard.Controls.Add(_profileDescription);
            profileCard.Controls.Add(apply);
            UpdateProfileDescription();

            var memoryCard = MetricCard("Memória disponível", 20, 176, out _memoryValue, out _memoryDetail, out _memoryGauge, out _memoryChart);
            var diskCard = MetricCard("Espaço no disco C:", 356, 176, out _diskValue, out _diskDetail, out _diskGauge, out _diskChart);
            var cpuCard = MetricCard("Uso do processador", 692, 176, out _cpuValue, out _cpuDetail, out _cpuGauge, out _cpuChart);

            var optionsCard = DashboardCard(20, 306, 640, 174);
            optionsCard.Controls.Add(new Label { Text = "Ajustes do perfil", Location = new Point(20, 16), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11.5f) });
            optionsCard.Controls.Add(new Label { Text = "Escolha o que será aplicado", Location = new Point(20, 41), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) });

            _dark = Option("Modo escuro", 20, 75, true);
            _visuals = Option("Reduzir animações", 20, 108, true);
            _restorePoint = Option("Ponto de restauração (admin)", 20, 141, true);
            _startup = Option("Otimizar inicialização", 326, 75, true);
            _cleanup = Option("Limpar temporários", 326, 108, true);
            _backgroundEfficiency = Option("Reduzir segundo plano", 326, 141, true);
            optionsCard.Controls.Add(_dark);
            optionsCard.Controls.Add(_visuals);
            optionsCard.Controls.Add(_restorePoint);
            optionsCard.Controls.Add(_startup);
            optionsCard.Controls.Add(_cleanup);
            optionsCard.Controls.Add(_backgroundEfficiency);

            var toolsCard = DashboardCard(676, 306, 360, 174);
            toolsCard.Controls.Add(new Label { Text = "Ferramentas", Location = new Point(20, 16), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11.5f) });
            var restore = ButtonFactory("Restaurar original", 20, 48, 152, Theme.Secondary);
            var export = ButtonFactory("Exportar backup", 188, 48, 152, Theme.Secondary);
            var admin = ButtonFactory("Executar como admin", 20, 101, 152, Theme.Secondary);
            var history = ButtonFactory("Ver histórico", 188, 101, 152, Theme.Secondary);
            restore.Size = export.Size = admin.Size = history.Size = new Size(152, 42);
            toolsCard.Controls.Add(restore);
            toolsCard.Controls.Add(export);
            toolsCard.Controls.Add(admin);
            toolsCard.Controls.Add(history);

            var activityCard = DashboardCard(20, 498, 1016, 142);
            activityCard.Controls.Add(new Label { Text = "PROCESSOS EM DESTAQUE", Location = new Point(18, 14), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8.5f) });
            _liveAlert = new Label { Text = "Monitorando em tempo real", Location = new Point(664, 12), Size = new Size(330, 23), TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true, ForeColor = Theme.Success, Font = new Font("Segoe UI Semibold", 8.5f) };
            activityCard.Controls.Add(_liveAlert);

            _processCards = new DashboardPanel[3];
            _processNames = new Label[3];
            _processStats = new Label[3];
            _processTags = new Label[3];
            for (int i = 0; i < 3; i++)
            {
                var processCard = DashboardCard(14 + (i * 332), 46, 324, 80);
                processCard.BackColor = Theme.SurfaceAlt;
                processCard.BorderColor = Color.Transparent;
                var processName = new Label { Text = "Calculando...", Location = new Point(14, 10), Size = new Size(190, 22), AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 10f) };
                var processTag = new Label { Text = "", Location = new Point(205, 10), Size = new Size(103, 20), TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8f) };
                var processStats = new Label { Text = "Aguardando amostra", Location = new Point(14, 42), Size = new Size(294, 22), AutoEllipsis = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.8f) };
                processCard.Controls.Add(processName);
                processCard.Controls.Add(processTag);
                processCard.Controls.Add(processStats);
                _processCards[i] = processCard;
                _processNames[i] = processName;
                _processTags[i] = processTag;
                _processStats[i] = processStats;
                activityCard.Controls.Add(processCard);
            }

            apply.Click += async delegate
            {
                var options = new ApplyOptions
                {
                    Profile = _profile.SelectedIndex,
                    DarkMode = _dark.Checked,
                    ReduceVisuals = _visuals.Checked,
                    OptimizeStartup = _startup.Checked,
                    CleanupTemp = _cleanup.Checked,
                    CreateRestorePoint = _restorePoint.Checked,
                    BackgroundEfficiency = _backgroundEfficiency.Checked
                };
                await RunWork("Aplicando perfil...", delegate(CancellationToken t, IProgress<string> p) { return V2Engine.Apply(options, t, p); });
                await RefreshAudit();
            };
            restore.Click += async delegate
            {
                if (MessageBox.Show(this, "Restaurar as configurações registradas antes da primeira otimização?", "Restaurar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    await RunWork("Restaurando...", delegate(CancellationToken t, IProgress<string> p) { return V2Engine.Restore(t, p); });
                    await RefreshAudit();
                }
            };
            export.Click += ExportBackup;
            admin.Click += RunAsAdmin;
            history.Click += delegate { _tabs.SelectedIndex = _tabs.TabCount - 1; };

            page.Controls.Add(healthCard);
            page.Controls.Add(profileCard);
            page.Controls.Add(memoryCard);
            page.Controls.Add(diskCard);
            page.Controls.Add(cpuCard);
            page.Controls.Add(optionsCard);
            page.Controls.Add(toolsCard);
            page.Controls.Add(activityCard);
            return page;
        }

        private TabPage BuildHardwareTab()
        {
            var page = NewPage("Hardware");
            _hardwareSummary = new Label { Text = "Componentes principais", AutoSize = false, AutoEllipsis = true, Size = new Size(730, 32), Location = new Point(20, 20), ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 10.5f) };
            var refresh = ButtonFactory("Atualizar", 770, 12, 110, Theme.Primary);
            var export = ButtonFactory("Exportar", 890, 12, 120, Theme.Secondary);
            refresh.Click += async delegate { await LoadHardware(true); };
            export.Click += ExportHardware;

            _hardwareCards = new FlowLayoutPanel
            {
                Location = new Point(20, 62),
                Size = new Size(1000, 525),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Theme.SurfaceDark,
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true,
                Padding = new Padding(10),
                WrapContents = true
            };

            page.Controls.Add(_hardwareSummary);
            page.Controls.Add(refresh);
            page.Controls.Add(export);
            page.Controls.Add(_hardwareCards);
            page.Enter += async delegate { if (!_hardwareLoaded && _cts == null) await LoadHardware(false); };
            return page;
        }

        private TabPage BuildStartupTab()
        {
            var page = NewPage("Inicialização");
            page.Controls.Add(new Label { Text = "Aplicativos que abrem com o Windows", AutoSize = true, Location = new Point(20, 18), ForeColor = Theme.Muted });
            _startupGrid = Grid(20, 48, 1000, 480);
            _startupGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "Ativo", Width = 65 });
            _startupGrid.Columns.Add("Name", "Programa");
            _startupGrid.Columns[1].Width = 230;
            _startupGrid.Columns[1].ReadOnly = true;
            _startupGrid.Columns.Add("Impact", "Impacto estimado");
            _startupGrid.Columns[2].Width = 145;
            _startupGrid.Columns[2].ReadOnly = true;
            _startupGrid.Columns.Add("Command", "Comando");
            _startupGrid.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _startupGrid.Columns[3].ReadOnly = true;
            _startupGrid.Columns.Add("Original", "Original");
            _startupGrid.Columns[4].Visible = false;
            _startupGrid.Columns[4].ReadOnly = true;
            var refresh = ButtonFactory("Atualizar lista", 20, 545, 150, Theme.Secondary);
            var save = ButtonFactory("Aplicar alterações", 182, 545, 170, Theme.Primary);
            refresh.Click += delegate { LoadStartup(); };
            save.Click += async delegate { await ApplyStartupGrid(); };
            page.Controls.Add(_startupGrid);
            page.Controls.Add(refresh);
            page.Controls.Add(save);
            _startupGrid.Anchor = AnchorStyles.None;
            page.Resize += delegate { LayoutStartupTab(page, refresh, save); };
            LayoutStartupTab(page, refresh, save);
            page.Enter += delegate { LoadStartup(); };
            return page;
        }

        private void LayoutStartupTab(TabPage page, Button refresh, Button save)
        {
            int width = Math.Max(500, page.ClientSize.Width - 40);
            int buttonY = Math.Max(260, page.ClientSize.Height - 50);
            _startupGrid.Location = new Point(20, 48);
            _startupGrid.Size = new Size(width, Math.Max(180, buttonY - _startupGrid.Top - 12));
            refresh.Location = new Point(20, buttonY);
            save.Location = new Point(182, buttonY);
        }

        private TabPage BuildStorageTab()
        {
            var page = NewPage("Armazenamento");
            _storageSummary = new Label { Text = "Discos e volumes", AutoSize = false, Size = new Size(520, 30), Location = new Point(20, 20), ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 10.5f) };
            var scan = ButtonFactory("Pastas", 430, 12, 84, Theme.Primary);
            var largeFiles = ButtonFactory("Grandes", 524, 12, 92, Theme.Secondary);
            var clean = ButtonFactory("Limpar", 626, 12, 84, Theme.Warning);
            var duplicates = ButtonFactory("Duplicados", 720, 12, 92, Theme.Secondary);
            var optimize = ButtonFactory("Otimizar", 822, 12, 92, Theme.Success);
            var export = ButtonFactory("Exportar", 924, 12, 80, Theme.Secondary);

            _volumeGrid = Grid(20, 58, 1000, 145);
            _volumeGrid.Columns.Add("Drive", "Disco");
            _volumeGrid.Columns[0].Width = 80;
            _volumeGrid.Columns.Add("Label", "Nome");
            _volumeGrid.Columns[1].Width = 260;
            _volumeGrid.Columns.Add("Used", "Usado");
            _volumeGrid.Columns[2].Width = 110;
            _volumeGrid.Columns.Add("Free", "Livre");
            _volumeGrid.Columns[3].Width = 110;
            _volumeGrid.Columns.Add("Total", "Total");
            _volumeGrid.Columns[4].Width = 110;
            _volumeGrid.Columns.Add("Usage", "Uso");
            _volumeGrid.Columns[5].Width = 80;
            _volumeGrid.Columns.Add("FileSystem", "Sistema");
            _volumeGrid.Columns[6].Width = 90;
            _volumeGrid.Columns.Add("Health", "Saúde");
            _volumeGrid.Columns[7].Width = 95;
            _volumeGrid.ReadOnly = true;
            _volumeGrid.SelectionChanged += delegate
            {
                if (_volumeGrid.SelectedRows.Count > 0) _selectedDrive = Convert.ToString(_volumeGrid.SelectedRows[0].Cells["Drive"].Value);
            };

            _folderSummary = new Label { Text = "Selecione um disco e clique em Analisar", AutoSize = true, Location = new Point(20, 219), ForeColor = Theme.Muted };
            _storageGrid = Grid(20, 246, 1000, 341);
            _storageGrid.Columns.Add("Path", "Pasta");
            _storageGrid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _storageGrid.Columns.Add("Logical", "Tamanho");
            _storageGrid.Columns[1].Width = 130;
            _storageGrid.Columns.Add("Allocated", "No disco");
            _storageGrid.Columns[2].Width = 130;
            _storageGrid.ReadOnly = true;

            scan.Click += async delegate { await ScanSelectedVolume(); };
            largeFiles.Click += async delegate { await ScanLargeFiles(); };
            clean.Click += async delegate { await OpenSafeCleanup(); };
            duplicates.Click += async delegate { await ScanDuplicates(); };
            optimize.Click += async delegate { await OptimizeSelectedVolume(); };
            export.Click += delegate { ExportGrid(_storageGrid, "armazenamento.csv"); };
            page.Controls.Add(_storageSummary);
            page.Controls.Add(_volumeGrid);
            page.Controls.Add(_folderSummary);
            page.Controls.Add(_storageGrid);
            page.Controls.Add(scan);
            page.Controls.Add(largeFiles);
            page.Controls.Add(clean);
            page.Controls.Add(duplicates);
            page.Controls.Add(optimize);
            page.Controls.Add(export);
            _volumeGrid.Anchor = AnchorStyles.None;
            _storageGrid.Anchor = AnchorStyles.None;
            page.Resize += delegate { LayoutStorageTab(page, scan, largeFiles, clean, duplicates, optimize, export); };
            LayoutStorageTab(page, scan, largeFiles, clean, duplicates, optimize, export);
            page.Enter += delegate { LoadVolumes(); };
            return page;
        }

        private void LayoutStorageTab(TabPage page, Button scan, Button largeFiles, Button clean, Button duplicates, Button optimize, Button export)
        {
            int width = Math.Max(600, page.ClientSize.Width - 40);
            _volumeGrid.Location = new Point(20, 58);
            _volumeGrid.Size = new Size(width, 145);
            _storageGrid.Location = new Point(20, 246);
            _storageGrid.Size = new Size(width, Math.Max(210, page.ClientSize.Height - _storageGrid.Top - 20));
            int actionsLeft = Math.Max(410, page.ClientSize.Width - 640);
            scan.Location = new Point(actionsLeft, 12);
            largeFiles.Location = new Point(actionsLeft + 94, 12);
            clean.Location = new Point(actionsLeft + 196, 12);
            duplicates.Location = new Point(actionsLeft + 290, 12);
            optimize.Location = new Point(actionsLeft + 392, 12);
            export.Location = new Point(actionsLeft + 494, 12);
            _storageSummary.Size = new Size(Math.Max(320, actionsLeft - 40), 30);
        }

        private TabPage BuildMaintenanceTab()
        {
            var page = NewPage("Manutenção");
            page.Controls.Add(new Label { Text = "Manutenção automática", AutoSize = true, Location = new Point(22, 22), ForeColor = Theme.Muted });
            page.Controls.Add(new Label { Text = "Frequência", AutoSize = true, Location = new Point(22, 78), ForeColor = Theme.Muted });
            _schedule = new ComboBox { Location = new Point(22, 102), Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
            _schedule.Items.AddRange(new object[] { "Desativada", "Semanal (segunda-feira)", "Mensal (dia 1)" });
            _schedule.SelectedIndex = V2Engine.ReadScheduleIndex();
            var configure = ButtonFactory("Salvar agendamento", 270, 98, 180, Theme.Primary);
            configure.Click += async delegate { await RunWork("Configurando agendamento...", delegate(CancellationToken t, IProgress<string> p) { return V2Engine.ConfigureSchedule(_schedule.SelectedIndex); }); };
            var run = ButtonFactory("Executar manutenção agora", 22, 165, 220, Theme.Secondary);
            run.Click += async delegate { await RunWork("Executando manutenção...", delegate(CancellationToken t, IProgress<string> p) { return V2Engine.MaintenanceReport(t, p); }); };
            var advanced = ButtonFactory("Limpeza avançada...", 254, 165, 190, Theme.Warning);
            advanced.Click += async delegate { await AdvancedCleanup(); };
            var components = ButtonFactory("Componentes do Windows", 456, 165, 200, Theme.Secondary);
            components.Click += async delegate
            {
                if (MessageBox.Show(this, "Remover componentes substituídos do Windows? O modo agressivo ResetBase não será usado.", "Componentes do Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await RunWork("Limpando componentes do Windows...", delegate(CancellationToken t, IProgress<string> p) { return WindowsMaintenance.CleanupComponentStore(t, p); });
            };
            var energy = ButtonFactory("Diagnóstico de energia", 668, 165, 200, Theme.Secondary);
            energy.Click += async delegate
            {
                string result = await RunWork("Gerando diagnóstico de energia...", delegate(CancellationToken t, IProgress<string> p) { return WindowsMaintenance.GenerateEnergyReport(t, p); });
                if (!string.IsNullOrWhiteSpace(WindowsMaintenance.LatestEnergyReportPath) && result.IndexOf("relatório criado", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    MessageBox.Show(this, "Relatório criado. Abrir agora?", "Diagnóstico de energia", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    WindowsMaintenance.OpenLatestEnergyReport();
            };
            var storageSense = ButtonFactory("Limpeza automática", 880, 165, 160, Theme.Secondary);
            storageSense.AccessibleName = "Abrir Sensor de Armazenamento";
            storageSense.Click += delegate { WindowsMaintenance.OpenStorageSenseSettings(); };
            var info = DashboardCard(22, 230, 1018, 220);
            info.Controls.Add(new Label { Text = "Protegido automaticamente", Location = new Point(20, 18), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11.5f) });
            info.Controls.Add(new Label { Text = "OneDrive e arquivos pessoais\r\nVeeam, Defender e Intune\r\nPolíticas corporativas", Location = new Point(22, 55), Size = new Size(330, 90), ForeColor = Theme.Text, Font = new Font("Segoe UI", 10f) });
            info.Controls.Add(new Label { Text = "Novas ações", Location = new Point(510, 18), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11.5f) });
            info.Controls.Add(new Label { Text = "Otimização adequada para SSD ou HDD\r\nLimpeza do WinSxS sem ResetBase\r\nRelatório energético de 15 segundos", Location = new Point(512, 55), Size = new Size(450, 90), ForeColor = Theme.Text, Font = new Font("Segoe UI", 10f) });
            info.Controls.Add(new Label { Text = "Lixeira, Windows.old e componentes do Windows sempre pedem confirmação.", Location = new Point(22, 171), AutoSize = true, ForeColor = Theme.Warning });
            page.Controls.Add(_schedule);
            page.Controls.Add(configure);
            page.Controls.Add(run);
            page.Controls.Add(advanced);
            page.Controls.Add(components);
            page.Controls.Add(energy);
            page.Controls.Add(storageSense);
            page.Controls.Add(info);
            return page;
        }

        private TabPage BuildReportTab()
        {
            var page = NewPage("Histórico");
            page.Controls.Add(new Label { Text = "Atividades recentes", Location = new Point(20, 20), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 13f) });
            var refresh = ButtonFactory("Atualizar", 605, 12, 110, Theme.Primary);
            var export = ButtonFactory("Exportar selecionado", 725, 12, 165, Theme.Secondary);
            var open = ButtonFactory("Abrir pasta", 900, 12, 110, Theme.Secondary);
            refresh.Click += delegate { LoadReportHistory(); };
            export.Click += ExportSelectedReport;
            open.Click += delegate { V2Engine.OpenReportsFolder(); };

            _reportCards = new FlowLayoutPanel
            {
                Location = new Point(20, 64),
                Size = new Size(1000, 520),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Theme.SurfaceDark,
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(10)
            };
            _reportEmpty = new Label { Text = "Nenhuma atividade registrada", AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(18, 24, 0, 0) };
            page.Controls.Add(refresh);
            page.Controls.Add(export);
            page.Controls.Add(open);
            page.Controls.Add(_reportCards);
            page.Enter += delegate { LoadReportHistory(); };
            return page;
        }

        private void LoadReportHistory()
        {
            if (_reportCards == null) return;
            List<ReportSummary> reports = V2Engine.ReadReportHistory(40);
            _reportCards.SuspendLayout();
            _reportCards.Controls.Clear();
            if (reports.Count == 0)
            {
                _reportCards.Controls.Add(_reportEmpty);
                _selectedReportPath = null;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_selectedReportPath) || !reports.Any(delegate(ReportSummary item) { return string.Equals(item.Path, _selectedReportPath, StringComparison.OrdinalIgnoreCase); }))
                    _selectedReportPath = reports[0].Path;
                foreach (ReportSummary report in reports) _reportCards.Controls.Add(CreateReportCard(report));
            }
            _reportCards.ResumeLayout();
        }

        private DashboardPanel CreateReportCard(ReportSummary report)
        {
            bool selected = string.Equals(report.Path, _selectedReportPath, StringComparison.OrdinalIgnoreCase);
            var card = new DashboardPanel
            {
                Size = new Size(950, 82),
                Margin = new Padding(6),
                Padding = new Padding(16),
                BackColor = selected ? Theme.SurfaceAlt : Theme.Surface,
                BorderColor = selected ? Theme.Primary : Theme.Border,
                Radius = 9,
                Tag = report.Path,
                Cursor = Cursors.Hand
            };
            var title = new Label { Text = report.Category, Location = new Point(18, 13), Size = new Size(610, 24), AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11f), Cursor = Cursors.Hand };
            var date = new Label { Text = report.Created.ToString("dd/MM/yyyy  HH:mm"), Location = new Point(720, 15), Size = new Size(205, 22), TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Muted, Cursor = Cursors.Hand };
            var summary = new Label { Text = report.Summary, Location = new Point(18, 45), Size = new Size(907, 22), AutoEllipsis = true, ForeColor = Theme.Muted, Cursor = Cursors.Hand };
            EventHandler select = delegate { SelectReport(report.Path); };
            card.Click += select;
            title.Click += select;
            date.Click += select;
            summary.Click += select;
            card.Controls.Add(title);
            card.Controls.Add(date);
            card.Controls.Add(summary);
            return card;
        }

        private void SelectReport(string path)
        {
            if (string.Equals(path, _selectedReportPath, StringComparison.OrdinalIgnoreCase)) return;
            _selectedReportPath = path;
            LoadReportHistory();
        }

        private void ExportSelectedReport(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedReportPath) || !File.Exists(_selectedReportPath))
            {
                MessageBox.Show(this, "Selecione uma atividade para exportar.", "Histórico", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var save = new SaveFileDialog { Filter = "Relatório de texto|*.txt", FileName = Path.GetFileName(_selectedReportPath) })
                if (save.ShowDialog(this) == DialogResult.OK) File.Copy(_selectedReportPath, save.FileName, true);
        }

        private async Task RefreshAudit()
        {
            await RunWork("Analisando sistema...", delegate(CancellationToken t, IProgress<string> p) { return V2Engine.BuildFullAudit(t, p); });
            _liveMetrics = V2Engine.ReadMetrics();
            ApplyActivitySample(_liveMetrics);
            string environment = V2Engine.DetectManagedEnvironmentShort();
            _managedEnvironment = environment.IndexOf("Gerenciado", StringComparison.OrdinalIgnoreCase) >= 0 || environment.IndexOf("corporativo", StringComparison.OrdinalIgnoreCase) >= 0;
            UpdateMetricCards(_liveMetrics);
            UpdateSustainedAlert(_liveMetrics);
            LoadStartup();
        }

        private void RefreshLiveMetrics()
        {
            if (_liveMetrics == null || IsDisposed) return;
            ApplyActivitySample(_liveMetrics);
            _liveMetricTicks++;
            if (_liveMetricTicks % 5 == 0) RefreshDiskMetrics(_liveMetrics);
            UpdateMetricCards(_liveMetrics);
            UpdateSustainedAlert(_liveMetrics);
            if (_liveMetricTicks % 2 == 0)
            {
                List<ProcessActivity> activities = _processSampler.Sample(3);
                _lastProcessActivities = activities;
                UpdateProcessCards(activities);
                _processHistory.Record(activities);
                if (_liveMetricTicks % 10 == 0) UpdateProcessHistoryGrid();
            }
            if (_liveMetricTicks % 5 == 0) HandleAutomaticPowerProfile();
            if (_liveMetricTicks == 2 || _liveMetricTicks % 60 == 0) BeginHistoryCapture();
        }

        private void ApplyActivitySample(SystemMetrics metrics)
        {
            double totalRamGb;
            double freeRamGb;
            double? cpuUsage = _activitySampler.Sample(out totalRamGb, out freeRamGb);
            if (totalRamGb > 0)
            {
                metrics.TotalRamGb = totalRamGb;
                metrics.FreeRamGb = freeRamGb;
            }
            if (cpuUsage.HasValue) metrics.CpuUsagePercent = cpuUsage.Value;
        }

        private static void RefreshDiskMetrics(SystemMetrics metrics)
        {
            try
            {
                var disk = new DriveInfo("C");
                metrics.FreeDiskGb = disk.AvailableFreeSpace / 1073741824.0;
                metrics.TotalDiskGb = disk.TotalSize / 1073741824.0;
                metrics.FreeDiskPercent = disk.TotalSize == 0 ? 0 : disk.AvailableFreeSpace * 100.0 / disk.TotalSize;
            }
            catch { }
        }

        private void UpdateMetricCards(SystemMetrics m)
        {
            double freeRamPercent = m.TotalRamGb > 0 ? (m.FreeRamGb / m.TotalRamGb) * 100.0 : 0;
            _memoryValue.Text = string.Format(CultureInfo.CurrentCulture, "{0:N1} GB", m.FreeRamGb);
            _memoryDetail.Text = string.Format(CultureInfo.CurrentCulture, "Total {0:N1} GB\r\n{1:N0}% livre", m.TotalRamGb, freeRamPercent);
            _memoryGauge.Value = ClampPercent(freeRamPercent);
            Color memoryColor = freeRamPercent < 15 ? Theme.Warning : Theme.Success;
            _memoryGauge.BarColor = memoryColor;
            _memoryChart.LineColor = memoryColor;
            _memoryChart.AddValue(freeRamPercent);

            _diskValue.Text = string.Format(CultureInfo.CurrentCulture, "{0:N1} GB", m.FreeDiskGb);
            _diskDetail.Text = string.Format(CultureInfo.CurrentCulture, "Total {0:N1} GB\r\n{1:N1}% livre", m.TotalDiskGb, m.FreeDiskPercent);
            _diskGauge.Value = ClampPercent(m.FreeDiskPercent);
            Color diskColor = m.FreeDiskPercent < 10 ? Theme.Warning : Theme.Success;
            _diskGauge.BarColor = diskColor;
            _diskChart.LineColor = diskColor;
            _diskChart.AddValue(m.FreeDiskPercent);

            _cpuValue.Text = string.Format(CultureInfo.CurrentCulture, "{0:N0}%", m.CpuUsagePercent);
            _cpuDetail.Text = m.CpuCores > 0 ? m.CpuCores + " núcleos\r\n" + m.CpuThreads + " threads" : "Atividade atual";
            _cpuGauge.Value = ClampPercent(m.CpuUsagePercent);
            Color cpuColor = m.CpuUsagePercent >= 90 ? Theme.Danger : m.CpuUsagePercent >= 70 ? Theme.Warning : Theme.Success;
            _cpuGauge.BarColor = cpuColor;
            _cpuChart.LineColor = cpuColor;
            _cpuChart.AddValue(m.CpuUsagePercent);

            string power = m.PowerScheme.IndexOf("Desempenho Máximo", StringComparison.OrdinalIgnoreCase) >= 0 ? "Máximo desempenho ativo" : "Perfil de energia ativo";
            _environmentBadge.Text = power + "  •  " + (_managedEnvironment ? "Ambiente corporativo" : "Ambiente pessoal");
            if (m.FreeDiskPercent < 10 && freeRamPercent < 15)
            {
                _overviewStatus.Text = "Otimizado, com recursos no limite";
                _overviewNote.Text = "Memória em uso e pouco espaço livre no disco C:";
            }
            else if (m.FreeDiskPercent < 10)
            {
                _overviewStatus.Text = "Otimizado, com pouco espaço";
                _overviewNote.Text = "O disco C: precisa de uma limpeza";
            }
            else if (freeRamPercent < 15)
            {
                _overviewStatus.Text = "Otimizado, com memória em uso";
                _overviewNote.Text = "Feche aplicativos pesados para ganhar agilidade";
            }
            else
            {
                _overviewStatus.Text = "Tudo em ordem";
                _overviewNote.Text = "Seu perfil e as proteções essenciais estão ativos";
            }
        }

        private void UpdateProcessCards(List<ProcessActivity> processes)
        {
            for (int i = 0; i < _processCards.Length; i++)
            {
                if (processes != null && i < processes.Count)
                {
                    ProcessActivity process = processes[i];
                    _processNames[i].Text = process.Name;
                    _processTags[i].Text = process.Protected ? "Protegido" : process.Impact;
                    _processTags[i].ForeColor = process.Protected ? Theme.Success : process.Impact == "Alto" ? Theme.Warning : Theme.Muted;
                    _processStats[i].Text = string.Format(CultureInfo.CurrentCulture, "CPU {0:N1}%   •   RAM {1}", process.CpuPercent, V2Engine.FormatBytes(process.WorkingSetBytes));
                }
                else
                {
                    _processNames[i].Text = "Sem atividade relevante";
                    _processTags[i].Text = "";
                    _processStats[i].Text = "Nenhum processo para exibir";
                    _processStats[i].ForeColor = Theme.Muted;
                }
            }
        }

        private void UpdateSustainedAlert(SystemMetrics metrics)
        {
            SustainedAlert alert = _alertMonitor.Evaluate(metrics);
            if (alert == null)
            {
                _liveAlert.Text = "Monitorando em tempo real";
                _liveAlert.ForeColor = Theme.Success;
                return;
            }

            _liveAlert.Text = "ALERTA  •  " + alert.Title;
            _liveAlert.ForeColor = Theme.Danger;
            _overviewStatus.Text = alert.Title;
            _overviewNote.Text = alert.Detail;
        }

        private async Task LoadHardware(bool force)
        {
            if (_hardwareLoaded && !force) return;
            _hardwareLoaded = true;
            _hardwareSummary.Text = "Lendo componentes...";
            _hardwareCards.Controls.Clear();
            await RunWork("Lendo hardware...", delegate(CancellationToken t, IProgress<string> p)
            {
                List<ImportantHardware> records = V2Engine.ReadImportantHardware(t, p);
                string recommendations = V2Engine.BuildPerformanceRecommendations();
                BeginInvoke((Action)delegate
                {
                    _importantHardware = records;
                    PopulateHardwareCards(records);
                    _hardwareSummary.Text = "Componentes principais";
                });
                return V2Engine.ImportantHardwareReport(records, recommendations);
            });
        }

        private void PopulateHardwareCards(List<ImportantHardware> records)
        {
            _hardwareCards.SuspendLayout();
            _hardwareCards.Controls.Clear();
            int cardIndex = 0;
            foreach (ImportantHardware record in records)
            {
                var card = new Panel { Size = new Size(455, 132), Margin = new Padding(8), BackColor = Theme.Surface };
                card.Controls.Add(new Label { Text = record.Component, Location = new Point(16, 13), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 9.5f) });
                card.Controls.Add(new Label { Text = record.Model, Location = new Point(16, 38), Size = new Size(440, 25), AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 12f) });
                card.Controls.Add(new Label { Text = record.Specifications, Location = new Point(16, 70), Size = new Size(440, 40), AutoEllipsis = true, ForeColor = Theme.Text });
                card.Controls.Add(new Label { Text = record.Status, Location = new Point(16, 108), AutoSize = true, ForeColor = record.Warning ? Color.Khaki : Color.LightGreen, Font = new Font("Segoe UI Semibold", 9f) });
                _hardwareCards.Controls.Add(card);
                if (cardIndex % 2 == 1) _hardwareCards.SetFlowBreak(card, true);
                cardIndex++;
            }
            _hardwareCards.ResumeLayout();
        }

        private void ExportHardware(object sender, EventArgs e)
        {
            if (_importantHardware == null || _importantHardware.Count == 0) return;
            using (var save = new SaveFileDialog { Filter = "Texto|*.txt", FileName = "hardware-" + Environment.MachineName + ".txt" })
            {
                if (save.ShowDialog(this) == DialogResult.OK) File.WriteAllText(save.FileName, V2Engine.ImportantHardwareReport(_importantHardware, V2Engine.BuildPerformanceRecommendations()), Encoding.UTF8);
            }
        }

        private async Task<string> RunWork(string initialStatus, Func<CancellationToken, IProgress<string>, string> worker, bool saveReport = true)
        {
            if (_cts != null) return string.Empty;
            _cts = new CancellationTokenSource();
            _progress.Visible = true;
            _status.Location = new Point(194, 12);
            _progress.Style = ProgressBarStyle.Marquee;
            _cancel.Enabled = true;
            _status.Text = initialStatus;
            var progress = new Progress<string>(delegate(string s) { _status.Text = s; });
            try
            {
                string result = await Task.Run(delegate { return worker(_cts.Token, progress); }, _cts.Token);
                _status.Text = "Concluído em " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                if (saveReport)
                {
                    string path = V2Engine.SaveReport(result);
                    if (!string.IsNullOrWhiteSpace(path)) _selectedReportPath = path;
                    LoadReportHistory();
                }
                return result;
            }
            catch (OperationCanceledException)
            {
                _status.Text = "Operação cancelada";
                return "Operação cancelada pelo usuário.";
            }
            catch (Exception ex)
            {
                string result = "Falha: " + ex.Message + Environment.NewLine + ex;
                _status.Text = "Falha";
                if (saveReport)
                {
                    string path = V2Engine.SaveReport(result);
                    if (!string.IsNullOrWhiteSpace(path)) _selectedReportPath = path;
                    LoadReportHistory();
                }
                return result;
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Value = 0;
                _progress.Visible = false;
                _status.Location = new Point(20, 12);
                _cancel.Enabled = false;
            }
        }

        private void LoadStartup()
        {
            if (_startupGrid == null) return;
            _startupGrid.Rows.Clear();
            foreach (StartupEntry item in V2Engine.ReadStartupEntries())
                _startupGrid.Rows.Add(item.Enabled, item.Name, item.Impact, item.Command, item.Enabled);
        }

        private async Task ApplyStartupGrid()
        {
            var entries = new List<StartupEntry>();
            foreach (DataGridViewRow row in _startupGrid.Rows)
            {
                if (row.IsNewRow) continue;
                bool enabled = Convert.ToBoolean(row.Cells["Enabled"].Value);
                string name = Convert.ToString(row.Cells["Name"].Value);
                bool original = Convert.ToBoolean(row.Cells["Original"].Value);
                if (original && !enabled && string.Equals(name, "OneDrive", StringComparison.OrdinalIgnoreCase))
                {
                    if (MessageBox.Show(this, "Desativar a inicialização do OneDrive interrompe a sincronização automática até que ele seja aberto manualmente. Continuar?", "OneDrive", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    {
                        row.Cells["Enabled"].Value = true;
                        enabled = true;
                    }
                }
                entries.Add(new StartupEntry { Enabled = enabled, Name = name, Command = Convert.ToString(row.Cells["Command"].Value), Impact = Convert.ToString(row.Cells["Impact"].Value) });
            }
            await RunWork("Atualizando inicialização...", delegate(CancellationToken t, IProgress<string> p) { return V2Engine.ApplyStartupEntries(entries, t, p); });
            LoadStartup();
        }

        private void LoadVolumes()
        {
            List<VolumeEntry> volumes = V2Engine.ReadVolumes();
            _volumeGrid.Rows.Clear();
            foreach (VolumeEntry volume in volumes)
                _volumeGrid.Rows.Add(volume.Drive, volume.Label, V2Engine.FormatBytes(volume.UsedBytes), V2Engine.FormatBytes(volume.FreeBytes), V2Engine.FormatBytes(volume.TotalBytes), volume.UsagePercent.ToString("N0", CultureInfo.CurrentCulture) + "%", volume.FileSystem, volume.Health);
            if (_volumeGrid.Rows.Count > 0)
            {
                _volumeGrid.Rows[0].Selected = true;
                _selectedDrive = Convert.ToString(_volumeGrid.Rows[0].Cells["Drive"].Value);
            }
            _storageSummary.Text = volumes.Count + (volumes.Count == 1 ? " disco disponível" : " discos disponíveis");
        }

        private async Task ScanSelectedVolume()
        {
            if (string.IsNullOrWhiteSpace(_selectedDrive)) { LoadVolumes(); if (string.IsNullOrWhiteSpace(_selectedDrive)) return; }
            _storageGrid.Rows.Clear();
            _folderSummary.Text = "Analisando " + _selectedDrive + "...";
            string drive = _selectedDrive;
            await RunWork("Analisando " + drive + "...", delegate(CancellationToken t, IProgress<string> p)
            {
                List<StorageEntry> rows = V2Engine.ScanVolume(drive, t, p, delegate(StorageEntry row)
                {
                    BeginInvoke((Action)delegate
                    {
                        _storageGrid.Rows.Add(row.Path, V2Engine.FormatBytes(row.LogicalBytes), V2Engine.FormatBytes(row.AllocatedBytes));
                        _folderSummary.Text = _storageGrid.Rows.Count + " pastas medidas em " + drive;
                    });
                });
                BeginInvoke((Action)delegate { _folderSummary.Text = rows.Count + " pastas • " + V2Engine.FormatBytes(rows.Sum(delegate(StorageEntry e) { return e.AllocatedBytes; })); });
                return V2Engine.StorageReport(rows);
            });
        }

        private async Task OptimizeSelectedVolume()
        {
            if (string.IsNullOrWhiteSpace(_selectedDrive)) { LoadVolumes(); if (string.IsNullOrWhiteSpace(_selectedDrive)) return; }
            string drive = _selectedDrive;
            if (MessageBox.Show(this, "O Windows escolherá automaticamente TRIM, desfragmentação ou otimização em camadas para " + drive + ". Continuar?", "Otimizar unidade", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            await RunWork("Otimizando " + drive + "...", delegate(CancellationToken t, IProgress<string> p) { return WindowsMaintenance.OptimizeVolume(drive, t, p); });
            LoadVolumes();
        }

        private async Task OpenSafeCleanup()
        {
            List<CleanupTarget> targets = null;
            await RunWork("Calculando limpeza...", delegate(CancellationToken t, IProgress<string> p)
            {
                targets = V2Engine.GetCleanupTargets(t, p);
                return "Itens de limpeza calculados.";
            }, false);
            if (targets == null || targets.Count == 0) return;
            using (var dialog = new SafeCleanupForm(targets))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                List<CleanupTarget> selected = dialog.SelectedTargets;
                if (selected.Count == 0) return;
                long total = selected.Sum(delegate(CleanupTarget item) { return item.SizeBytes; });
                if (MessageBox.Show(this, "Limpar " + V2Engine.FormatBytes(total) + " de arquivos temporários e caches?", "Limpeza segura", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await RunWork("Limpando arquivos temporários...", delegate(CancellationToken t, IProgress<string> p) { return V2Engine.CleanTargets(selected, t, p); });
                LoadVolumes();
            }
        }

        private async Task ScanLargeFiles()
        {
            if (string.IsNullOrWhiteSpace(_selectedDrive)) { LoadVolumes(); if (string.IsNullOrWhiteSpace(_selectedDrive)) return; }
            string drive = _selectedDrive;
            _storageGrid.Rows.Clear();
            _folderSummary.Text = "Mapeando arquivos maiores que 100 MB em " + drive + "...";
            List<LargeFileEntry> files = null;
            await RunWork("Mapeando arquivos grandes...", delegate(CancellationToken t, IProgress<string> p)
            {
                files = AdvancedEngine.FindLargeFiles(drive, t, p);
                var report = new StringBuilder("ARQUIVOS GRANDES\r\n" + new string('=', 72) + "\r\n");
                foreach (LargeFileEntry file in files) report.AppendLine(V2Engine.FormatBytes(file.Size) + " | " + file.Path);
                return report.ToString();
            });
            if (files == null) return;
            foreach (LargeFileEntry file in files) _storageGrid.Rows.Add(file.Path, V2Engine.FormatBytes(file.Size), file.Modified.ToString("dd/MM/yyyy"));
            _folderSummary.Text = files.Count + " arquivos grandes • " + V2Engine.FormatBytes(files.Sum(item => item.Size));
        }

        private async Task ScanDuplicates()
        {
            using (var picker = new FolderBrowserDialog { Description = "Escolha a pasta para procurar arquivos duplicados" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                _storageGrid.Rows.Clear();
                string folder = picker.SelectedPath;
                List<DuplicateEntry> rows = null;
                await RunWork("Procurando duplicados...", delegate(CancellationToken t, IProgress<string> p)
                {
                    rows = V2Engine.FindDuplicates(folder, t, p);
                    return V2Engine.DuplicateReport(folder, rows);
                });
                if (rows == null) return;
                foreach (DuplicateEntry row in rows) _storageGrid.Rows.Add(row.Path, V2Engine.FormatBytes(row.Size), "Grupo " + row.Group);
                _folderSummary.Text = rows.Count == 0 ? "Nenhum duplicado encontrado" : rows.Select(item => item.Group).Distinct().Count() + " grupos confirmados por SHA-256";
                if (rows.Count == 0) return;
                using (var dialog = new DuplicateReviewForm(rows))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedEntries.Count == 0) return;
                    int count = dialog.SelectedEntries.Count;
                    if (MessageBox.Show(this, "Mover " + count + " arquivo(s) para a quarentena reversível?", "Duplicados", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    await RunWork("Movendo duplicados para a quarentena...", delegate(CancellationToken t, IProgress<string> p) { return AdvancedEngine.QuarantineDuplicates(dialog.SelectedEntries, t, p); });
                    await ScanDuplicatesRefresh(folder);
                }
            }
        }

        private async Task ScanDuplicatesRefresh(string folder)
        {
            List<DuplicateEntry> rows = null;
            await RunWork("Atualizando duplicados...", delegate(CancellationToken t, IProgress<string> p)
            {
                rows = V2Engine.FindDuplicates(folder, t, p);
                return V2Engine.DuplicateReport(folder, rows);
            }, false);
            _storageGrid.Rows.Clear();
            if (rows != null) foreach (DuplicateEntry row in rows) _storageGrid.Rows.Add(row.Path, V2Engine.FormatBytes(row.Size), "Grupo " + row.Group);
            _folderSummary.Text = rows == null || rows.Count == 0 ? "Nenhum duplicado restante" : rows.Select(item => item.Group).Distinct().Count() + " grupos restantes";
        }

        private async Task AdvancedCleanup()
        {
            using (var dialog = new CleanupForm())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!dialog.EmptyRecycleBin && !dialog.RemoveWindowsOld) return;
                string exact = (dialog.EmptyRecycleBin ? "esvaziar definitivamente a Lixeira" : string.Empty) + (dialog.EmptyRecycleBin && dialog.RemoveWindowsOld ? " e " : string.Empty) + (dialog.RemoveWindowsOld ? "remover Windows.old e a opção de reversão" : string.Empty);
                if (MessageBox.Show(this, "Confirma que deseja " + exact + "?", "Confirmação de exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                bool recycle = dialog.EmptyRecycleBin;
                bool old = dialog.RemoveWindowsOld;
                await RunWork("Executando limpeza avançada...", delegate(CancellationToken t, IProgress<string> p) { return Optimizer.AdvancedCleanup(recycle, old); });
            }
        }

        private void ExportBackup(object sender, EventArgs e)
        {
            V2Engine.EnsureSnapshot();
            using (var save = new SaveFileDialog { Filter = "Backup JSON|*.json", FileName = "backup-otimizador-" + DateTime.Now.ToString("yyyyMMdd") + ".json" })
            {
                if (save.ShowDialog(this) == DialogResult.OK)
                {
                    File.Copy(V2Engine.SnapshotPath, save.FileName, true);
                    MessageBox.Show(this, "Backup exportado.", "Concluído", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ExportGrid(DataGridView grid, string defaultName)
        {
            using (var save = new SaveFileDialog { Filter = "CSV|*.csv", FileName = defaultName })
            {
                if (save.ShowDialog(this) != DialogResult.OK) return;
                var sb = new StringBuilder();
                foreach (DataGridViewColumn c in grid.Columns) sb.Append('"').Append(c.HeaderText.Replace("\"", "\"\"")).Append("\",");
                sb.AppendLine();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow) continue;
                    foreach (DataGridViewCell cell in row.Cells) sb.Append('"').Append(Convert.ToString(cell.Value).Replace("\"", "\"\"")).Append("\",");
                    sb.AppendLine();
                }
                File.WriteAllText(save.FileName, sb.ToString(), Encoding.UTF8);
            }
        }

        private void RunAsAdmin(object sender, EventArgs e)
        {
            if (Optimizer.IsAdministrator()) { MessageBox.Show(this, "O programa já está elevado."); return; }
            try { Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" }); Close(); }
            catch (Exception ex) { MessageBox.Show(this, "Elevação cancelada: " + ex.Message); }
        }

        private void SaveAdvancedPreferences()
        {
            AdvancedSettings settings = AdvancedEngine.ReadSettings();
            settings.MinimizeToTray = _minimizeToTray != null && _minimizeToTray.Checked;
            settings.AutomaticPowerProfiles = _automaticProfiles != null && _automaticProfiles.Checked;
            AdvancedEngine.SaveSettings(settings);
            _advancedSettings = settings;
        }

        private void HandleAutomaticPowerProfile()
        {
            if (_automaticProfiles == null || !_automaticProfiles.Checked) return;
            PowerLineStatus current = SystemInformation.PowerStatus.PowerLineStatus;
            if (current == PowerLineStatus.Unknown || (_lastPowerLineStatus.HasValue && _lastPowerLineStatus.Value == current)) return;
            _lastPowerLineStatus = current;
            Task.Run(delegate { AdvancedEngine.ApplyAutomaticPowerProfile(current == PowerLineStatus.Online); });
        }

        private void ConfigureTrayIcon()
        {
            var menu = new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Text, ShowImageMargin = false };
            var open = new ToolStripMenuItem("Abrir otimizador");
            var maintenance = new ToolStripMenuItem("Executar manutenção segura");
            var exit = new ToolStripMenuItem("Sair");
            open.Click += delegate { RestoreFromTray(); };
            maintenance.Click += async delegate
            {
                string report = await Task.Run(delegate { return V2Engine.MaintenanceReport(CancellationToken.None, new Progress<string>()); });
                V2Engine.SaveReport(report);
                _trayIcon.BalloonTipTitle = "Manutenção concluída";
                _trayIcon.BalloonTipText = "O relatório foi salvo no histórico.";
                _trayIcon.ShowBalloonTip(4000);
            };
            exit.Click += delegate { _trayIcon.Visible = false; Close(); };
            menu.Items.Add(open);
            menu.Items.Add(maintenance);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);
            _trayIcon = new NotifyIcon { Icon = SystemIcons.Application, Text = "Otimizador de Desempenho", ContextMenuStrip = menu, Visible = false };
            _trayIcon.DoubleClick += delegate { RestoreFromTray(); };
            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized && _minimizeToTray != null && _minimizeToTray.Checked)
                {
                    Hide();
                    ShowInTaskbar = false;
                    _trayIcon.Visible = true;
                    _trayIcon.BalloonTipTitle = "Monitoramento ativo";
                    _trayIcon.BalloonTipText = "O otimizador continua acompanhando o sistema.";
                    _trayIcon.ShowBalloonTip(2500);
                }
            };
        }

        private void RestoreFromTray()
        {
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            _trayIcon.Visible = false;
        }

        private async Task CheckForUpdates()
        {
            _updateStatus.Text = "Verificando canal de atualização...";
            UpdateCheckResult result = await Task.Run(delegate { return AdvancedEngine.CheckForUpdates(); });
            _updateStatus.Text = result.Message;
            if (!result.Available || result.Manifest == null) return;
            string notes = string.IsNullOrWhiteSpace(result.Manifest.Notes) ? string.Empty : "\r\n\r\n" + result.Manifest.Notes;
            if (MessageBox.Show(this, result.Message + notes + "\r\n\r\nBaixar e verificar o instalador?", "Atualização", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            _updateStatus.Text = "Baixando e verificando SHA-256...";
            string download = await Task.Run(delegate { return AdvancedEngine.DownloadVerifiedUpdate(result.Manifest); });
            _updateStatus.Text = download;
        }

        private void BeginAutomaticUpdateCheck()
        {
            Task.Run(delegate { return AdvancedEngine.CheckForUpdates(); }).ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled || IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        UpdateCheckResult result = task.Result;
                        if (_updateStatus != null) _updateStatus.Text = result.Message;
                        if (result.Available)
                        {
                            _trayIcon.BalloonTipTitle = "Atualização disponível";
                            _trayIcon.BalloonTipText = result.Message;
                            _trayIcon.ShowBalloonTip(4000);
                        }
                    });
                }
                catch (InvalidOperationException) { }
            });
        }

        private void ShowTextDialog(string title, string content)
        {
            using (var dialog = new Form { Text = title, StartPosition = FormStartPosition.CenterParent, Size = new Size(720, 520), MinimumSize = new Size(560, 400), BackColor = Theme.Background, ForeColor = Theme.Text, Font = new Font("Segoe UI", 9.5f) })
            {
                var text = new TextBox { Text = content, Location = new Point(20, 20), Size = new Size(664, 400), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Theme.SurfaceDark, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
                var close = ButtonFactory("Fechar", 584, 432, 100, Theme.Secondary);
                close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                close.DialogResult = DialogResult.OK;
                dialog.Controls.Add(text);
                dialog.Controls.Add(close);
                dialog.AcceptButton = close;
                dialog.CancelButton = close;
                dialog.ShowDialog(this);
            }
        }

        private TabPage NewPage(string text)
        {
            return new TabPage(text) { BackColor = Theme.Background, ForeColor = Theme.Text, AccessibleName = "Aba " + text };
        }

        private DashboardPanel DashboardCard(int x, int y, int width, int height)
        {
            return new DashboardPanel { Location = new Point(x, y), Size = new Size(width, height), BackColor = Theme.Surface, BorderColor = Theme.Border, Radius = 10 };
        }

        private DashboardPanel MetricCard(string title, int x, int y, out Label value, out Label detail, out ModernProgressBar gauge, out SparklineChart chart)
        {
            var card = DashboardCard(x, y, 324, 112);
            card.Controls.Add(new Label { Text = title.ToUpperInvariant(), Location = new Point(18, 9), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8.3f) });
            value = new Label { Text = "--", Location = new Point(17, 29), Size = new Size(140, 30), AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 16f) };
            detail = new Label { Text = "Calculando...", Location = new Point(151, 25), Size = new Size(155, 38), TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) };
            chart = new SparklineChart { Location = new Point(18, 64), Size = new Size(288, 28), LineColor = Theme.Primary, AccessibleName = "Histórico de 60 segundos de " + title };
            gauge = new ModernProgressBar { Location = new Point(18, 99), Size = new Size(288, 5), Value = 0, BarColor = Theme.Primary, TrackColor = Theme.SurfaceAlt, AccessibleName = "Percentual de " + title };
            card.Controls.Add(value);
            card.Controls.Add(detail);
            card.Controls.Add(chart);
            card.Controls.Add(gauge);
            return card;
        }

        private CheckBox Option(string text, int x, int y, bool value)
        {
            return new CheckBox { Text = text, AutoSize = true, Location = new Point(x, y), Checked = value, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat, AccessibleName = text };
        }

        private void UpdateProfileDescription()
        {
            if (_profileDescription == null || _profile == null) return;
            if (_profile.SelectedIndex == 1) _profileDescription.Text = "Bom equilíbrio para o uso diário";
            else if (_profile.SelectedIndex == 2) _profileDescription.Text = "Menor consumo quando estiver na bateria";
            else _profileDescription.Text = "Prioriza velocidade e resposta do sistema";
        }

        private static int ClampPercent(double value)
        {
            return Math.Max(0, Math.Min(100, (int)Math.Round(value)));
        }

        private DataGridView Grid(int x, int y, int width, int height)
        {
            var grid = new DataGridView
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = Theme.SurfaceDark,
                ForeColor = Theme.Text,
                GridColor = Color.FromArgb(62, 67, 76),
                BorderStyle = BorderStyle.FixedSingle,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                EnableHeadersVisualStyles = false,
                AccessibleName = "Tabela de dados"
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
            grid.DefaultCellStyle.BackColor = Theme.SurfaceDark;
            grid.DefaultCellStyle.ForeColor = Theme.Text;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 90, 158);
            return grid;
        }

        private static Button ButtonFactory(string text, int x, int y, int width, Color color)
        {
            var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 38), BackColor = color, ForeColor = Theme.ButtonText, FlatStyle = FlatStyle.Flat, AccessibleName = text };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void DrawTab(object sender, DrawItemEventArgs e)
        {
            bool selected = e.Index == _tabs.SelectedIndex;
            Rectangle rect = e.Bounds;
            Color background = selected ? Theme.Surface : Theme.Header;
            using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, rect);
            TextRenderer.DrawText(e.Graphics, _tabs.TabPages[e.Index].Text, Font, rect, selected ? Theme.Text : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (selected)
            {
                using (var accent = new SolidBrush(Theme.Primary)) e.Graphics.FillRectangle(accent, rect.Left + 16, rect.Bottom - 3, rect.Width - 32, 3);
            }
        }

        private void ResizeTabs()
        {
            if (_tabs.TabCount == 0 || _tabs.ClientSize.Width < 100) return;
            int width = Math.Max(110, (_tabs.ClientSize.Width - 4) / _tabs.TabCount);
            if (_tabs.ItemSize.Width != width || _tabs.ItemSize.Height != 36) _tabs.ItemSize = new Size(width, 36);
        }
    }

    internal sealed class DashboardPanel : Panel
    {
        public int Radius { get; set; }
        public Color BorderColor { get; set; }

        public DashboardPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Radius = 10;
            BorderColor = Color.Transparent;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (Width <= 1 || Height <= 1) return;
            using (GraphicsPath path = RoundedRectangle(new Rectangle(0, 0, Width, Height), Radius))
            {
                Region oldRegion = Region;
                Region = new Region(path);
                if (oldRegion != null) oldRegion.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (BorderColor == Color.Transparent || Width <= 1 || Height <= 1) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (var pen = new Pen(BorderColor))
                e.Graphics.DrawPath(pen, path);
        }

        private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
        {
            int diameter = Math.Max(2, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class SparklineChart : Control
    {
        private readonly List<float> _values = new List<float>();
        private Color _lineColor = Theme.Primary;

        public Color LineColor
        {
            get { return _lineColor; }
            set { _lineColor = value; Invalidate(); }
        }

        public SparklineChart()
        {
            DoubleBuffered = true;
            AccessibleRole = AccessibleRole.Graphic;
            TabStop = false;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }

        public void AddValue(double value)
        {
            _values.Add((float)Math.Max(0, Math.Min(100, value)));
            if (_values.Count > 60) _values.RemoveAt(0);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 2 || Height < 2) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var gridPen = new Pen(Color.FromArgb(32, Theme.Muted)))
            {
                e.Graphics.DrawLine(gridPen, 0, Height / 2, Width, Height / 2);
                e.Graphics.DrawLine(gridPen, 0, Height - 1, Width, Height - 1);
            }
            if (_values.Count == 0) return;

            var points = new PointF[_values.Count];
            float step = _values.Count > 1 ? (Width - 1f) / (_values.Count - 1) : 0;
            for (int i = 0; i < _values.Count; i++)
                points[i] = new PointF(i * step, (Height - 2f) * (1f - (_values[i] / 100f)) + 1f);

            if (points.Length == 1)
            {
                using (var dot = new SolidBrush(LineColor)) e.Graphics.FillEllipse(dot, 0, points[0].Y - 2, 4, 4);
                return;
            }

            var fillPoints = new PointF[points.Length + 2];
            fillPoints[0] = new PointF(points[0].X, Height);
            Array.Copy(points, 0, fillPoints, 1, points.Length);
            fillPoints[fillPoints.Length - 1] = new PointF(points[points.Length - 1].X, Height);
            using (var fill = new SolidBrush(Color.FromArgb(34, LineColor))) e.Graphics.FillPolygon(fill, fillPoints);
            using (var line = new Pen(LineColor, 1.8f)) e.Graphics.DrawLines(line, points);
        }
    }

    internal sealed class ModernProgressBar : Control
    {
        private int _value;
        private Color _barColor;
        private Color _trackColor;

        public int Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public Color BarColor
        {
            get { return _barColor; }
            set { _barColor = value; Invalidate(); }
        }

        public Color TrackColor
        {
            get { return _trackColor; }
            set { _trackColor = value; Invalidate(); }
        }

        public ModernProgressBar()
        {
            DoubleBuffered = true;
            _barColor = Theme.Primary;
            _trackColor = Theme.SurfaceAlt;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AccessibleRole = AccessibleRole.ProgressBar;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath trackPath = RoundedRectangle(track))
            using (var trackBrush = new SolidBrush(TrackColor))
                e.Graphics.FillPath(trackBrush, trackPath);

            int fillWidth = (int)Math.Round(track.Width * (Value / 100.0));
            if (fillWidth <= 0) return;
            Rectangle fill = new Rectangle(0, 0, Math.Max(2, fillWidth), track.Height);
            using (GraphicsPath fillPath = RoundedRectangle(fill))
            using (var fillBrush = new SolidBrush(BarColor))
                e.Graphics.FillPath(fillBrush, fillPath);
        }

        private static GraphicsPath RoundedRectangle(Rectangle rectangle)
        {
            int diameter = Math.Max(2, Math.Min(rectangle.Height, 8));
            var path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class SafeCleanupForm : Form
    {
        private readonly CheckedListBox _items;
        private readonly List<CleanupTarget> _targets;
        public List<CleanupTarget> SelectedTargets
        {
            get
            {
                var selected = new List<CleanupTarget>();
                for (int i = 0; i < _items.Items.Count; i++) if (_items.GetItemChecked(i)) selected.Add(_targets[i]);
                return selected;
            }
        }

        public SafeCleanupForm(List<CleanupTarget> targets)
        {
            _targets = targets;
            Text = "Limpeza segura";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(620, 430);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            Controls.Add(new Label { Text = "Arquivos temporários e caches", Location = new Point(24, 20), AutoSize = true, Font = new Font("Segoe UI Semibold", 14f) });
            Controls.Add(new Label { Text = "Cookies, senhas, documentos e arquivos pessoais não são incluídos.", Location = new Point(27, 54), AutoSize = true, ForeColor = Theme.Muted });
            _items = new CheckedListBox { Location = new Point(26, 88), Size = new Size(566, 260), BackColor = Theme.SurfaceDark, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, CheckOnClick = true };
            foreach (CleanupTarget target in targets) _items.Items.Add(target.Name + "   " + V2Engine.FormatBytes(target.SizeBytes), target.DefaultSelected);
            var clean = new Button { Text = "Limpar selecionados", DialogResult = DialogResult.OK, Location = new Point(332, 370), Size = new Size(150, 38), BackColor = Theme.Primary, ForeColor = Theme.ButtonText, FlatStyle = FlatStyle.Flat };
            clean.FlatAppearance.BorderSize = 0;
            var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(492, 370), Size = new Size(100, 38), BackColor = Theme.Secondary, ForeColor = Theme.ButtonText, FlatStyle = FlatStyle.Flat };
            cancel.FlatAppearance.BorderSize = 0;
            Controls.Add(_items);
            Controls.Add(clean);
            Controls.Add(cancel);
            AcceptButton = clean;
            CancelButton = cancel;
        }
    }

    internal static class Theme
    {
        private static readonly bool HighContrast = SystemInformation.HighContrast;
        public static readonly Color Background = HighContrast ? SystemColors.Window : Color.FromArgb(18, 20, 24);
        public static readonly Color Header = HighContrast ? SystemColors.Control : Color.FromArgb(13, 15, 18);
        public static readonly Color Surface = HighContrast ? SystemColors.Control : Color.FromArgb(29, 33, 39);
        public static readonly Color SurfaceAlt = HighContrast ? SystemColors.ControlDark : Color.FromArgb(39, 44, 52);
        public static readonly Color SurfaceDark = HighContrast ? SystemColors.Window : Color.FromArgb(15, 17, 20);
        public static readonly Color Border = HighContrast ? SystemColors.WindowText : Color.FromArgb(50, 56, 66);
        public static readonly Color Text = HighContrast ? SystemColors.WindowText : Color.FromArgb(239, 242, 247);
        public static readonly Color Muted = HighContrast ? SystemColors.GrayText : Color.FromArgb(157, 166, 181);
        public static readonly Color Primary = HighContrast ? SystemColors.Highlight : Color.FromArgb(48, 139, 255);
        public static readonly Color Secondary = HighContrast ? SystemColors.ControlDark : Color.FromArgb(49, 55, 65);
        public static readonly Color Success = HighContrast ? SystemColors.Highlight : Color.FromArgb(62, 199, 132);
        public static readonly Color Warning = HighContrast ? SystemColors.HotTrack : Color.FromArgb(224, 145, 56);
        public static readonly Color Danger = HighContrast ? SystemColors.HotTrack : Color.FromArgb(232, 92, 92);
        public static readonly Color ButtonText = HighContrast ? SystemColors.HighlightText : Color.White;
    }
}
