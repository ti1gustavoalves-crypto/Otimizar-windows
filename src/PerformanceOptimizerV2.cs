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
            Maintenance,
            Updates,
            System,
            Settings
        }

        private TabControl _tabs;
        private TabControl _maintenanceTabs;
        private TabControl _systemTabs;
        private Button[] _navigationButtons;
        private Panel _navigationPanel;
        private Button _homeButton;
        private Label _privilegeStatus;
        private Panel _operationBar;
        private Image _brandImage;
        private Label _overviewStatus;
        private Label _overviewNote;
        private Label _environmentBadge;
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
        private Label _processHistoryLabel;
        private Button _processHistoryToggle;
        private FlowLayoutPanel _diagnosticCards;
        private Label _diagnosticStatus;
        private DiagnosticSnapshot _diagnosticSnapshot;
        private bool _diagnosticsLoaded;
        private DataGridView _integrityGrid;
        private Label _integritySummary;
        private EmptyStatePanel _integrityEmpty;
        private Button _integrityRepairButton;
        private List<IntegrityCheckResult> _integrityResults = new List<IntegrityCheckResult>();
        private bool _integrityLoaded;
        private DataGridView _repairGrid;
        private Label _repairSummary;
        private EmptyStatePanel _repairEmpty;
        private Button _repairRunButton;
        private List<RepairFinding> _repairFindings = new List<RepairFinding>();
        private bool _repairsLoaded;
        private CheckBox _minimizeToTray;
        private CheckBox _automaticProfiles;
        private CheckBox _compactMode;
        private Label _updateStatus;
        private bool _applicationUpdateInProgress;
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
        private ComboBox _storageAnalysisMode;
        private ToolTip _toolTip;
        private ComboBox _schedule;
        private Label _maintenanceResult;
        private DataGridView _installedDriverGrid;
        private Label _driverInventorySummary;
        private ComboBox _driverFilter;
        private TextBox _driverSearch;
        private CheckBox _driverProblemsOnly;
        private List<DriverInventoryItem> _driverInventoryItems = new List<DriverInventoryItem>();
        private List<DriverUpdate> _driverUpdates = new List<DriverUpdate>();
        private bool _driverInventoryLoaded;
        private List<ProgramUpdate> _programUpdates = new List<ProgramUpdate>();
        private List<WindowsSystemUpdate> _windowsUpdates = new List<WindowsSystemUpdate>();
        private DataGridView _updateQueueGrid;
        private Label _updateQueueSummary;
        private TextBox _updateQueueSearch;
        private ComboBox _updateQueueFilter;
        private EmptyStatePanel _updateQueueEmpty;
        private Button _installUpdatesButton;
        private bool _updatesSearched;
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
        private readonly int? _initialServiceProfile;
        private ComboBox _serviceProfile;
        private DataGridView _issueGrid;
        private Label _planSummary;
        private Label _comparisonSummary;
        private Button _comparisonToggle;
        private Button _runPlanButton;
        private MaintenancePlan _maintenancePlan;
        private bool _planLoading;
        private HealthAssessment _healthAssessment;
        private Button _fullServiceButton;
        private bool _fullServiceRunning;
        private readonly bool _startFullService;
        private readonly bool _suppressStartup;
        private readonly bool _startIntegrityScan;

        public MainFormV2(int? initialServiceProfile = null, bool startFullService = false, bool suppressStartup = false, bool startIntegrityScan = false)
        {
            _initialServiceProfile = initialServiceProfile;
            _startFullService = startFullService;
            _suppressStartup = suppressStartup;
            _startIntegrityScan = startIntegrityScan;
            Text = "Otimizador";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1024, 680);
            Size = new Size(1280, 800);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            NativeWindowTheme.Apply(this);
            AutoScaleMode = AutoScaleMode.Dpi;
            AccessibleName = "Otimizador de Desempenho";
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            _advancedSettings = AdvancedEngine.ReadSettings();
            _processHistory = new ProcessHistoryTracker();
            _toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 350, ReshowDelay = 100 };

            _tabs = new TabControl { Location = new Point(-4, -28), SizeMode = TabSizeMode.Fixed, ItemSize = new Size(1, 24), Appearance = TabAppearance.FlatButtons };
            _tabs.TabPages.Add(BuildGuidedDashboard());
            _tabs.TabPages.Add(BuildMaintenanceWorkspace());
            _tabs.TabPages.Add(BuildUpdatesTab());
            _tabs.TabPages.Add(BuildSystemWorkspace());
            _tabs.TabPages.Add(BuildSettingsTab());
            ApplyDensity();
            _tabs.SelectedIndexChanged += async delegate
            {
                UpdateNavigationState();
                if (_suppressStartup || _cts != null) return;
                if (_tabs.SelectedIndex == (int)AppSection.Maintenance)
                {
                    if (_maintenanceTabs != null && _maintenanceTabs.SelectedIndex == 1) await LoadStartupAsync();
                    else LoadVolumes();
                }
                else if (_tabs.SelectedIndex == (int)AppSection.System && _systemTabs != null)
                {
                    if (_systemTabs.SelectedIndex == 0) await LoadDiagnostics(false);
                    else if (_systemTabs.SelectedIndex == 1) await LoadIntegrityAsync(false);
                    else if (_systemTabs.SelectedIndex == 2 && !_hardwareLoaded) await LoadHardware(false);
                    else if (_systemTabs.SelectedIndex == 3) await LoadDriverInventoryAsync(false);
                    else if (_systemTabs.SelectedIndex == 4) await LoadRepairsAsync(false);
                }
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

            _operationBar = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Theme.Header, Visible = false };
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
            _operationBar.Controls.Add(_progress);
            _operationBar.Controls.Add(_status);
            _operationBar.Controls.Add(cancelArea);

            Controls.Add(body);
            Controls.Add(_operationBar);
            Resize += delegate { ApplyResponsiveShell(); };
            ApplyResponsiveShell();
            _activitySampler = new SystemActivitySampler();
            _processSampler = new ProcessActivitySampler();
            _alertMonitor = new SustainedAlertMonitor(TimeSpan.FromSeconds(20));
            ConfigureTrayIcon();
            _liveMetricsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _liveMetricsTimer.Tick += delegate { RefreshLiveMetrics(); };
            Shown += async delegate
            {
                if (_suppressStartup) return;
                _activitySampler.Prime();
                _processSampler.Prime();
                _liveMetricsTimer.Start();
                await RefreshAudit();
                BeginAutomaticUpdateCheck();
                if (_startIntegrityScan && !IsDisposed)
                {
                    NavigateToSystem(1);
                    await RunDeepIntegrityAsync(true);
                }
                if (_startFullService && !IsDisposed) await ExecuteCompleteTechnicalServiceAsync(true);
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
            _navigationPanel = new Panel { Dock = DockStyle.Left, Width = 172, BackColor = Theme.Navigation, Padding = new Padding(10, 16, 10, 12) };
            _brandImage = LoadBrandImage();
            _homeButton = new Button
            {
                BackgroundImage = _brandImage,
                BackgroundImageLayout = ImageLayout.Zoom,
                Location = new Point(10, 14),
                Size = new Size(44, 44),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Navigation,
                Cursor = Cursors.Hand,
                AccessibleName = "Ir para o Painel",
                TabIndex = 0
            };
            _homeButton.FlatAppearance.BorderSize = 0;
            _homeButton.FlatAppearance.MouseOverBackColor = Theme.SurfaceAlt;
            _homeButton.Click += delegate { _tabs.SelectedIndex = (int)AppSection.Dashboard; };
            _toolTip.SetToolTip(_homeButton, "Painel");
            _navigationPanel.Controls.Add(_homeButton);

            _privilegeStatus = new Label
            {
                Text = Optimizer.IsAdministrator() ? "●  Administrador" : "●  Usuário padrão",
                Dock = DockStyle.Bottom,
                Height = 38,
                Padding = new Padding(8, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Optimizer.IsAdministrator() ? Theme.Success : Theme.Muted,
                Font = new Font("Segoe UI", 8.5f),
                AccessibleName = Optimizer.IsAdministrator() ? "Executando como administrador" : "Executando como usuário padrão"
            };
            _navigationPanel.Controls.Add(_privilegeStatus);

            string[] labels = { "Painel", "Manutenção", "Atualizações", "Sistema", "Ajustes" };
            _navigationButtons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int tabIndex = i;
                var button = new Button
                {
                    Text = labels[i],
                    Image = CreateNavigationIcon(i),
                    ImageAlign = ContentAlignment.MiddleLeft,
                    TextImageRelation = TextImageRelation.ImageBeforeText,
                    Location = new Point(10, 70 + (i * 48)),
                    Size = new Size(152, 40),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(12, 0, 0, 0),
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
                _toolTip.SetToolTip(button, labels[i]);
                _navigationButtons[i] = button;
                _navigationPanel.Controls.Add(button);
            }

            UpdateNavigationState();
            return _navigationPanel;
        }

        private void ApplyResponsiveShell()
        {
            if (_navigationPanel == null || _navigationButtons == null) return;
            bool compact = ClientSize.Width < 1180;
            _navigationPanel.Width = compact ? 68 : 172;
            if (_homeButton != null)
            {
                _homeButton.Location = compact ? new Point(12, 14) : new Point(10, 14);
                _homeButton.Size = new Size(44, 44);
            }
            if (_privilegeStatus != null) _privilegeStatus.Visible = !compact;
            string[] full = { "Painel", "Manutenção", "Atualizações", "Sistema", "Ajustes" };
            for (int index = 0; index < _navigationButtons.Length; index++)
            {
                Button button = _navigationButtons[index];
                button.Text = compact ? string.Empty : full[index];
                button.Location = new Point(compact ? 8 : 10, 70 + (index * 48));
                button.Size = new Size(compact ? 52 : 152, 40);
                button.Padding = compact ? Padding.Empty : new Padding(12, 0, 0, 0);
                button.ImageAlign = compact ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
                button.TextAlign = compact ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            }
        }

        private static Image CreateNavigationIcon(int kind)
        {
            string[] resources = { "NavigationPanelPng", "NavigationMaintenancePng", "NavigationUpdatesPng", "NavigationSystemPng", "NavigationSettingsPng" };
            if (kind >= 0 && kind < resources.Length)
            {
                try
                {
                    using (Stream stream = typeof(MainFormV2).Assembly.GetManifestResourceStream(resources[kind]))
                    using (Image source = stream == null ? null : Image.FromStream(stream))
                    {
                        if (source != null)
                        {
                            var icon = new Bitmap(20, 20, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            using (Graphics graphics = Graphics.FromImage(icon))
                            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
                            {
                                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                float red = Theme.Muted.R / 255f;
                                float green = Theme.Muted.G / 255f;
                                float blue = Theme.Muted.B / 255f;
                                attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new[]
                                {
                                    new[] { 0f, 0f, 0f, 0f, 0f },
                                    new[] { 0f, 0f, 0f, 0f, 0f },
                                    new[] { 0f, 0f, 0f, 0f, 0f },
                                    new[] { 0f, 0f, 0f, 1f, 0f },
                                    new[] { red, green, blue, 0f, 1f }
                                }));
                                graphics.DrawImage(source, new Rectangle(1, 1, 18, 18), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                            }
                            return icon;
                        }
                    }
                }
                catch { }
            }
            var image = new Bitmap(18, 18);
            using (Graphics graphics = Graphics.FromImage(image))
            using (var pen = new Pen(Theme.Muted, 1.7f))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                if (kind == 0)
                {
                    graphics.DrawRectangle(pen, 2, 2, 5, 5); graphics.DrawRectangle(pen, 11, 2, 5, 5);
                    graphics.DrawRectangle(pen, 2, 11, 5, 5); graphics.DrawRectangle(pen, 11, 11, 5, 5);
                }
                else if (kind == 1)
                {
                    graphics.DrawLine(pen, 3, 15, 14, 4); graphics.DrawEllipse(pen, 11, 1, 5, 5); graphics.DrawEllipse(pen, 1, 12, 5, 5);
                }
                else if (kind == 2)
                {
                    graphics.DrawArc(pen, 2, 2, 14, 14, 35, 285); graphics.DrawLine(pen, 13, 2, 16, 2); graphics.DrawLine(pen, 16, 2, 16, 5);
                }
                else if (kind == 3)
                {
                    graphics.DrawRectangle(pen, 2, 3, 14, 11); graphics.DrawLine(pen, 6, 17, 12, 17); graphics.DrawLine(pen, 9, 14, 9, 17);
                }
                else
                {
                    graphics.DrawEllipse(pen, 5, 5, 8, 8); graphics.DrawEllipse(pen, 8, 8, 2, 2);
                    graphics.DrawLine(pen, 9, 1, 9, 4); graphics.DrawLine(pen, 9, 14, 9, 17); graphics.DrawLine(pen, 1, 9, 4, 9); graphics.DrawLine(pen, 14, 9, 17, 9);
                }
            }
            return image;
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

        private TabPage BuildHardwareTab()
        {
            var page = NewPage("Hardware");
            _hardwareSummary = new Label { Text = "Componentes principais", AutoSize = false, AutoEllipsis = true, Size = new Size(1000, 32), Location = new Point(20, 20), ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 10.5f) };

            _hardwareCards = new FlowLayoutPanel
            {
                Location = new Point(20, 62),
                Size = new Size(1000, 525),
                Anchor = AnchorStyles.None,
                BackColor = Theme.SurfaceDark,
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true,
                Padding = new Padding(10),
                WrapContents = true
            };

            page.Controls.Add(_hardwareSummary);
            page.Controls.Add(_hardwareCards);
            page.Resize += delegate
            {
                _hardwareSummary.Size = new Size(Math.Max(500, page.ClientSize.Width - 40), 32);
                _hardwareCards.Location = new Point(20, 62);
                _hardwareCards.Size = new Size(Math.Max(600, page.ClientSize.Width - 40), Math.Max(300, page.ClientSize.Height - 82));
            };
            page.Enter += async delegate { if (!_suppressStartup && !_hardwareLoaded && _cts == null) await LoadHardware(false); };
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
            var refresh = ButtonFactory("↻", 20, 545, 42, Theme.Secondary);
            _toolTip.SetToolTip(refresh, "Atualizar lista");
            _startupApplyButton = ButtonFactory("Nenhuma alteração", 74, 545, 190, Theme.Primary);
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
            page.Enter += async delegate { if (!_suppressStartup) await LoadStartupAsync(); };
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
            save.Location = new Point(74, buttonY);
        }

        private TabPage BuildStorageTab()
        {
            var page = NewPage("Armazenamento");
            _storageSummary = new Label { Text = "Discos e volumes", AutoSize = false, Size = new Size(520, 30), Location = new Point(20, 20), ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 10.5f) };
            _storageAnalysisMode = new ComboBox { Location = new Point(425, 15), Size = new Size(170, 28), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text };
            _storageAnalysisMode.Items.AddRange(new object[] { "Pastas", "Arquivos grandes", "Duplicados" });
            _storageAnalysisMode.SelectedIndex = 0;
            var scan = ButtonFactory("Analisar", 607, 12, 110, Theme.Primary);
            var clean = ButtonFactory("Limpar", 729, 12, 105, Theme.Warning);
            var optimize = ButtonFactory("Otimizar", 846, 12, 110, Theme.Success);
            var toolsMenu = new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Text, ShowImageMargin = false };
            var advancedCleanup = new ToolStripMenuItem("Limpeza avançada");
            var componentsCleanup = new ToolStripMenuItem("Limpar componentes do Windows");
            var storageSense = new ToolStripMenuItem("Abrir Sensor de Armazenamento");
            var energyDiagnostic = new ToolStripMenuItem("Diagnóstico de energia");
            advancedCleanup.Click += async delegate { await AdvancedCleanup(); };
            componentsCleanup.Click += async delegate
            {
                if (MessageBox.Show(this, "Remover componentes substituídos do Windows? O modo agressivo ResetBase não será usado.", "Componentes do Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await RunWork("Limpando componentes do Windows...", delegate(CancellationToken t, IProgress<string> p) { return WindowsMaintenance.CleanupComponentStore(t, p); });
                LoadVolumes();
            };
            storageSense.Click += delegate { WindowsMaintenance.OpenStorageSenseSettings(); };
            energyDiagnostic.Click += async delegate { await RunEnergyDiagnostic(); };
            toolsMenu.Items.Add(advancedCleanup);
            toolsMenu.Items.Add(componentsCleanup);
            toolsMenu.Items.Add(storageSense);
            toolsMenu.Items.Add(new ToolStripSeparator());
            toolsMenu.Items.Add(energyDiagnostic);

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
            _volumeGrid.ContextMenuStrip = toolsMenu;
            _toolTip.SetToolTip(_volumeGrid, "Clique com o botão direito para ações avançadas do disco");
            _volumeGrid.SelectionChanged += delegate
            {
                if (_volumeGrid.SelectedRows.Count > 0) _selectedDrive = Convert.ToString(_volumeGrid.SelectedRows[0].Cells["Drive"].Value);
            };

            _folderSummary = new Label { Text = "Selecione um disco e escolha uma análise", AutoSize = false, AutoEllipsis = true, Size = new Size(490, 28), Location = new Point(20, 194), ForeColor = Theme.Muted };
            _storageSearch = new TextBox { Location = new Point(520, 188), Size = new Size(210, 27), BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Pesquisar resultados do armazenamento", Visible = false };
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

            scan.Click += async delegate
            {
                if (_storageAnalysisMode.SelectedIndex == 1) await ScanLargeFiles();
                else if (_storageAnalysisMode.SelectedIndex == 2) await ScanDuplicates();
                else await ScanSelectedVolume();
            };
            clean.Click += async delegate { await OpenSafeCleanup(); };
            optimize.Click += async delegate { await OptimizeSelectedVolume(); };
            _deleteStorageItem.Click += async delegate { await DeleteSelectedStorageItem(); };
            page.Controls.Add(_storageSummary);
            page.Controls.Add(_volumeGrid);
            page.Controls.Add(_folderSummary);
            page.Controls.Add(_storageSearch);
            page.Controls.Add(_storageGrid);
            page.Controls.Add(_storageAnalysisMode);
            page.Controls.Add(scan);
            page.Controls.Add(clean);
            page.Controls.Add(optimize);
            page.Controls.Add(_deleteStorageItem);
            page.Controls.Add(_storageSelectionStatus);
            _volumeGrid.Anchor = AnchorStyles.None;
            _storageGrid.Anchor = AnchorStyles.None;
            page.Resize += delegate { LayoutStorageTab(page, scan, clean, optimize); };
            LayoutStorageTab(page, scan, clean, optimize);
            page.Enter += delegate { if (!_suppressStartup) LoadVolumes(); };
            return page;
        }

        private void LayoutStorageTab(TabPage page, Button scan, Button clean, Button optimize)
        {
            int width = Math.Max(600, page.ClientSize.Width - 40);
            _volumeGrid.Location = new Point(20, 58);
            _volumeGrid.Size = new Size(width, 120);
            _storageGrid.Location = new Point(20, 228);
            _storageGrid.Size = new Size(width, Math.Max(210, page.ClientSize.Height - _storageGrid.Top - 20));
            int actionsLeft = Math.Max(300, page.ClientSize.Width - 590);
            _storageAnalysisMode.Location = new Point(actionsLeft, 15);
            scan.Location = new Point(actionsLeft + 182, 12);
            clean.Location = new Point(actionsLeft + 304, 12);
            optimize.Location = new Point(actionsLeft + 421, 12);
            _deleteStorageItem.Location = new Point(page.ClientSize.Width - 200, 184);
            _storageSelectionStatus.Location = new Point(page.ClientSize.Width - 200, 192);
            _storageSearch.Location = new Point(Math.Max(360, page.ClientSize.Width - 510), 188);
            _storageSearch.Size = new Size(290, 27);
            _folderSummary.Size = new Size(Math.Max(280, _storageSearch.Left - 40), 28);
            _storageSummary.Size = new Size(Math.Max(180, actionsLeft - 40), 30);
        }


        private async Task LoadDriverInventoryAsync(bool force)
        {
            if (_driverInventoryLoaded && !force) return;
            _driverInventoryLoaded = true;
            _driverInventorySummary.Text = "Drivers instalados • lendo vídeo, BIOS, chipset e dispositivos...";
            List<DriverInventoryItem> items = await Task.Run(delegate { return CachedAnalysis.ReadDriverInventory(force); });
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
            CachedAnalysis.InvalidateDrivers();
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
            await LoadDiagnostics(false);
            await RefreshMaintenancePlanAsync();
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

            _environmentBadge.Text = (_managedEnvironment ? "Corporativo" : "Pessoal") + "  •  " + (Optimizer.IsAdministrator() ? "Administrador" : "Acesso padrão") + "  •  " + (AppPaths.IsPortable ? "Portátil" : "Instalado");
            _healthAssessment = SystemHealthEngine.Assess(m, _diagnosticSnapshot, _lastProcessActivities, _driverUpdates == null ? 0 : _driverUpdates.Count, _programUpdates == null ? 0 : _programUpdates.Count);
            BottleneckCause cause = BottleneckAnalyzer.Analyze(m, _diagnosticSnapshot, _lastProcessActivities);
            _overviewStatus.Text = "Saúde " + _healthAssessment.Level.ToLowerInvariant() + "  •  " + _healthAssessment.Score + "/100";
            _overviewStatus.ForeColor = _healthAssessment.Score < 55 ? Theme.Danger : _healthAssessment.Score < 75 ? Theme.Warning : Theme.Text;
            _overviewNote.Text = cause.Title;
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
                List<ImportantHardware> records = CachedAnalysis.ReadHardware(force, t, p);
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
                    AttachClick(card, delegate { NavigateToMaintenance(0); });
                else if (record.Component.IndexOf("Vídeo", StringComparison.OrdinalIgnoreCase) >= 0 || record.Component.IndexOf("BIOS", StringComparison.OrdinalIgnoreCase) >= 0 || record.Component.IndexOf("Placa", StringComparison.OrdinalIgnoreCase) >= 0)
                    AttachClick(card, delegate { NavigateTo(AppSection.Updates); });
                _hardwareCards.Controls.Add(card);
                if (cardIndex % 2 == 1) _hardwareCards.SetFlowBreak(card, true);
                cardIndex++;
            }
            _hardwareCards.ResumeLayout();
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
            _storageSearch.Visible = false;
            _folderSummary.Text = "Analisando " + _selectedDrive + "...";
            string drive = _selectedDrive;
            await RunWork("Analisando " + drive + "...", delegate(CancellationToken t, IProgress<string> p)
            {
                List<StorageEntry> rows = CachedAnalysis.ScanVolume(drive, false, t, p, delegate(StorageEntry row)
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
                CachedAnalysis.InvalidateStorage();
                LoadVolumes();
            }
        }

        private async Task ScanLargeFiles()
        {
            if (string.IsNullOrWhiteSpace(_selectedDrive)) { LoadVolumes(); if (string.IsNullOrWhiteSpace(_selectedDrive)) return; }
            string drive = _selectedDrive;
            _storageGrid.Rows.Clear();
            _storageSearch.Visible = false;
            _folderSummary.Text = "Mapeando arquivos maiores que 100 MB em " + drive + "...";
            List<LargeFileEntry> files = null;
            await RunWork("Mapeando arquivos grandes...", delegate(CancellationToken t, IProgress<string> p)
            {
                files = CachedAnalysis.FindLargeFiles(drive, false, t, p);
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
                _storageSearch.Visible = false;
                string folder = picker.SelectedPath;
                List<DuplicateEntry> rows = null;
                await RunWork("Procurando duplicados...", delegate(CancellationToken t, IProgress<string> p)
                {
                    rows = CachedAnalysis.FindDuplicates(folder, false, t, p);
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
                    CachedAnalysis.InvalidateStorage();
                    await ScanDuplicatesRefresh(folder);
                }
            }
        }

        private async Task ScanDuplicatesRefresh(string folder)
        {
            List<DuplicateEntry> rows = null;
            await RunWork("Atualizando duplicados...", delegate(CancellationToken t, IProgress<string> p)
            {
                rows = CachedAnalysis.FindDuplicates(folder, true, t, p);
                return V2Engine.DuplicateReport(folder, rows);
            }, false);
            _storageGrid.Rows.Clear();
            _storageSearch.Visible = false;
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
            if (_storageSearch != null) _storageSearch.Visible = true;
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
            CachedAnalysis.InvalidateStorage();
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
                CachedAnalysis.InvalidateStorage();
            }
        }

        private void RunAsAdmin(object sender, EventArgs e)
        {
            if (Optimizer.IsAdministrator()) { MessageBox.Show(this, "O programa já está elevado."); return; }
            string arguments = "--wait-for-instance" + (AppPaths.IsPortable ? " --portable" : string.Empty);
            try { Process.Start(new ProcessStartInfo(Application.ExecutablePath, arguments) { UseShellExecute = true, Verb = "runas" }); Close(); }
            catch (Exception ex) { MessageBox.Show(this, "Elevação cancelada: " + ex.Message); }
        }

        private void SaveAdvancedPreferences()
        {
            AdvancedSettings settings = AdvancedEngine.ReadSettings();
            settings.MinimizeToTray = _minimizeToTray != null && _minimizeToTray.Checked;
            settings.AutomaticPowerProfiles = _automaticProfiles != null && _automaticProfiles.Checked;
            settings.CompactMode = _compactMode != null && _compactMode.Checked;
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
            if (_applicationUpdateInProgress)
            {
                _updateStatus.Text = "A atualização já está em andamento.";
                return;
            }
            _updateStatus.Text = "Verificando canal de atualização...";
            UpdateCheckResult result = await Task.Run(delegate { return AdvancedEngine.CheckForUpdates(); });
            _updateStatus.Text = result.Message;
            if (!result.Available || result.Manifest == null) return;
            string notes = string.IsNullOrWhiteSpace(result.Manifest.Notes) ? string.Empty : "\r\n\r\n" + result.Manifest.Notes;
            if (MessageBox.Show(this, result.Message + notes + "\r\n\r\nBaixar agora? O aplicativo será fechado, atualizado e reaberto automaticamente.", "Atualização", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            _applicationUpdateInProgress = true;
            _updateStatus.Text = "Preparando atualização...";
            if (_operationBar != null) _operationBar.Visible = true;
            _progress.Visible = true;
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Minimum = 0;
            _progress.Maximum = 100;
            _progress.Value = 0;
            var progress = new Progress<UpdateDownloadProgress>(delegate(UpdateDownloadProgress state)
            {
                _progress.Value = Math.Max(0, Math.Min(100, state.Percent));
                _updateStatus.Text = state.TotalBytes > 0
                    ? "Baixando atualização... " + state.Percent + "%  •  " + V2Engine.FormatBytes(state.ReceivedBytes) + " de " + V2Engine.FormatBytes(state.TotalBytes)
                    : "Baixando atualização... " + V2Engine.FormatBytes(state.ReceivedBytes);
            });
            try
            {
                UpdateDownloadResult download = await Task.Run(delegate { return AdvancedEngine.DownloadVerifiedUpdate(result.Manifest, CancellationToken.None, progress); });
                _updateStatus.Text = download.Message;
                if (!download.Success) return;
                AdvancedEngine.LaunchVerifiedUpdate(download.InstallerPath, Process.GetCurrentProcess().Id);
                _updateStatus.Text = download.Reused ? "Pacote validado em cache. Reiniciando..." : "Atualização verificada. Reiniciando...";
                Close();
            }
            catch (Exception ex) { _updateStatus.Text = "Não foi possível iniciar a atualização: " + ex.Message; }
            finally
            {
                _applicationUpdateInProgress = false;
                if (_progress != null && !_progress.IsDisposed)
                {
                    _progress.Visible = false;
                    _progress.Value = 0;
                }
                if (_operationBar != null && _cts == null) _operationBar.Visible = false;
            }
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
                ColumnHeadersHeight = _advancedSettings != null && _advancedSettings.CompactMode ? 32 : 38,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                EnableHeadersVisualStyles = false,
                AccessibleName = "Tabela de dados"
            };
            grid.RowTemplate.Height = _advancedSettings != null && _advancedSettings.CompactMode ? 28 : 34;
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

        private void ApplyDensity()
        {
            bool compact = _advancedSettings != null && _advancedSettings.CompactMode;
            foreach (DataGridView grid in FindControls<DataGridView>(this))
            {
                grid.ColumnHeadersHeight = compact ? 32 : 38;
                grid.RowTemplate.Height = compact ? 28 : 34;
                foreach (DataGridViewRow row in grid.Rows) row.Height = compact ? 28 : 34;
            }
        }

        private static IEnumerable<T> FindControls<T>(Control root) where T : Control
        {
            foreach (Control child in root.Controls)
            {
                T match = child as T;
                if (match != null) yield return match;
                foreach (T nested in FindControls<T>(child)) yield return nested;
            }
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
