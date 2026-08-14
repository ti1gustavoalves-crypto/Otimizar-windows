using System;
using System.Drawing;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2
    {
        private TabPage BuildMaintenanceWorkspace()
        {
            TabPage page = NewPage("Manutenção");
            Button storage = ButtonFactory("Armazenamento", 20, 10, 150, Theme.Primary);
            Button startup = ButtonFactory("Inicialização", 182, 10, 135, Theme.Secondary);
            _maintenanceTabs = HiddenTabs(BuildStorageTab(), BuildStartupTab());
            Panel content = WorkspaceContent(_maintenanceTabs, 54);
            Action update = delegate
            {
                SetButtonColor(storage, _maintenanceTabs.SelectedIndex == 0 ? Theme.Primary : Theme.Secondary);
                SetButtonColor(startup, _maintenanceTabs.SelectedIndex == 1 ? Theme.Primary : Theme.Secondary);
            };
            storage.Click += delegate { _maintenanceTabs.SelectedIndex = 0; update(); if (!_suppressStartup) LoadVolumes(); };
            startup.Click += async delegate { _maintenanceTabs.SelectedIndex = 1; update(); if (!_suppressStartup) await LoadStartupAsync(); };
            page.Controls.Add(content);
            page.Controls.Add(storage);
            page.Controls.Add(startup);
            page.Resize += delegate { LayoutWorkspaceContent(page, content, 54); };
            LayoutWorkspaceContent(page, content, 54);
            return page;
        }

        private TabPage BuildSystemWorkspace()
        {
            TabPage page = NewPage("Sistema");
            Button status = ButtonFactory("Saúde e processos", 20, 10, 170, Theme.Primary);
            Button hardware = ButtonFactory("Hardware", 202, 10, 120, Theme.Secondary);
            Button drivers = ButtonFactory("Drivers instalados", 334, 10, 155, Theme.Secondary);
            _systemTabs = HiddenTabs(BuildDiagnosticsTab(), BuildHardwareTab(), BuildDriverInventoryTab());
            Panel content = WorkspaceContent(_systemTabs, 54);
            Action update = delegate
            {
                SetButtonColor(status, _systemTabs.SelectedIndex == 0 ? Theme.Primary : Theme.Secondary);
                SetButtonColor(hardware, _systemTabs.SelectedIndex == 1 ? Theme.Primary : Theme.Secondary);
                SetButtonColor(drivers, _systemTabs.SelectedIndex == 2 ? Theme.Primary : Theme.Secondary);
            };
            status.Click += async delegate { _systemTabs.SelectedIndex = 0; update(); if (!_suppressStartup) await LoadDiagnostics(false); };
            hardware.Click += async delegate
            {
                _systemTabs.SelectedIndex = 1;
                update();
                if (!_hardwareLoaded && _cts == null) await LoadHardware(false);
            };
            drivers.Click += async delegate
            {
                _systemTabs.SelectedIndex = 2;
                update();
                await LoadDriverInventoryAsync(false);
            };
            page.Controls.Add(content);
            page.Controls.Add(status);
            page.Controls.Add(hardware);
            page.Controls.Add(drivers);
            page.Resize += delegate { LayoutWorkspaceContent(page, content, 54); };
            LayoutWorkspaceContent(page, content, 54);
            return page;
        }

        private TabPage BuildDriverInventoryTab()
        {
            TabPage page = NewPage("Drivers instalados");
            _driverInventorySummary = new Label { Text = "Inventário de drivers • abra esta seção para carregar", Location = new Point(20, 18), Size = new Size(420, 28), AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11f) };
            _driverFilter = new ComboBox { Location = new Point(470, 14), Size = new Size(155, 28), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text };
            _driverFilter.Items.AddRange(new object[] { "Todos", "Vídeo", "BIOS", "Firmware", "Chipset / sistema", "Áudio", "Rede", "Armazenamento", "Bluetooth", "USB", "Problema", "Sem assinatura" });
            _driverFilter.SelectedIndex = 0;
            _driverSearch = new TextBox { Location = new Point(637, 15), Size = new Size(190, 26), BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Pesquisar drivers instalados" };
            _driverProblemsOnly = new CheckBox { Text = "Somente problemas", Location = new Point(841, 16), AutoSize = true, ForeColor = Theme.Muted };
            NativeWindowTheme.SetCueBanner(_driverSearch, "Pesquisar drivers");
            _driverFilter.SelectedIndexChanged += delegate { ApplyDriverInventoryFilter(); };
            _driverSearch.TextChanged += delegate { ApplyDriverInventoryFilter(); };
            _driverProblemsOnly.CheckedChanged += delegate { ApplyDriverInventoryFilter(); };

            _installedDriverGrid = Grid(20, 54, 1000, 500);
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
            _installedDriverGrid.Columns[6].Visible = false;
            _installedDriverGrid.ReadOnly = true;
            _installedDriverGrid.Anchor = AnchorStyles.None;
            Button protection = BuildDriverProtectionButton();

            page.Controls.Add(_driverInventorySummary);
            page.Controls.Add(_driverFilter);
            page.Controls.Add(_driverSearch);
            page.Controls.Add(_driverProblemsOnly);
            page.Controls.Add(_installedDriverGrid);
            page.Controls.Add(protection);
            page.Resize += delegate { LayoutDriverInventory(page, protection); };
            LayoutDriverInventory(page, protection);
            return page;
        }

        private Button BuildDriverProtectionButton()
        {
            Button protection = ButtonFactory("Proteção e backup", 20, 0, 170, Theme.Secondary);
            var menu = new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Text, ShowImageMargin = false };
            var backup = new ToolStripMenuItem("Criar backup de drivers");
            var restore = new ToolStripMenuItem("Restaurar backup mais recente");
            var backups = new ToolStripMenuItem("Abrir pasta de backups");
            var windowsUpdate = new ToolStripMenuItem("Abrir Windows Update");
            backup.Click += async delegate { await CreateDriverBackup(); };
            restore.Click += async delegate { await RestoreDriverBackup(); };
            backups.Click += delegate { DriverManager.OpenDriverBackups(); };
            windowsUpdate.Click += delegate { DriverManager.OpenWindowsUpdate(); };
            menu.Items.Add(backup);
            menu.Items.Add(restore);
            menu.Items.Add(backups);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(windowsUpdate);
            protection.Click += delegate { menu.Show(protection, new Point(0, protection.Height)); };
            return protection;
        }

        private void LayoutDriverInventory(TabPage page, Button protection)
        {
            int width = Math.Max(650, page.ClientSize.Width - 40);
            int right = page.ClientSize.Width - 20;
            _driverProblemsOnly.Location = new Point(right - 160, 16);
            _driverSearch.Location = new Point(_driverProblemsOnly.Left - 204, 15);
            _driverFilter.Location = new Point(_driverSearch.Left - 167, 14);
            _driverInventorySummary.Size = new Size(Math.Max(220, _driverFilter.Left - 40), 28);
            _installedDriverGrid.Location = new Point(20, 54);
            int buttonY = Math.Max(360, page.ClientSize.Height - 50);
            _installedDriverGrid.Size = new Size(width, Math.Max(250, buttonY - 66));
            protection.Location = new Point(20, buttonY);
        }

        private static TabControl HiddenTabs(params TabPage[] pages)
        {
            var tabs = new TabControl { Location = new Point(-4, -28), SizeMode = TabSizeMode.Fixed, ItemSize = new Size(1, 24), Appearance = TabAppearance.FlatButtons };
            tabs.TabPages.AddRange(pages);
            return tabs;
        }

        private static Panel WorkspaceContent(TabControl tabs, int top)
        {
            var content = new Panel { Location = new Point(0, top), BackColor = Theme.Background, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            content.Controls.Add(tabs);
            content.Resize += delegate
            {
                tabs.Location = new Point(-4, -28);
                tabs.Size = new Size(content.ClientSize.Width + 8, content.ClientSize.Height + 32);
            };
            return content;
        }

        private static void LayoutWorkspaceContent(TabPage page, Panel content, int top)
        {
            content.Location = new Point(0, top);
            content.Size = new Size(page.ClientSize.Width, Math.Max(260, page.ClientSize.Height - top));
        }

        private void NavigateTo(AppSection section)
        {
            _tabs.SelectedIndex = (int)section;
        }

        private void NavigateToMaintenance(int index)
        {
            NavigateTo(AppSection.Maintenance);
            if (_maintenanceTabs != null) _maintenanceTabs.SelectedIndex = Math.Max(0, Math.Min(_maintenanceTabs.TabPages.Count - 1, index));
        }

        private void NavigateToSystem(int index)
        {
            NavigateTo(AppSection.System);
            if (_systemTabs != null) _systemTabs.SelectedIndex = Math.Max(0, Math.Min(_systemTabs.TabPages.Count - 1, index));
        }
    }
}
