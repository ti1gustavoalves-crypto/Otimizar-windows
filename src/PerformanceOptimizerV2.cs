using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
        private Button[] _navigationButtons;
        private Image _brandImage;
        private readonly string _displayVersion;
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
        private bool _startupLoading;
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
        private DataGridView _installedDriverGrid;
        private Label _driverInventorySummary;
        private ComboBox _driverFilter;
        private TextBox _driverSearch;
        private CheckBox _driverProblemsOnly;
        private List<DriverInventoryItem> _driverInventoryItems = new List<DriverInventoryItem>();
        private DataGridView _driverGrid;
        private Label _driverSummary;
        private List<DriverUpdate> _driverUpdates = new List<DriverUpdate>();
        private bool _driverInventoryLoaded;
        private DataGridView _programUpdateGrid;
        private Label _programUpdateSummary;
        private TextBox _programUpdateSearch;
        private List<ProgramUpdate> _programUpdates = new List<ProgramUpdate>();
        private bool _programUpdatesLoaded;
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
            Version version = GetType().Assembly.GetName().Version;
            _displayVersion = version.Major + "." + version.Minor;
            Text = "Otimizador de Desempenho " + _displayVersion;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1260, 760);
            Size = new Size(1320, 820);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            NativeWindowTheme.Apply(this);
            AutoScaleMode = AutoScaleMode.Dpi;
            AccessibleName = "Otimizador de Desempenho " + _displayVersion;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            _advancedSettings = AdvancedEngine.ReadSettings();
            _processHistory = new ProcessHistoryTracker();

            _tabs = new TabControl { Location = new Point(-4, -28), SizeMode = TabSizeMode.Fixed, ItemSize = new Size(1, 24), Appearance = TabAppearance.FlatButtons };
            _tabs.TabPages.Add(BuildDashboard());
            _tabs.TabPages.Add(BuildHardwareTab());
            _tabs.TabPages.Add(BuildStartupTab());
            _tabs.TabPages.Add(BuildStorageTab());
            _tabs.TabPages.Add(BuildDriversTab());
            _tabs.TabPages.Add(BuildProgramUpdatesTab());
            _tabs.TabPages.Add(BuildDiagnosticsTab());
            _tabs.TabPages.Add(BuildMaintenanceTab());
            _tabs.TabPages.Add(BuildControlTab());
            _tabs.SelectedIndexChanged += async delegate
            {
                UpdateNavigationState();
                if (_tabs.SelectedIndex == 5 && !_programUpdatesLoaded) await SearchProgramUpdates();
            };

            var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            content.Controls.Add(_tabs);
            content.Resize += delegate
            {
                _tabs.Location = new Point(-4, -28);
                _tabs.Size = new Size(content.ClientSize.Width + 8, content.ClientSize.Height + 32);
            };

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            body.Controls.Add(content);
            body.Controls.Add(BuildNavigation());

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

            Controls.Add(body);
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
                if (_brandImage != null) _brandImage.Dispose();
            };
        }

        private Panel BuildNavigation()
        {
            var navigation = new Panel { Dock = DockStyle.Left, Width = 184, BackColor = Theme.Navigation, Padding = new Padding(14, 18, 14, 14) };
            _brandImage = LoadBrandImage();
            if (_brandImage != null)
            {
                navigation.Controls.Add(new PictureBox { Image = _brandImage, Location = new Point(18, 18), Size = new Size(42, 42), SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = "Ícone do Otimizador" });
            }
            navigation.Controls.Add(new Label { Text = "Otimizador", Location = new Point(68, 19), Size = new Size(102, 24), ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 12.5f), AutoEllipsis = true });
            navigation.Controls.Add(new Label { Text = "Versão " + _displayVersion, Location = new Point(69, 43), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) });

            string[] labels = { "Início", "Hardware", "Inicialização", "Armazenamento", "Drivers", "Programas", "Diagnóstico", "Manutenção", "Ajustes" };
            _navigationButtons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int tabIndex = i;
                var button = new Button
                {
                    Text = labels[i],
                    Location = new Point(12, 86 + (i * 48)),
                    Size = new Size(160, 40),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(16, 0, 0, 0),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Theme.Navigation,
                    ForeColor = Theme.Muted,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    Cursor = Cursors.Hand,
                    AccessibleName = "Abrir " + labels[i]
                };
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = Theme.SurfaceAlt;
                button.Click += delegate { _tabs.SelectedIndex = tabIndex; };
                _navigationButtons[i] = button;
                navigation.Controls.Add(button);
            }

            navigation.Controls.Add(new Label { Text = "Seguro e reversível", Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) });
            UpdateNavigationState();
            return navigation;
        }

        private Image LoadBrandImage()
        {
            try
            {
                using (Stream stream = GetType().Assembly.GetManifestResourceStream("OptimizerIconPng"))
                {
                    if (stream == null) return null;
                    using (Image image = Image.FromStream(stream)) return new Bitmap(image);
                }
            }
            catch { return null; }
        }

        private void UpdateNavigationState()
        {
            if (_navigationButtons == null) return;
            for (int i = 0; i < _navigationButtons.Length; i++)
            {
                bool selected = i == _tabs.SelectedIndex;
                _navigationButtons[i].BackColor = selected ? Theme.SurfaceAlt : Theme.Navigation;
                _navigationButtons[i].ForeColor = selected ? Theme.Text : Theme.Muted;
                _navigationButtons[i].FlatAppearance.BorderColor = selected ? Theme.Primary : Theme.Navigation;
                _navigationButtons[i].FlatAppearance.BorderSize = selected ? 1 : 0;
            }
        }

        private TabPage BuildDashboard()
        {
            var page = NewPage("Visão geral");
            var healthCard = DashboardCard(20, 18, 640, 142);
            healthCard.Controls.Add(new Label { Text = "Estado do PC", Location = new Point(20, 15), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8.5f) });
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
            profileCard.Controls.Add(new Label { Text = "Perfil de desempenho", Location = new Point(20, 15), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8.5f) });
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
            activityCard.Controls.Add(new Label { Text = "Processos em destaque", Location = new Point(18, 14), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8.5f) });
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
            _startupGrid.Columns.Add("Source", "Origem");
            _startupGrid.Columns[2].Width = 165;
            _startupGrid.Columns[2].ReadOnly = true;
            _startupGrid.Columns.Add("Impact", "Impacto estimado");
            _startupGrid.Columns[3].Width = 140;
            _startupGrid.Columns[3].ReadOnly = true;
            _startupGrid.Columns.Add("Command", "Comando");
            _startupGrid.Columns[4].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _startupGrid.Columns[4].ReadOnly = true;
            _startupGrid.Columns.Add("Original", "Original");
            _startupGrid.Columns.Add("CanChange", "Editável");
            _startupGrid.Columns.Add("RegistryHive", "Hive");
            _startupGrid.Columns.Add("RegistryPath", "Registro");
            _startupGrid.Columns.Add("ApprovalPath", "Aprovação");
            _startupGrid.Columns.Add("ValueName", "Valor");
            _startupGrid.Columns.Add("StateKind", "Tipo");
            for (int i = 5; i < _startupGrid.Columns.Count; i++) { _startupGrid.Columns[i].Visible = false; _startupGrid.Columns[i].ReadOnly = true; }
            var refresh = ButtonFactory("Atualizar lista", 20, 545, 150, Theme.Secondary);
            var save = ButtonFactory("Aplicar alterações", 182, 545, 170, Theme.Primary);
            refresh.Click += async delegate { await LoadStartupAsync(); };
            save.Click += async delegate { await ApplyStartupGrid(); };
            page.Controls.Add(_startupGrid);
            page.Controls.Add(refresh);
            page.Controls.Add(save);
            _startupGrid.Anchor = AnchorStyles.None;
            page.Resize += delegate { LayoutStartupTab(page, refresh, save); };
            LayoutStartupTab(page, refresh, save);
            page.Enter += async delegate { await LoadStartupAsync(); };
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

        private TabPage BuildDriversTab()
        {
            var page = NewPage("Drivers");
            _driverInventorySummary = new Label { Text = "Versões instaladas • verificando hardware...", Location = new Point(20, 18), Size = new Size(440, 28), ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11f) };
            _driverFilter = new ComboBox { Location = new Point(470, 14), Size = new Size(155, 28), DropDownStyle = ComboBoxStyle.DropDownList };
            _driverFilter.Items.AddRange(new object[] { "Todos", "Vídeo", "BIOS", "Firmware", "Chipset / sistema", "Áudio", "Rede", "Armazenamento", "Bluetooth", "USB", "Problema", "Sem assinatura" });
            _driverFilter.SelectedIndex = 0;
            _driverSearch = new TextBox { Location = new Point(637, 15), Size = new Size(190, 26), BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            _driverProblemsOnly = new CheckBox { Text = "Somente problemas", Location = new Point(841, 16), AutoSize = true, ForeColor = Theme.Muted };
            _driverFilter.SelectedIndexChanged += delegate { ApplyDriverInventoryFilter(); };
            _driverSearch.TextChanged += delegate { ApplyDriverInventoryFilter(); };
            _driverProblemsOnly.CheckedChanged += delegate { ApplyDriverInventoryFilter(); };
            _installedDriverGrid = Grid(20, 52, 1000, 217);
            _installedDriverGrid.Columns.Add("Category", "Componente");
            _installedDriverGrid.Columns[0].Width = 125;
            _installedDriverGrid.Columns.Add("Device", "Dispositivo");
            _installedDriverGrid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _installedDriverGrid.Columns.Add("Provider", "Fornecedor");
            _installedDriverGrid.Columns[2].Width = 135;
            _installedDriverGrid.Columns.Add("Version", "Versão instalada");
            _installedDriverGrid.Columns[3].Width = 125;
            _installedDriverGrid.Columns.Add("Date", "Data");
            _installedDriverGrid.Columns[4].Width = 90;
            _installedDriverGrid.Columns.Add("Status", "Status");
            _installedDriverGrid.Columns[5].Width = 115;
            _installedDriverGrid.Columns.Add("InfName", "Pacote");
            _installedDriverGrid.Columns[6].Width = 95;
            _installedDriverGrid.ReadOnly = true;

            _driverSummary = new Label { Text = "Atualizações disponíveis • busca ainda não executada", Location = new Point(20, 281), Size = new Size(760, 28), ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11f) };
            _driverGrid = Grid(20, 311, 1000, 231);
            _driverGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Instalar", Width = 60 });
            _driverGrid.Columns.Add("Classification", "Tipo");
            _driverGrid.Columns[1].Width = 125;
            _driverGrid.Columns[1].ReadOnly = true;
            _driverGrid.Columns.Add("Title", "Driver");
            _driverGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _driverGrid.Columns[2].ReadOnly = true;
            _driverGrid.Columns.Add("Comparison", "Instalada → disponível");
            _driverGrid.Columns[3].Width = 165;
            _driverGrid.Columns[3].ReadOnly = true;
            _driverGrid.Columns.Add("Size", "Download");
            _driverGrid.Columns[4].Width = 90;
            _driverGrid.Columns[4].ReadOnly = true;
            _driverGrid.Columns.Add("Restart", "Reinício");
            _driverGrid.Columns[5].Width = 75;
            _driverGrid.Columns[5].ReadOnly = true;
            _driverGrid.Columns.Add(new DataGridViewLinkColumn { Name = "OfficialSite", HeaderText = "Fabricante", Width = 100, TrackVisitedState = false, LinkColor = Theme.Primary, ActiveLinkColor = Theme.Text, VisitedLinkColor = Theme.Primary });
            _driverGrid.Columns.Add(new DataGridViewLinkColumn { Name = "CatalogSite", HeaderText = "Hardware ID", Width = 100, TrackVisitedState = false, LinkColor = Theme.Primary, ActiveLinkColor = Theme.Text, VisitedLinkColor = Theme.Primary });
            _driverGrid.Columns.Add("UpdateId", "ID");
            _driverGrid.Columns[8].Visible = false;
            _driverGrid.Columns.Add("SupportUrl", "Endereço oficial");
            _driverGrid.Columns[9].Visible = false;
            _driverGrid.Columns.Add("CatalogUrl", "Catálogo exato");
            _driverGrid.Columns[10].Visible = false;
            _driverGrid.Columns.Add("IsFirmware", "Firmware");
            _driverGrid.Columns[11].Visible = false;
            for (int i = 8; i < _driverGrid.Columns.Count; i++) _driverGrid.Columns[i].ReadOnly = true;
            _driverGrid.CellContentClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0) return;
                string column = _driverGrid.Columns[e.ColumnIndex].Name;
                if (column != "OfficialSite" && column != "CatalogSite") return;
                string url = Convert.ToString(_driverGrid.Rows[e.RowIndex].Cells[column == "OfficialSite" ? "SupportUrl" : "CatalogUrl"].Value);
                try { DriverManager.OpenOfficialSupport(url); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Site oficial", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };

            var search = ButtonFactory("Buscar updates", 20, 554, 140, Theme.Primary);
            var selectAll = ButtonFactory("Selecionar", 170, 554, 105, Theme.Secondary);
            var install = ButtonFactory("Atualizar", 285, 554, 125, Theme.Success);
            var backup = ButtonFactory("Criar backup", 420, 554, 130, Theme.Secondary);
            var restore = ButtonFactory("Restaurar", 560, 554, 120, Theme.Warning);
            var backups = ButtonFactory("Backups", 690, 554, 105, Theme.Secondary);
            var windowsUpdate = ButtonFactory("Windows Update", 805, 554, 145, Theme.Secondary);
            search.Click += async delegate { await SearchDriverUpdates(); };
            selectAll.Click += delegate { foreach (DataGridViewRow row in _driverGrid.Rows) if (!row.IsNewRow) row.Cells["Selected"].Value = true; };
            install.Click += async delegate { await InstallSelectedDrivers(); };
            backup.Click += async delegate { await CreateDriverBackup(); };
            restore.Click += async delegate { await RestoreDriverBackup(); };
            backups.Click += delegate { DriverManager.OpenDriverBackups(); };
            windowsUpdate.Click += delegate { DriverManager.OpenWindowsUpdate(); };
            page.Controls.Add(_driverInventorySummary);
            page.Controls.Add(_driverFilter);
            page.Controls.Add(_driverSearch);
            page.Controls.Add(_driverProblemsOnly);
            page.Controls.Add(_installedDriverGrid);
            page.Controls.Add(_driverSummary);
            page.Controls.Add(_driverGrid);
            page.Controls.Add(search);
            page.Controls.Add(selectAll);
            page.Controls.Add(install);
            page.Controls.Add(backup);
            page.Controls.Add(restore);
            page.Controls.Add(backups);
            page.Controls.Add(windowsUpdate);
            page.Resize += delegate
            {
                int width = Math.Max(600, page.ClientSize.Width - 40);
                int y = Math.Max(520, page.ClientSize.Height - 50);
                int installedHeight = Math.Max(150, (y - 120) / 2);
                _installedDriverGrid.Size = new Size(width, installedHeight);
                _driverSummary.Location = new Point(20, _installedDriverGrid.Bottom + 12);
                _driverGrid.Location = new Point(20, _driverSummary.Bottom + 2);
                _driverGrid.Size = new Size(width, Math.Max(145, y - _driverGrid.Top - 12));
                search.Location = new Point(20, y);
                selectAll.Location = new Point(170, y);
                install.Location = new Point(285, y);
                backup.Location = new Point(420, y);
                restore.Location = new Point(560, y);
                backups.Location = new Point(690, y);
                windowsUpdate.Location = new Point(805, y);
            };
            page.Enter += async delegate { await LoadDriverInventoryAsync(false); };
            return page;
        }

        private TabPage BuildProgramUpdatesTab()
        {
            var page = NewPage("Programas");
            _programUpdateSummary = new Label { Text = "Atualizações de programas • abra esta área para verificar", Location = new Point(20, 18), Size = new Size(700, 28), ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11f) };
            page.Controls.Add(new Label { Text = "Pesquisar", Location = new Point(20, 59), AutoSize = true, ForeColor = Theme.Muted });
            _programUpdateSearch = new TextBox { Location = new Point(91, 55), Size = new Size(300, 26), BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            _programUpdateSearch.TextChanged += delegate { ApplyProgramUpdateFilter(); };

            _programUpdateGrid = Grid(20, 96, 1000, 460);
            _programUpdateGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Atualizar", Width = 70 });
            _programUpdateGrid.Columns.Add("Name", "Programa");
            _programUpdateGrid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _programUpdateGrid.Columns.Add("Installed", "Instalada");
            _programUpdateGrid.Columns[2].Width = 145;
            _programUpdateGrid.Columns.Add("Available", "Disponível");
            _programUpdateGrid.Columns[3].Width = 145;
            _programUpdateGrid.Columns.Add("PackageId", "Identificador WinGet");
            _programUpdateGrid.Columns[4].Width = 260;
            _programUpdateGrid.Columns.Add("Source", "Origem");
            _programUpdateGrid.Columns[5].Width = 90;
            for (int index = 1; index < _programUpdateGrid.Columns.Count; index++) _programUpdateGrid.Columns[index].ReadOnly = true;

            var refresh = ButtonFactory("Buscar atualizações", 20, 574, 170, Theme.Secondary);
            var selectAll = ButtonFactory("Selecionar todos", 202, 574, 155, Theme.Secondary);
            var install = ButtonFactory("Atualizar selecionados", 369, 574, 190, Theme.Primary);
            var help = ButtonFactory("Sobre o WinGet", 571, 574, 145, Theme.Secondary);
            refresh.Click += async delegate { await SearchProgramUpdates(); };
            selectAll.Click += delegate
            {
                SyncProgramUpdateSelection();
                bool select = _programUpdates.Any(item => !item.Selected);
                foreach (ProgramUpdate item in _programUpdates) item.Selected = select;
                ApplyProgramUpdateFilter();
            };
            install.Click += async delegate { await InstallSelectedPrograms(); };
            help.Click += delegate { Process.Start(new ProcessStartInfo("https://learn.microsoft.com/windows/package-manager/winget/") { UseShellExecute = true }); };

            page.Controls.Add(_programUpdateSummary);
            page.Controls.Add(_programUpdateSearch);
            page.Controls.Add(_programUpdateGrid);
            page.Controls.Add(refresh);
            page.Controls.Add(selectAll);
            page.Controls.Add(install);
            page.Controls.Add(help);
            page.Resize += delegate
            {
                int y = Math.Max(574, page.ClientSize.Height - 55);
                _programUpdateGrid.Size = new Size(Math.Max(760, page.ClientSize.Width - 40), Math.Max(300, y - 108));
                refresh.Location = new Point(20, y);
                selectAll.Location = new Point(202, y);
                install.Location = new Point(369, y);
                help.Location = new Point(571, y);
            };
            return page;
        }

        private async Task SearchProgramUpdates()
        {
            bool available = await Task.Run(delegate { return ProgramUpdater.IsAvailable(); });
            if (!available)
            {
                _programUpdateSummary.Text = "WinGet não está disponível neste Windows";
                MessageBox.Show(this, "O Windows Package Manager (WinGet) não foi encontrado. Instale ou atualize o App Installer pela Microsoft Store.", "Atualização de programas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            List<ProgramUpdate> found = null;
            string result = await RunWork("Buscando atualizações de programas...", delegate(CancellationToken token, IProgress<string> progress)
            {
                found = ProgramUpdater.SearchUpdates(token, progress);
                return found.Count == 0 ? "Todos os programas consultados estão atualizados." : found.Count + " atualizações de programas encontradas.";
            }, false);
            if (found == null)
            {
                _programUpdateSummary.Text = "Não foi possível consultar o WinGet";
                MessageBox.Show(this, result, "Atualização de programas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _programUpdates = found;
            _programUpdatesLoaded = true;
            ApplyProgramUpdateFilter();
            string version = await Task.Run(delegate { return ProgramUpdater.ReadVersion(); });
            _programUpdateSummary.Text = found.Count == 0 ? "Seus programas estão atualizados" : found.Count + (found.Count == 1 ? " atualização disponível" : " atualizações disponíveis");
            if (!string.IsNullOrEmpty(version)) _programUpdateSummary.Text += " • WinGet " + version.TrimStart('v', 'V');
        }

        private void ApplyProgramUpdateFilter()
        {
            if (_programUpdateGrid == null) return;
            SyncProgramUpdateSelection();
            string search = _programUpdateSearch == null ? string.Empty : _programUpdateSearch.Text.Trim();
            IEnumerable<ProgramUpdate> visible = _programUpdates.Where(delegate(ProgramUpdate item)
            {
                return string.IsNullOrEmpty(search) || (item.Name + " " + item.PackageId + " " + item.InstalledVersion + " " + item.AvailableVersion).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            });
            _programUpdateGrid.Rows.Clear();
            foreach (ProgramUpdate item in visible)
                _programUpdateGrid.Rows.Add(item.Selected, item.Name, item.InstalledVersion, item.AvailableVersion, item.PackageId, item.Source);
        }

        private void SyncProgramUpdateSelection()
        {
            if (_programUpdateGrid == null) return;
            foreach (DataGridViewRow row in _programUpdateGrid.Rows)
            {
                if (row.IsNewRow) continue;
                string id = Convert.ToString(row.Cells["PackageId"].Value);
                ProgramUpdate item = _programUpdates.FirstOrDefault(update => string.Equals(update.PackageId, id, StringComparison.OrdinalIgnoreCase));
                if (item != null) item.Selected = Convert.ToBoolean(row.Cells["Selected"].Value);
            }
        }

        private async Task InstallSelectedPrograms()
        {
            SyncProgramUpdateSelection();
            var ids = _programUpdates.Where(item => item.Selected).Select(item => item.PackageId).ToList();
            if (ids.Count == 0)
            {
                MessageBox.Show(this, "Selecione ao menos um programa.", "Atualização de programas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<ProgramUpdate> selected = _programUpdates.Where(item => ids.Contains(item.PackageId, StringComparer.OrdinalIgnoreCase)).ToList();
            string preview = string.Join("\r\n", selected.Take(8).Select(item => "• " + item.Name + ": " + item.InstalledVersion + " → " + item.AvailableVersion));
            if (selected.Count > 8) preview += "\r\n• e mais " + (selected.Count - 8);
            string confirmation = "Atualizar " + selected.Count + (selected.Count == 1 ? " programa" : " programas") + " pelo WinGet?\r\n\r\n" + preview + "\r\n\r\nAlguns instaladores podem solicitar permissão do Windows.";
            if (MessageBox.Show(this, confirmation, "Confirmar atualizações", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string result = await RunWork("Atualizando programas...", delegate(CancellationToken token, IProgress<string> progress) { return ProgramUpdater.InstallUpdates(ids, token, progress); });
            ShowTextDialog("Resultado das atualizações", result);
            _programUpdatesLoaded = false;
            await SearchProgramUpdates();
        }

        private async Task LoadDriverInventoryAsync(bool force)
        {
            if (_driverInventoryLoaded && !force) return;
            _driverInventoryLoaded = true;
            _driverInventorySummary.Text = "Versões instaladas • lendo vídeo, BIOS, chipset e dispositivos...";
            List<DriverInventoryItem> items = await Task.Run(delegate { return DriverManager.ReadInstalledDrivers(); });
            if (IsDisposed) return;
            _driverInventoryItems = items;
            ApplyDriverInventoryFilter();
            int categories = items.Select(delegate(DriverInventoryItem item) { return item.Category; }).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            int problems = items.Count(delegate(DriverInventoryItem item) { return item.HasProblem; });
            _driverInventorySummary.Text = items.Count == 0 ? "Não foi possível ler as versões instaladas" : items.Count + " drivers importantes • " + categories + " categorias" + (problems == 0 ? " • nenhum problema" : " • " + problems + " problemas");
        }

        private void ApplyDriverInventoryFilter()
        {
            if (_installedDriverGrid == null) return;
            string category = _driverFilter == null || _driverFilter.SelectedIndex <= 0 ? string.Empty : Convert.ToString(_driverFilter.SelectedItem);
            string search = _driverSearch == null ? string.Empty : _driverSearch.Text.Trim();
            bool problemsOnly = _driverProblemsOnly != null && _driverProblemsOnly.Checked;
            IEnumerable<DriverInventoryItem> visible = _driverInventoryItems.Where(delegate(DriverInventoryItem item)
            {
                if (!string.IsNullOrEmpty(category) && !string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase)) return false;
                if (problemsOnly && !item.HasProblem) return false;
                return string.IsNullOrEmpty(search) || (item.Category + " " + item.Device + " " + item.Provider + " " + item.Version + " " + item.InfName).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            });
            _installedDriverGrid.Rows.Clear();
            foreach (DriverInventoryItem item in visible)
            {
                int index = _installedDriverGrid.Rows.Add(item.Category, item.Device, item.Provider, item.Version, item.Date, item.Status, item.InfName);
                if (item.HasProblem) _installedDriverGrid.Rows[index].DefaultCellStyle.ForeColor = Theme.Warning;
            }
        }

        private async Task SearchDriverUpdates()
        {
            List<DriverUpdate> found = null;
            string result = await RunWork("Buscando drivers no Windows Update...", delegate(CancellationToken token, IProgress<string> progress)
            {
                found = DriverManager.SearchUpdates(token, progress);
                return DriverManager.BuildSearchReport(found);
            }, false);
            if (found == null)
            {
                MessageBox.Show(this, result, "Drivers", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _driverUpdates = found;
            _driverGrid.Rows.Clear();
            foreach (DriverUpdate update in found)
            {
                int index = _driverGrid.Rows.Add(update.Selected, update.Classification, update.Title, update.Comparison, V2Engine.FormatBytes(update.DownloadBytes), update.RebootRequired ? "Sim" : "Não", update.SupportName, "Catálogo", update.UpdateId, update.SupportUrl, update.CatalogUrl, update.IsFirmware);
                if (update.IsOlderRisk) _driverGrid.Rows[index].DefaultCellStyle.ForeColor = Theme.Warning;
            }
            int recommended = found.Count(delegate(DriverUpdate item) { return item.Classification == "Recomendada" || item.Classification == "Obrigatória"; });
            int firmware = found.Count(delegate(DriverUpdate item) { return item.IsFirmware; });
            _driverSummary.Text = found.Count == 0 ? "Seus drivers estão atualizados" : found.Count + " atualizações • " + recommended + " recomendadas" + (firmware == 0 ? string.Empty : " • " + firmware + " firmware/BIOS");
        }

        private async Task InstallSelectedDrivers()
        {
            var ids = new List<string>();
            foreach (DataGridViewRow row in _driverGrid.Rows)
                if (!row.IsNewRow && Convert.ToBoolean(row.Cells["Selected"].Value)) ids.Add(Convert.ToString(row.Cells["UpdateId"].Value));
            if (ids.Count == 0)
            {
                MessageBox.Show(this, "Selecione ao menos um driver.", "Drivers", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<DriverUpdate> selected = _driverUpdates.Where(delegate(DriverUpdate item) { return ids.Contains(item.UpdateId, StringComparer.OrdinalIgnoreCase); }).ToList();
            if (!Optimizer.IsAdministrator())
            {
                if (MessageBox.Show(this, "A instalação exige privilégios de administrador. Reabrir o Otimizador como administrador?", "Drivers", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) RunAsAdmin(null, EventArgs.Empty);
                return;
            }
            DriverSafetyStatus safety = await Task.Run(delegate { return DriverManager.ReadSafetyStatus(); });
            string firmwareBlock = DriverManager.ValidateFirmwareSelection(selected, safety);
            if (!string.IsNullOrEmpty(firmwareBlock))
            {
                MessageBox.Show(this, firmwareBlock, "Proteção de BIOS e firmware", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            bool older = selected.Any(delegate(DriverUpdate item) { return item.IsOlderRisk; });
            string warning = older ? "\r\n\r\nA seleção contém um pacote possivelmente mais antigo." : string.Empty;
            string firmwareInfo = selected.Any(delegate(DriverUpdate item) { return item.IsFirmware; }) ? "\r\n\r\nVerificação de firmware:\r\n" + safety.Summary : string.Empty;
            string confirmation = "Será criado um backup dos drivers atuais antes da instalação.\r\n\r\nO Windows Update instalará " + ids.Count + (ids.Count == 1 ? " driver." : " drivers.") + warning + firmwareInfo + "\r\n\r\nContinuar?";
            if (MessageBox.Show(this, confirmation, "Atualizar drivers", MessageBoxButtons.YesNo, older ? MessageBoxIcon.Warning : MessageBoxIcon.Question) != DialogResult.Yes) return;
            string result = await RunWork("Atualizando drivers...", delegate(CancellationToken token, IProgress<string> progress) { return DriverManager.InstallUpdates(ids, token, progress); });
            MessageBox.Show(this, result, "Drivers", MessageBoxButtons.OK, result.IndexOf("Falha", StringComparison.OrdinalIgnoreCase) >= 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            await LoadDriverInventoryAsync(true);
            await SearchDriverUpdates();
        }

        private async Task CreateDriverBackup()
        {
            if (!Optimizer.IsAdministrator())
            {
                if (MessageBox.Show(this, "O backup exige administrador. Reabrir agora?", "Backup de drivers", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) RunAsAdmin(null, EventArgs.Empty);
                return;
            }
            string result = await RunWork("Criando backup dos drivers...", delegate(CancellationToken token, IProgress<string> progress) { return DriverManager.CreateDriverBackup(token, progress); });
            MessageBox.Show(this, result, "Backup de drivers", MessageBoxButtons.OK, result.StartsWith("Falha", StringComparison.OrdinalIgnoreCase) ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private async Task RestoreDriverBackup()
        {
            if (!Optimizer.IsAdministrator())
            {
                if (MessageBox.Show(this, "A restauração exige administrador. Reabrir agora?", "Restaurar drivers", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) RunAsAdmin(null, EventArgs.Empty);
                return;
            }
            if (MessageBox.Show(this, "Reaplicar o backup de drivers mais recente? O Windows manterá o pacote com melhor classificação para cada dispositivo.", "Restaurar drivers", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            string result = await RunWork("Restaurando drivers...", delegate(CancellationToken token, IProgress<string> progress) { return DriverManager.RestoreLatestDriverBackup(token, progress); });
            MessageBox.Show(this, result, "Restaurar drivers", MessageBoxButtons.OK, result.IndexOf("não concluído", StringComparison.OrdinalIgnoreCase) >= 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            await LoadDriverInventoryAsync(true);
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
                    V2Engine.SaveReport(result);
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
                    V2Engine.SaveReport(result);
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

        private async Task LoadStartupAsync()
        {
            if (_startupGrid == null || _startupLoading) return;
            _startupLoading = true;
            try
            {
                List<StartupEntry> entries = await Task.Run(delegate { return V2Engine.ReadStartupEntries(); });
                if (IsDisposed) return;
                _startupGrid.Rows.Clear();
                foreach (StartupEntry item in entries)
                {
                    int index = _startupGrid.Rows.Add(item.Enabled, item.Name, item.Source, item.Impact, item.Command, item.OriginalEnabled, item.CanChange, item.RegistryHive, item.RegistryPath, item.ApprovalPath, item.ValueName, item.StateKind);
                    DataGridViewRow row = _startupGrid.Rows[index];
                    row.Cells["Enabled"].ReadOnly = !item.CanChange;
                    if (!item.CanChange)
                    {
                        row.Cells["Enabled"].ToolTipText = "Esta entrada é controlada pelo Windows, por política ou exige administrador.";
                        row.DefaultCellStyle.ForeColor = Theme.Muted;
                    }
                }
            }
            finally { _startupLoading = false; }
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
                entries.Add(new StartupEntry
                {
                    Enabled = enabled,
                    OriginalEnabled = original,
                    CanChange = Convert.ToBoolean(row.Cells["CanChange"].Value),
                    Name = name,
                    Source = Convert.ToString(row.Cells["Source"].Value),
                    Command = Convert.ToString(row.Cells["Command"].Value),
                    Impact = Convert.ToString(row.Cells["Impact"].Value),
                    RegistryHive = Convert.ToString(row.Cells["RegistryHive"].Value),
                    RegistryPath = Convert.ToString(row.Cells["RegistryPath"].Value),
                    ApprovalPath = Convert.ToString(row.Cells["ApprovalPath"].Value),
                    ValueName = Convert.ToString(row.Cells["ValueName"].Value),
                    StateKind = Convert.ToString(row.Cells["StateKind"].Value)
                });
            }
            await RunWork("Atualizando inicialização...", delegate(CancellationToken t, IProgress<string> p) { return V2Engine.ApplyStartupEntries(entries, t, p); });
            await LoadStartupAsync();
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
            try { Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--wait-for-instance") { UseShellExecute = true, Verb = "runas" }); Close(); }
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
                _trayIcon.BalloonTipText = "A manutenção segura foi finalizada.";
                _trayIcon.ShowBalloonTip(4000);
            };
            exit.Click += delegate { _trayIcon.Visible = false; Close(); };
            menu.Items.Add(open);
            menu.Items.Add(maintenance);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);
            _trayIcon = new NotifyIcon { Icon = Icon ?? SystemIcons.Application, Text = "Otimizador de Desempenho", ContextMenuStrip = menu, Visible = false };
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
                NativeWindowTheme.Apply(dialog);
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
            return new TabPage(text) { BackColor = Theme.Background, ForeColor = Theme.Text, AccessibleName = "Aba " + text, AutoScroll = true };
        }

        private DashboardPanel DashboardCard(int x, int y, int width, int height)
        {
            return new DashboardPanel { Location = new Point(x, y), Size = new Size(width, height), BackColor = Theme.Surface, BorderColor = Theme.Border, Radius = 14 };
        }

        private DashboardPanel MetricCard(string title, int x, int y, out Label value, out Label detail, out ModernProgressBar gauge, out SparklineChart chart)
        {
            var card = DashboardCard(x, y, 324, 112);
            card.Controls.Add(new Label { Text = title, Location = new Point(18, 9), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8.3f) });
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
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeight = 38,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                EnableHeadersVisualStyles = false,
                AccessibleName = "Tabela de dados"
            };
            grid.RowTemplate.Height = 34;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Surface;
            grid.DefaultCellStyle.BackColor = Theme.SurfaceDark;
            grid.DefaultCellStyle.ForeColor = Theme.Text;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(23, 83, 112);
            grid.DefaultCellStyle.SelectionForeColor = Theme.Text;
            return grid;
        }

        private static Button ButtonFactory(string text, int x, int y, int width, Color color)
        {
            var button = new ModernButton { Text = text, Location = new Point(x, y), Size = new Size(width, 38), BackColor = color, BaseColor = color, ForeColor = Theme.ButtonText, FlatStyle = FlatStyle.Flat, AccessibleName = text, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }
}
