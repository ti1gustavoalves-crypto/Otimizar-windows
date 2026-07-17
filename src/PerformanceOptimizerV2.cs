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
        private enum AppSection
        {
            Dashboard,
            Diagnostics,
            Storage,
            Startup,
            Updates,
            Hardware,
            Settings
        }

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
        private TextBox _startupSearch;
        private ComboBox _startupFilter;
        private Button _startupApplyButton;
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
        private Button _deleteStorageItem;
        private Label _storageSelectionStatus;
        private TextBox _storageSearch;
        private ToolTip _toolTip;
        private ComboBox _schedule;
        private Label _maintenanceResult;
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
            _toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 350, ReshowDelay = 100 };

            _tabs = new TabControl { Location = new Point(-4, -28), SizeMode = TabSizeMode.Fixed, ItemSize = new Size(1, 24), Appearance = TabAppearance.FlatButtons };
            _tabs.TabPages.Add(BuildDashboard());
            _tabs.TabPages.Add(BuildDiagnosticsTab());
            _tabs.TabPages.Add(BuildStorageTab());
            _tabs.TabPages.Add(BuildStartupTab());
            _tabs.TabPages.Add(BuildUpdatesTab());
            _tabs.TabPages.Add(BuildHardwareTab());
            _tabs.TabPages.Add(BuildSettingsTab());
            _tabs.SelectedIndexChanged += async delegate
            {
                UpdateNavigationState();
                if (_tabs.SelectedIndex == (int)AppSection.Updates) await LoadDriverInventoryAsync(false);
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
            NativeWindowTheme.ApplyTree(_progress);
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
                _toolTip.Dispose();
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

            string[] labels = { "Início", "Diagnóstico", "Armazenamento", "Inicialização", "Atualizações", "Hardware", "Ajustes" };
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
            var analyze = ButtonFactory("Atualizar diagnóstico", 446, 103, 172, Theme.Secondary);
            analyze.Size = new Size(172, 30);
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
            var apply = ButtonFactory("Executar otimização", 20, 98, 320, Theme.Primary);
            apply.Size = new Size(320, 31);
            profileCard.Controls.Add(_profile);
            profileCard.Controls.Add(_profileDescription);
            profileCard.Controls.Add(apply);
            UpdateProfileDescription();

            var memoryCard = MetricCard("Memória disponível", 20, 176, out _memoryValue, out _memoryDetail, out _memoryGauge, out _memoryChart);
            var diskCard = MetricCard("Espaço no disco C:", 356, 176, out _diskValue, out _diskDetail, out _diskGauge, out _diskChart);
            var cpuCard = MetricCard("Uso do processador", 692, 176, out _cpuValue, out _cpuDetail, out _cpuGauge, out _cpuChart);

            var optionsCard = DashboardCard(20, 306, 1016, 174);
            optionsCard.Controls.Add(new Label { Text = "Otimização recomendada", Location = new Point(20, 16), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11.5f) });
            optionsCard.Controls.Add(new Label { Text = "Revise somente se precisar personalizar o atendimento", Location = new Point(20, 41), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) });

            _dark = Option("Modo escuro", 20, 82, true);
            _visuals = Option("Reduzir animações", 20, 122, true);
            _startup = Option("Otimizar inicialização", 350, 82, true);
            _backgroundEfficiency = Option("Reduzir segundo plano", 350, 122, true);
            _cleanup = Option("Limpar temporários", 680, 82, true);
            _restorePoint = Option("Criar ponto de restauração", 680, 122, true);
            optionsCard.Controls.Add(_dark);
            optionsCard.Controls.Add(_visuals);
            optionsCard.Controls.Add(_restorePoint);
            optionsCard.Controls.Add(_startup);
            optionsCard.Controls.Add(_cleanup);
            optionsCard.Controls.Add(_backgroundEfficiency);

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
                AttachClick(processCard, delegate { _tabs.SelectedIndex = (int)AppSection.Diagnostics; });
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
            page.Controls.Add(healthCard);
            page.Controls.Add(profileCard);
            page.Controls.Add(memoryCard);
            page.Controls.Add(diskCard);
            page.Controls.Add(cpuCard);
            page.Controls.Add(optionsCard);
            page.Controls.Add(activityCard);
            page.Resize += delegate
            {
                int left = Math.Max(20, (page.ClientSize.Width - 1016) / 2);
                healthCard.Left = left;
                profileCard.Left = left + 656;
                memoryCard.Left = left;
                diskCard.Left = left + 336;
                cpuCard.Left = left + 672;
                optionsCard.Left = left;
                activityCard.Left = left;
            };
            return page;
        }

        private TabPage BuildHardwareTab()
        {
            var page = NewPage("Hardware");
            _hardwareSummary = new Label { Text = "Componentes principais", AutoSize = false, AutoEllipsis = true, Size = new Size(1000, 32), Location = new Point(20, 20), ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 10.5f) };

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
            page.Controls.Add(_hardwareCards);
            page.Resize += delegate { _hardwareSummary.Size = new Size(Math.Max(500, page.ClientSize.Width - 40), 32); };
            page.Enter += async delegate { if (!_hardwareLoaded && _cts == null) await LoadHardware(false); };
            return page;
        }

        private TabPage BuildStartupTab()
        {
            var page = NewPage("Inicialização");
            var title = new Label { Text = "Aplicativos que abrem com o Windows", AutoSize = false, AutoEllipsis = true, Size = new Size(350, 28), Location = new Point(20, 19), ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11f) };
            page.Controls.Add(title);
            _startupSearch = new TextBox { Location = new Point(390, 14), Size = new Size(260, 27), BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Pesquisar aplicativos de inicialização" };
            _startupFilter = new ComboBox { Location = new Point(662, 14), Size = new Size(190, 28), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text };
            _startupFilter.Items.AddRange(new object[] { "Todos — ativos primeiro", "Somente ativos", "Alto impacto", "Somente alteráveis", "Não alteráveis" });
            _startupFilter.SelectedIndex = 0;
            NativeWindowTheme.SetCueBanner(_startupSearch, "Pesquisar aplicativos");
            _startupSearch.TextChanged += delegate { ApplyStartupFilter(); };
            _startupFilter.SelectedIndexChanged += delegate { ApplyStartupFilter(); };
            _startupGrid = Grid(20, 56, 1000, 480);
            _startupGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "Ativo", Width = 65 });
            _startupGrid.Columns.Add("Name", "Programa");
            _startupGrid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
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
            _startupGrid.Columns[4].Visible = false;
            _startupGrid.Columns.Add("Original", "Original");
            _startupGrid.Columns.Add("CanChange", "Editável");
            _startupGrid.Columns.Add("RegistryHive", "Hive");
            _startupGrid.Columns.Add("RegistryPath", "Registro");
            _startupGrid.Columns.Add("ApprovalPath", "Aprovação");
            _startupGrid.Columns.Add("ValueName", "Valor");
            _startupGrid.Columns.Add("StateKind", "Tipo");
            for (int i = 5; i < _startupGrid.Columns.Count; i++) { _startupGrid.Columns[i].Visible = false; _startupGrid.Columns[i].ReadOnly = true; }
            var refresh = ButtonFactory("Atualizar", 20, 545, 125, Theme.Secondary);
            _startupApplyButton = ButtonFactory("Nenhuma alteração", 157, 545, 190, Theme.Primary);
            _startupApplyButton.Enabled = false;
            refresh.Click += async delegate { await LoadStartupAsync(); };
            _startupApplyButton.Click += async delegate { await ApplyStartupGrid(); };
            _startupGrid.CurrentCellDirtyStateChanged += delegate { if (_startupGrid.IsCurrentCellDirty) _startupGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _startupGrid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e) { if (!_startupLoading && e.RowIndex >= 0 && _startupGrid.Columns[e.ColumnIndex].Name == "Enabled") UpdateStartupChangeCount(); };
            _startupGrid.CellToolTipTextNeeded += delegate(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
            {
                if (e.RowIndex >= 0 && _startupGrid.Columns[e.ColumnIndex].Name == "Name")
                    e.ToolTipText = Convert.ToString(_startupGrid.Rows[e.RowIndex].Cells["Command"].Value);
            };
            page.Controls.Add(_startupSearch);
            page.Controls.Add(_startupFilter);
            page.Controls.Add(_startupGrid);
            page.Controls.Add(refresh);
            page.Controls.Add(_startupApplyButton);
            _startupGrid.Anchor = AnchorStyles.None;
            page.Resize += delegate { LayoutStartupTab(page, title, refresh, _startupApplyButton); };
            LayoutStartupTab(page, title, refresh, _startupApplyButton);
            page.Enter += async delegate { await LoadStartupAsync(); };
            return page;
        }

        private void LayoutStartupTab(TabPage page, Label title, Button refresh, Button save)
        {
            int width = Math.Max(500, page.ClientSize.Width - 40);
            int buttonY = Math.Max(260, page.ClientSize.Height - 50);
            _startupFilter.Location = new Point(Math.Max(610, page.ClientSize.Width - 210), 14);
            _startupSearch.Location = new Point(_startupFilter.Left - 272, 14);
            title.Size = new Size(Math.Max(280, _startupSearch.Left - 40), 28);
            _startupGrid.Location = new Point(20, 56);
            _startupGrid.Size = new Size(width, Math.Max(180, buttonY - _startupGrid.Top - 12));
            refresh.Location = new Point(20, buttonY);
            save.Location = new Point(157, buttonY);
        }

        private TabPage BuildStorageTab()
        {
            var page = NewPage("Armazenamento");
            _storageSummary = new Label { Text = "Discos e volumes", AutoSize = false, Size = new Size(520, 30), Location = new Point(20, 20), ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 10.5f) };
            var scan = ButtonFactory("Analisar pastas", 300, 12, 120, Theme.Primary);
            var largeFiles = ButtonFactory("Arquivos grandes", 430, 12, 125, Theme.Secondary);
            var duplicates = ButtonFactory("Duplicados", 565, 12, 100, Theme.Secondary);
            var clean = ButtonFactory("Limpeza segura", 675, 12, 120, Theme.Warning);
            var optimize = ButtonFactory("Otimizar disco", 805, 12, 120, Theme.Success);
            var tools = ButtonFactory("Mais", 935, 12, 80, Theme.Secondary);
            var toolsMenu = new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Text, ShowImageMargin = false };
            var advancedCleanup = new ToolStripMenuItem("Limpeza avançada");
            var componentsCleanup = new ToolStripMenuItem("Limpar componentes do Windows");
            var storageSense = new ToolStripMenuItem("Abrir Sensor de Armazenamento");
            advancedCleanup.Click += async delegate { await AdvancedCleanup(); };
            componentsCleanup.Click += async delegate
            {
                if (MessageBox.Show(this, "Remover componentes substituídos do Windows? O modo agressivo ResetBase não será usado.", "Componentes do Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await RunWork("Limpando componentes do Windows...", delegate(CancellationToken t, IProgress<string> p) { return WindowsMaintenance.CleanupComponentStore(t, p); });
                LoadVolumes();
            };
            storageSense.Click += delegate { WindowsMaintenance.OpenStorageSenseSettings(); };
            toolsMenu.Items.Add(advancedCleanup);
            toolsMenu.Items.Add(componentsCleanup);
            toolsMenu.Items.Add(storageSense);
            tools.Click += delegate { toolsMenu.Show(tools, new Point(0, tools.Height)); };

            _volumeGrid = Grid(20, 58, 1000, 120);
            _volumeGrid.Columns.Add("Drive", "Disco");
            _volumeGrid.Columns[0].Width = 80;
            _volumeGrid.Columns.Add("Label", "Nome");
            _volumeGrid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
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

            _folderSummary = new Label { Text = "Selecione um disco e escolha uma análise", AutoSize = false, AutoEllipsis = true, Size = new Size(490, 28), Location = new Point(20, 194), ForeColor = Theme.Muted };
            _storageSearch = new TextBox { Location = new Point(520, 188), Size = new Size(210, 27), BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Pesquisar resultados do armazenamento" };
            NativeWindowTheme.SetCueBanner(_storageSearch, "Pesquisar resultados");
            _storageSearch.TextChanged += delegate { ApplyStorageFilter(); };
            _deleteStorageItem = ButtonFactory("Mover para a Lixeira", 836, 184, 180, Theme.Warning);
            _deleteStorageItem.Enabled = false;
            _deleteStorageItem.Visible = false;
            _storageSelectionStatus = new Label { Text = "Protegido pelo sistema", Location = new Point(836, 192), Size = new Size(180, 24), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Warning, Visible = false, AutoEllipsis = true };
            _storageGrid = Grid(20, 228, 1000, 359);
            _storageGrid.Columns.Add("Path", "Arquivo ou pasta");
            _storageGrid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _storageGrid.Columns.Add("Logical", "Tamanho");
            _storageGrid.Columns[1].Width = 130;
            _storageGrid.Columns.Add("Details", "Detalhes");
            _storageGrid.Columns[2].Width = 220;
            _storageGrid.ReadOnly = true;
            _storageGrid.MultiSelect = false;
            _storageGrid.SelectionChanged += delegate { UpdateStorageSelection(); };
            _storageGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) OpenStorageItemLocation(Convert.ToString(_storageGrid.Rows[e.RowIndex].Cells["Path"].Value)); };

            scan.Click += async delegate { await ScanSelectedVolume(); };
            largeFiles.Click += async delegate { await ScanLargeFiles(); };
            clean.Click += async delegate { await OpenSafeCleanup(); };
            duplicates.Click += async delegate { await ScanDuplicates(); };
            optimize.Click += async delegate { await OptimizeSelectedVolume(); };
            _deleteStorageItem.Click += async delegate { await DeleteSelectedStorageItem(); };
            page.Controls.Add(_storageSummary);
            page.Controls.Add(_volumeGrid);
            page.Controls.Add(_folderSummary);
            page.Controls.Add(_storageSearch);
            page.Controls.Add(_storageGrid);
            page.Controls.Add(scan);
            page.Controls.Add(largeFiles);
            page.Controls.Add(clean);
            page.Controls.Add(duplicates);
            page.Controls.Add(optimize);
            page.Controls.Add(tools);
            page.Controls.Add(_deleteStorageItem);
            page.Controls.Add(_storageSelectionStatus);
            _volumeGrid.Anchor = AnchorStyles.None;
            _storageGrid.Anchor = AnchorStyles.None;
            page.Resize += delegate { LayoutStorageTab(page, scan, largeFiles, duplicates, clean, optimize, tools); };
            LayoutStorageTab(page, scan, largeFiles, duplicates, clean, optimize, tools);
            page.Enter += delegate { LoadVolumes(); };
            return page;
        }

        private void LayoutStorageTab(TabPage page, Button scan, Button largeFiles, Button duplicates, Button clean, Button optimize, Button tools)
        {
            int width = Math.Max(600, page.ClientSize.Width - 40);
            _volumeGrid.Location = new Point(20, 58);
            _volumeGrid.Size = new Size(width, 120);
            _storageGrid.Location = new Point(20, 228);
            _storageGrid.Size = new Size(width, Math.Max(210, page.ClientSize.Height - _storageGrid.Top - 20));
            int actionsLeft = Math.Max(250, page.ClientSize.Width - 790);
            scan.Location = new Point(actionsLeft, 12);
            largeFiles.Location = new Point(actionsLeft + 130, 12);
            duplicates.Location = new Point(actionsLeft + 265, 12);
            clean.Location = new Point(actionsLeft + 375, 12);
            optimize.Location = new Point(actionsLeft + 505, 12);
            tools.Location = new Point(actionsLeft + 635, 12);
            _deleteStorageItem.Location = new Point(page.ClientSize.Width - 200, 184);
            _storageSelectionStatus.Location = new Point(page.ClientSize.Width - 200, 192);
            _storageSearch.Location = new Point(Math.Max(360, page.ClientSize.Width - 510), 188);
            _storageSearch.Size = new Size(290, 27);
            _folderSummary.Size = new Size(Math.Max(280, _storageSearch.Left - 40), 28);
            _storageSummary.Size = new Size(Math.Max(180, actionsLeft - 40), 30);
        }

        private Panel BuildDriversPanel()
        {
            var page = NewContentPanel("Drivers instalados e disponíveis");
            _driverInventorySummary = new Label { Text = "Drivers instalados • verificando hardware...", Location = new Point(20, 18), Size = new Size(420, 28), AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11f) };
            _driverFilter = new ComboBox { Location = new Point(470, 14), Size = new Size(155, 28), DropDownStyle = ComboBoxStyle.DropDownList };
            _driverFilter.Items.AddRange(new object[] { "Todos", "Vídeo", "BIOS", "Firmware", "Chipset / sistema", "Áudio", "Rede", "Armazenamento", "Bluetooth", "USB", "Problema", "Sem assinatura" });
            _driverFilter.SelectedIndex = 0;
            _driverSearch = new TextBox { Location = new Point(637, 15), Size = new Size(190, 26), BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Pesquisar drivers instalados" };
            _driverProblemsOnly = new CheckBox { Text = "Somente problemas", Location = new Point(841, 16), AutoSize = true, ForeColor = Theme.Muted };
            NativeWindowTheme.SetCueBanner(_driverSearch, "Pesquisar drivers");
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
            _installedDriverGrid.Columns[4].Width = 105;
            _installedDriverGrid.Columns.Add("Status", "Status");
            _installedDriverGrid.Columns[5].Width = 90;
            _installedDriverGrid.Columns.Add("InfName", "Pacote");
            _installedDriverGrid.Columns[6].Width = 95;
            _installedDriverGrid.Columns[6].Visible = false;
            _installedDriverGrid.ReadOnly = true;

            _driverSummary = new Label { Text = "Atualizações disponíveis • verificação ainda não executada", Location = new Point(20, 281), Size = new Size(760, 28), AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11f) };
            _driverGrid = Grid(20, 311, 1000, 231);
            _driverGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Instalar", Width = 60 });
            _driverGrid.Columns.Add("Classification", "Tipo");
            _driverGrid.Columns[1].Width = 125;
            _driverGrid.Columns[1].ReadOnly = true;
            _driverGrid.Columns.Add("Title", "Driver");
            _driverGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _driverGrid.Columns[2].ReadOnly = true;
            _driverGrid.Columns.Add("Comparison", "Versões");
            _driverGrid.Columns[3].Width = 180;
            _driverGrid.Columns[3].ReadOnly = true;
            _driverGrid.Columns.Add("Size", "Download");
            _driverGrid.Columns[4].Width = 90;
            _driverGrid.Columns[4].ReadOnly = true;
            _driverGrid.Columns.Add("Restart", "Reinício");
            _driverGrid.Columns[5].Width = 75;
            _driverGrid.Columns[5].ReadOnly = true;
            _driverGrid.Columns.Add(new DataGridViewLinkColumn { Name = "OfficialSite", HeaderText = "Fabricante", Width = 100, TrackVisitedState = false, LinkColor = Theme.Primary, ActiveLinkColor = Theme.Text, VisitedLinkColor = Theme.Primary });
            _driverGrid.Columns.Add(new DataGridViewLinkColumn { Name = "CatalogSite", HeaderText = "Catálogo", Width = 90, TrackVisitedState = false, LinkColor = Theme.Primary, ActiveLinkColor = Theme.Text, VisitedLinkColor = Theme.Primary });
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

            var search = ButtonFactory("Verificar atualizações", 20, 554, 185, Theme.Primary);
            var selectAll = ButtonFactory("Selecionar recomendadas", 217, 554, 190, Theme.Secondary);
            var install = ButtonFactory("Instalar selecionadas", 419, 554, 190, Theme.Success);
            var protection = ButtonFactory("Proteção", 621, 554, 135, Theme.Secondary);
            search.Click += async delegate { await SearchDriverUpdates(); };
            selectAll.Click += delegate
            {
                foreach (DataGridViewRow row in _driverGrid.Rows)
                {
                    if (row.IsNewRow) continue;
                    string id = Convert.ToString(row.Cells["UpdateId"].Value);
                    DriverUpdate update = _driverUpdates.FirstOrDefault(item => string.Equals(item.UpdateId, id, StringComparison.OrdinalIgnoreCase));
                    row.Cells["Selected"].Value = update != null && !update.IsFirmware && !update.IsOlderRisk && (update.Classification == "Recomendada" || update.Classification == "Obrigatória");
                }
            };
            install.Click += async delegate { await InstallSelectedDrivers(); };
            var protectionMenu = new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Text, ShowImageMargin = false };
            var backup = new ToolStripMenuItem("Criar backup de drivers");
            var restore = new ToolStripMenuItem("Restaurar backup mais recente");
            var backups = new ToolStripMenuItem("Abrir pasta de backups");
            var windowsUpdate = new ToolStripMenuItem("Abrir Windows Update");
            backup.Click += async delegate { await CreateDriverBackup(); };
            restore.Click += async delegate { await RestoreDriverBackup(); };
            backups.Click += delegate { DriverManager.OpenDriverBackups(); };
            windowsUpdate.Click += delegate { DriverManager.OpenWindowsUpdate(); };
            protectionMenu.Items.Add(backup);
            protectionMenu.Items.Add(restore);
            protectionMenu.Items.Add(backups);
            protectionMenu.Items.Add(new ToolStripSeparator());
            protectionMenu.Items.Add(windowsUpdate);
            protection.Click += delegate { protectionMenu.Show(protection, new Point(0, protection.Height)); };
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
            page.Controls.Add(protection);
            _installedDriverGrid.Anchor = AnchorStyles.None;
            _driverGrid.Anchor = AnchorStyles.None;
            page.Resize += delegate { LayoutDriverPanel(page, search, selectAll, install, protection); };
            LayoutDriverPanel(page, search, selectAll, install, protection);
            return page;
        }

        private void LayoutDriverPanel(Panel page, Button search, Button selectAll, Button install, Button protection)
        {
            int width = Math.Max(720, page.ClientSize.Width - 40);
            int right = page.ClientSize.Width - 20;
            _driverProblemsOnly.Location = new Point(right - 160, 16);
            _driverSearch.Location = new Point(_driverProblemsOnly.Left - 204, 15);
            _driverFilter.Location = new Point(_driverSearch.Left - 167, 14);
            _driverInventorySummary.Size = new Size(Math.Max(260, _driverFilter.Left - 40), 28);

            int buttonY = Math.Max(500, page.ClientSize.Height - 50);
            int availableHeight = Math.Max(390, buttonY - 52);
            int installedHeight = Math.Max(180, Math.Min(250, (availableHeight - 48) * 44 / 100));
            _installedDriverGrid.Location = new Point(20, 52);
            _installedDriverGrid.Size = new Size(width, installedHeight);
            _driverSummary.Location = new Point(20, _installedDriverGrid.Bottom + 10);
            _driverSummary.Size = new Size(width, 28);
            _driverGrid.Location = new Point(20, _driverSummary.Bottom + 2);
            _driverGrid.Size = new Size(width, Math.Max(145, buttonY - _driverGrid.Top - 12));
            search.Location = new Point(20, buttonY);
            selectAll.Location = new Point(217, buttonY);
            install.Location = new Point(419, buttonY);
            protection.Location = new Point(621, buttonY);
        }

        private Panel BuildProgramUpdatesPanel()
        {
            var page = NewContentPanel("Atualizações de aplicativos");
            _programUpdateSummary = new Label { Text = "Aplicativos • verificação ainda não executada", Location = new Point(20, 18), Size = new Size(650, 28), AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11f) };
            _programUpdateSearch = new TextBox { Location = new Point(720, 14), Size = new Size(300, 27), BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Pesquisar aplicativos com atualização" };
            NativeWindowTheme.SetCueBanner(_programUpdateSearch, "Pesquisar aplicativos");
            _programUpdateSearch.TextChanged += delegate { ApplyProgramUpdateFilter(); };

            _programUpdateGrid = Grid(20, 56, 1000, 500);
            _programUpdateGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Atualizar", Width = 70 });
            _programUpdateGrid.Columns.Add("Name", "Programa");
            _programUpdateGrid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _programUpdateGrid.Columns.Add("Installed", "Versão instalada");
            _programUpdateGrid.Columns[2].Width = 145;
            _programUpdateGrid.Columns.Add("Available", "Nova versão");
            _programUpdateGrid.Columns[3].Width = 145;
            _programUpdateGrid.Columns.Add("PackageId", "Identificador WinGet");
            _programUpdateGrid.Columns[4].Width = 260;
            _programUpdateGrid.Columns[4].Visible = false;
            _programUpdateGrid.Columns.Add("Source", "Origem");
            _programUpdateGrid.Columns[5].Width = 90;
            _programUpdateGrid.Columns[5].Visible = false;
            for (int index = 1; index < _programUpdateGrid.Columns.Count; index++) _programUpdateGrid.Columns[index].ReadOnly = true;

            var refresh = ButtonFactory("Verificar atualizações", 20, 574, 185, Theme.Primary);
            var selectAll = ButtonFactory("Selecionar todas", 217, 574, 160, Theme.Secondary);
            var install = ButtonFactory("Atualizar selecionados", 389, 574, 190, Theme.Success);
            refresh.Click += async delegate { await SearchProgramUpdates(); };
            selectAll.Click += delegate
            {
                SyncProgramUpdateSelection();
                foreach (ProgramUpdate item in _programUpdates) item.Selected = true;
                ApplyProgramUpdateFilter();
            };
            install.Click += async delegate { await InstallSelectedPrograms(); };

            page.Controls.Add(_programUpdateSummary);
            page.Controls.Add(_programUpdateSearch);
            page.Controls.Add(_programUpdateGrid);
            page.Controls.Add(refresh);
            page.Controls.Add(selectAll);
            page.Controls.Add(install);
            _programUpdateGrid.Anchor = AnchorStyles.None;
            page.Resize += delegate { LayoutProgramUpdatesPanel(page, refresh, selectAll, install); };
            LayoutProgramUpdatesPanel(page, refresh, selectAll, install);
            return page;
        }

        private void LayoutProgramUpdatesPanel(Panel page, Button refresh, Button selectAll, Button install)
        {
            int width = Math.Max(720, page.ClientSize.Width - 40);
            int buttonY = Math.Max(500, page.ClientSize.Height - 50);
            _programUpdateSearch.Location = new Point(Math.Max(420, page.ClientSize.Width - 320), 14);
            _programUpdateSummary.Size = new Size(Math.Max(300, _programUpdateSearch.Left - 40), 28);
            _programUpdateGrid.Location = new Point(20, 56);
            _programUpdateGrid.Size = new Size(width, Math.Max(300, buttonY - 68));
            refresh.Location = new Point(20, buttonY);
            selectAll.Location = new Point(217, buttonY);
            install.Location = new Point(389, buttonY);
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
            _driverInventorySummary.Text = "Drivers instalados • lendo vídeo, BIOS, chipset e dispositivos...";
            List<DriverInventoryItem> items = await Task.Run(delegate { return DriverManager.ReadInstalledDrivers(); });
            if (IsDisposed) return;
            _driverInventoryItems = items;
            ApplyDriverInventoryFilter();
            int categories = items.Select(delegate(DriverInventoryItem item) { return item.Category; }).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            int problems = items.Count(delegate(DriverInventoryItem item) { return item.HasProblem; });
            _driverInventorySummary.Text = items.Count == 0 ? "Não foi possível ler os drivers instalados" : items.Count + " drivers relevantes • " + categories + " categorias" + (problems == 0 ? " • nenhum problema" : " • " + problems + (problems == 1 ? " problema" : " problemas"));
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
            _environmentBadge.Text = power + "  •  " + (_managedEnvironment ? "Corporativo" : "Pessoal") + "  •  " + (Optimizer.IsAdministrator() ? "Administrador" : "Padrão");
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
                if (record.Component.IndexOf("Armazen", StringComparison.OrdinalIgnoreCase) >= 0)
                    AttachClick(card, delegate { _tabs.SelectedIndex = (int)AppSection.Storage; });
                else if (record.Component.IndexOf("Vídeo", StringComparison.OrdinalIgnoreCase) >= 0 || record.Component.IndexOf("BIOS", StringComparison.OrdinalIgnoreCase) >= 0 || record.Component.IndexOf("Placa", StringComparison.OrdinalIgnoreCase) >= 0)
                    AttachClick(card, delegate { _tabs.SelectedIndex = (int)AppSection.Updates; });
                _hardwareCards.Controls.Add(card);
                if (cardIndex % 2 == 1) _hardwareCards.SetFlowBreak(card, true);
                cardIndex++;
            }
            _hardwareCards.ResumeLayout();
        }

        private async Task<string> RunWork(string initialStatus, Func<CancellationToken, IProgress<string>, string> worker, bool saveReport = true)
        {
            if (_cts != null) return "Outra operação está em andamento. Aguarde a conclusão ou cancele a operação atual.";
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
                UpdateStorageSelection();
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
                foreach (StartupEntry item in entries.OrderByDescending(item => item.Enabled).ThenBy(item => item.Impact == "Alto" ? 0 : item.Impact == "Médio" ? 1 : 2).ThenBy(item => item.Name))
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
            finally
            {
                _startupLoading = false;
                ApplyStartupFilter();
                UpdateStartupChangeCount();
            }
        }

        private void ApplyStartupFilter()
        {
            if (_startupGrid == null || _startupLoading) return;
            string search = _startupSearch == null ? string.Empty : _startupSearch.Text.Trim();
            int filter = _startupFilter == null ? 0 : _startupFilter.SelectedIndex;
            _startupGrid.CurrentCell = null;
            foreach (DataGridViewRow row in _startupGrid.Rows)
            {
                if (row.IsNewRow) continue;
                bool enabled = Convert.ToBoolean(row.Cells["Enabled"].Value);
                bool canChange = Convert.ToBoolean(row.Cells["CanChange"].Value);
                string impact = Convert.ToString(row.Cells["Impact"].Value);
                string haystack = Convert.ToString(row.Cells["Name"].Value) + " " + Convert.ToString(row.Cells["Source"].Value) + " " + Convert.ToString(row.Cells["Command"].Value);
                bool visible = string.IsNullOrEmpty(search) || haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                if (filter == 1) visible &= enabled;
                if (filter == 2) visible &= string.Equals(impact, "Alto", StringComparison.OrdinalIgnoreCase);
                else if (filter == 3) visible &= canChange;
                else if (filter == 4) visible &= !canChange;
                row.Visible = visible;
            }
        }

        private void UpdateStartupChangeCount()
        {
            if (_startupApplyButton == null || _startupGrid == null) return;
            int changes = 0;
            foreach (DataGridViewRow row in _startupGrid.Rows)
                if (!row.IsNewRow && Convert.ToBoolean(row.Cells["CanChange"].Value) && Convert.ToBoolean(row.Cells["Enabled"].Value) != Convert.ToBoolean(row.Cells["Original"].Value)) changes++;
            _startupApplyButton.Enabled = changes > 0;
            _startupApplyButton.Text = changes == 0 ? "Nenhuma alteração" : "Aplicar " + changes + (changes == 1 ? " alteração" : " alterações");
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
                        AddStorageResultRow(row.Path, V2Engine.FormatBytes(row.LogicalBytes), V2Engine.FormatBytes(row.AllocatedBytes) + " no disco");
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
            foreach (LargeFileEntry file in files) AddStorageResultRow(file.Path, V2Engine.FormatBytes(file.Size), "Modificado em " + file.Modified.ToString("dd/MM/yyyy"));
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
                foreach (DuplicateEntry row in rows) AddStorageResultRow(row.Path, V2Engine.FormatBytes(row.Size), "Grupo " + row.Group);
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
            if (rows != null) foreach (DuplicateEntry row in rows) AddStorageResultRow(row.Path, V2Engine.FormatBytes(row.Size), "Grupo " + row.Group);
            _folderSummary.Text = rows == null || rows.Count == 0 ? "Nenhum duplicado restante" : rows.Select(item => item.Group).Distinct().Count() + " grupos restantes";
        }

        private void UpdateStorageSelection()
        {
            if (_deleteStorageItem == null) return;
            bool selected = _storageGrid != null && _storageGrid.SelectedRows.Count == 1;
            string path = selected ? Convert.ToString(_storageGrid.SelectedRows[0].Cells["Path"].Value) : string.Empty;
            string blocked = selected ? StorageDeletion.GetBlockReason(path) : string.Empty;
            bool protectedItem = selected && !string.IsNullOrWhiteSpace(blocked);
            _deleteStorageItem.Enabled = selected && !protectedItem;
            _deleteStorageItem.Visible = selected && !protectedItem;
            _storageSelectionStatus.Visible = protectedItem;
            if (protectedItem) _toolTip.SetToolTip(_storageSelectionStatus, blocked);
        }

        private void AddStorageResultRow(string path, string size, string details)
        {
            string blocked = StorageDeletion.GetBlockReason(path);
            int index = _storageGrid.Rows.Add(path, size, string.IsNullOrWhiteSpace(blocked) ? details : "Protegido pelo sistema");
            if (_storageGrid.Rows.Count == 1)
            {
                _storageGrid.ClearSelection();
                UpdateStorageSelection();
            }
            if (string.IsNullOrWhiteSpace(blocked)) return;
            DataGridViewRow row = _storageGrid.Rows[index];
            row.Cells["Details"].Style.ForeColor = Theme.Warning;
            row.Cells["Details"].ToolTipText = blocked;
            row.Cells["Path"].ToolTipText = blocked;
        }

        private void ApplyStorageFilter()
        {
            if (_storageGrid == null) return;
            string search = _storageSearch == null ? string.Empty : _storageSearch.Text.Trim();
            _storageGrid.CurrentCell = null;
            foreach (DataGridViewRow row in _storageGrid.Rows)
            {
                if (row.IsNewRow) continue;
                string content = Convert.ToString(row.Cells["Path"].Value) + " " + Convert.ToString(row.Cells["Details"].Value);
                row.Visible = string.IsNullOrEmpty(search) || content.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private void OpenStorageItemLocation(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path))) return;
            try
            {
                if (File.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
                else Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Abrir local", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private async Task DeleteSelectedStorageItem()
        {
            if (_storageGrid == null || _storageGrid.SelectedRows.Count != 1) return;
            if (_cts != null)
            {
                MessageBox.Show(this, "Aguarde a operação atual terminar ou cancele-a antes de excluir um item.", "Operação em andamento", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DataGridViewRow selectedRow = _storageGrid.SelectedRows[0];
            string path = Convert.ToString(selectedRow.Cells["Path"].Value);
            string blocked = StorageDeletion.GetBlockReason(path);
            if (!string.IsNullOrWhiteSpace(blocked))
            {
                MessageBox.Show(this, blocked, "Item protegido", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string kind = Directory.Exists(path) ? "a pasta" : "o arquivo";
            string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
            if (MessageBox.Show(this, "Mover " + kind + " para a Lixeira?\r\n\r\n" + path, "Confirmar exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            string result = await RunWork("Movendo para a Lixeira...", delegate(CancellationToken t, IProgress<string> p)
            {
                t.ThrowIfCancellationRequested();
                return StorageDeletion.MoveToRecycleBin(path);
            });
            if (!result.StartsWith("Movido para a Lixeira", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, result, "Não foi possível excluir", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _storageGrid.Rows.Remove(selectedRow);
            _folderSummary.Text = "Movido para a Lixeira: " + name;
            LoadVolumes();
            UpdateStorageSelection();
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
                var copy = ButtonFactory("Copiar", 472, 432, 100, Theme.Primary);
                copy.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                copy.Click += delegate { if (!string.IsNullOrEmpty(content)) Clipboard.SetText(content); };
                var close = ButtonFactory("Fechar", 584, 432, 100, Theme.Secondary);
                close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                close.DialogResult = DialogResult.OK;
                dialog.Controls.Add(text);
                dialog.Controls.Add(copy);
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

        private static Panel NewContentPanel(string accessibleName)
        {
            return new Panel { BackColor = Theme.Background, ForeColor = Theme.Text, AccessibleName = accessibleName };
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

        private static string FirstResultLine(string result, string fallback)
        {
            string line = (result ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(line) ? fallback : line;
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
            NativeWindowTheme.ApplyTree(grid);
            return grid;
        }

        private static Button ButtonFactory(string text, int x, int y, int width, Color color)
        {
            var button = new ModernButton { Text = text, Location = new Point(x, y), Size = new Size(width, 38), BackColor = color, BaseColor = color, ForeColor = Theme.ButtonText, FlatStyle = FlatStyle.Flat, AccessibleName = text, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static void SetButtonColor(Button button, Color color)
        {
            if (button == null) return;
            button.BackColor = color;
            ModernButton modern = button as ModernButton;
            if (modern != null) modern.BaseColor = color;
            button.Invalidate();
        }

    }
}
