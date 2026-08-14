using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2
    {
        private TabPage BuildUpdatesTab()
        {
            TabPage page = NewPage("Atualizações");
            _updateQueueSummary = new Label { Text = "Verificação ainda não executada", AutoEllipsis = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11f) };
            _updateQueueFilter = new ComboBox { Width = 155, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text };
            _updateQueueFilter.Items.AddRange(new object[] { "Todos", "Windows", "Drivers", "Aplicativos" });
            _updateQueueFilter.SelectedIndex = 0;
            _updateQueueSearch = new TextBox { Width = 240, Height = 27, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Pesquisar atualizações" };
            NativeWindowTheme.SetCueBanner(_updateQueueSearch, "Pesquisar atualizações");

            Button verify = ButtonFactory("Verificar atualizações", 0, 0, 185, Theme.Primary);
            _installUpdatesButton = ButtonFactory("Instalar selecionadas", 0, 0, 190, Theme.Success);
            var actions = new ResponsiveActionBar();
            actions.AddAction(verify);
            actions.AddAction(_installUpdatesButton);

            _updateQueueGrid = Grid(0, 0, 1000, 500);
            _updateQueueGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Instalar", Width = 65 });
            _updateQueueGrid.Columns.Add("Area", "Tipo");
            _updateQueueGrid.Columns[1].Width = 105;
            _updateQueueGrid.Columns[1].ReadOnly = true;
            _updateQueueGrid.Columns.Add("Name", "Atualização");
            _updateQueueGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _updateQueueGrid.Columns[2].ReadOnly = true;
            _updateQueueGrid.Columns.Add("Installed", "Instalada");
            _updateQueueGrid.Columns[3].Width = 130;
            _updateQueueGrid.Columns[3].ReadOnly = true;
            _updateQueueGrid.Columns.Add("Available", "Disponível");
            _updateQueueGrid.Columns[4].Width = 130;
            _updateQueueGrid.Columns[4].ReadOnly = true;
            _updateQueueGrid.Columns.Add("Details", "Detalhes");
            _updateQueueGrid.Columns[5].Width = 175;
            _updateQueueGrid.Columns[5].ReadOnly = true;
            _updateQueueGrid.Columns.Add(new DataGridViewLinkColumn { Name = "OfficialSite", HeaderText = "Suporte", Width = 90, TrackVisitedState = false, LinkColor = Theme.Primary, ActiveLinkColor = Theme.Text, VisitedLinkColor = Theme.Primary });
            _updateQueueGrid.Columns[6].ReadOnly = true;
            _updateQueueGrid.CellContentClick += UpdateQueueLinkClicked;
            _updateQueueGrid.CurrentCellDirtyStateChanged += delegate { if (_updateQueueGrid.IsCurrentCellDirty) _updateQueueGrid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _updateQueueGrid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || _updateQueueGrid.Columns[e.ColumnIndex].Name != "Selected") return;
                SyncUnifiedUpdateSelection();
                UpdateInstallActionState();
            };

            Panel queueHost = new Panel { BackColor = Theme.SurfaceDark };
            _updateQueueGrid.Dock = DockStyle.Fill;
            _updateQueueEmpty = new EmptyStatePanel { Dock = DockStyle.Fill };
            _updateQueueEmpty.SetMessage("Verificação ainda não executada", "Clique em Verificar atualizações para consultar Windows, drivers e aplicativos.");
            queueHost.Controls.Add(_updateQueueGrid);
            queueHost.Controls.Add(_updateQueueEmpty);
            _updateQueueEmpty.BringToFront();

            verify.Click += async delegate { await SearchAllUpdatesAsync(true); };
            _installUpdatesButton.Click += async delegate { await InstallSelectedUpdatesAsync(); };
            _updateQueueFilter.SelectedIndexChanged += delegate { ApplyUnifiedUpdateFilter(); };
            _updateQueueSearch.TextChanged += delegate { ApplyUnifiedUpdateFilter(); };

            page.Controls.Add(_updateQueueSummary);
            page.Controls.Add(_updateQueueFilter);
            page.Controls.Add(_updateQueueSearch);
            page.Controls.Add(queueHost);
            page.Controls.Add(actions);
            page.Resize += delegate { LayoutUpdatesPage(page, queueHost, actions); };
            LayoutUpdatesPage(page, queueHost, actions);
            ApplyUnifiedUpdateFilter();
            return page;
        }

        private void LayoutUpdatesPage(TabPage page, Panel queueHost, ResponsiveActionBar actions)
        {
            int margin = 20;
            int width = Math.Max(720, page.ClientSize.Width - (margin * 2));
            _updateQueueSummary.SetBounds(margin, 18, Math.Max(240, width - 430), 28);
            _updateQueueFilter.Location = new Point(page.ClientSize.Width - margin - 407, 14);
            _updateQueueSearch.Location = new Point(page.ClientSize.Width - margin - 240, 14);
            int actionsHeight = page.ClientSize.Width < 1080 ? 84 : 46;
            actions.SetBounds(margin, Math.Max(430, page.ClientSize.Height - actionsHeight - 12), width, actionsHeight);
            queueHost.SetBounds(margin, 56, width, Math.Max(320, actions.Top - 68));
        }

        private async Task SearchAllUpdatesAsync(bool force)
        {
            List<DriverUpdate> drivers = null;
            List<ProgramUpdate> programs = null;
            List<WindowsSystemUpdate> windowsUpdates = null;
            string driverError = string.Empty;
            string programError = string.Empty;
            string windowsError = string.Empty;
            bool wingetAvailable = await Task.Run(delegate { return ProgramUpdater.IsAvailable(); });
            await RunWork("Consultando atualizações...", delegate(CancellationToken token, IProgress<string> progress)
            {
                Task programTask = Task.Run(delegate
                {
                    if (!wingetAvailable) { programError = "WinGet não disponível"; return; }
                    try { programs = CachedAnalysis.SearchProgramUpdates(force, token, progress); }
                    catch (Exception ex) { programError = ex.Message; }
                }, token);
                Task windowsTask = Task.Run(delegate
                {
                    try { windowsUpdates = WindowsUpdateInventory.Search(token, progress); }
                    catch (Exception ex) { windowsError = ex.Message; }
                    token.ThrowIfCancellationRequested();
                    try { drivers = CachedAnalysis.SearchDriverUpdates(force, token, progress); }
                    catch (Exception ex) { driverError = ex.Message; }
                }, token);
                try { Task.WaitAll(programTask, windowsTask); }
                catch (AggregateException) { token.ThrowIfCancellationRequested(); throw; }
                token.ThrowIfCancellationRequested();
                if (drivers == null && programs == null && windowsUpdates == null) throw new InvalidOperationException("Nenhuma origem respondeu. " + windowsError + " " + driverError + " " + programError);
                return "Windows: " + (windowsUpdates == null ? "indisponível" : windowsUpdates.Count.ToString()) + " • Drivers: " + (drivers == null ? "indisponível" : drivers.Count.ToString()) + " • Aplicativos: " + (programs == null ? "indisponível" : programs.Count.ToString());
            }, false);

            if (drivers == null && programs == null && windowsUpdates == null)
            {
                _updatesSearched = true;
                ApplyUnifiedUpdateFilter();
                int previous = _windowsUpdates.Count + _driverUpdates.Count + _programUpdates.Count;
                _updateQueueSummary.Text = previous == 0 ? "Verificação não concluída" : "Verificação não concluída • exibindo dados anteriores";
                if (previous == 0)
                {
                    _updateQueueEmpty.SetMessage("Não foi possível verificar", "Confira a conexão e os serviços do Windows Update e tente novamente.");
                    _updateQueueEmpty.BringToFront();
                }
                return;
            }

            if (drivers != null) _driverUpdates = drivers;
            if (programs != null) _programUpdates = programs;
            if (windowsUpdates != null) _windowsUpdates = windowsUpdates;
            _updatesSearched = true;
            ApplyUnifiedUpdateFilter();
            int total = _windowsUpdates.Count + _driverUpdates.Count + _programUpdates.Count;
            _updateQueueSummary.Text = total == 0 ? "Tudo atualizado" : total + (total == 1 ? " atualização disponível" : " atualizações disponíveis");
            if (!string.IsNullOrEmpty(windowsError) || !string.IsNullOrEmpty(driverError) || !string.IsNullOrEmpty(programError)) _updateQueueSummary.Text += " • consulta parcial";
        }

        private void ApplyUnifiedUpdateFilter()
        {
            if (_updateQueueGrid == null) return;
            SyncUnifiedUpdateSelection();
            string filter = _updateQueueFilter == null ? "Todos" : Convert.ToString(_updateQueueFilter.SelectedItem);
            string search = _updateQueueSearch == null ? string.Empty : _updateQueueSearch.Text.Trim();
            _updateQueueGrid.Rows.Clear();
            if (string.Equals(filter, "Todos", StringComparison.OrdinalIgnoreCase) || string.Equals(filter, "Windows", StringComparison.OrdinalIgnoreCase))
            {
                foreach (WindowsSystemUpdate item in _windowsUpdates.Where(value => MatchesUpdateSearch(value.Title, search)))
                {
                    int index = _updateQueueGrid.Rows.Add(false, "Windows", item.Title, "—", "Pendente", V2Engine.FormatBytes(item.DownloadBytes) + (item.RebootRequired ? " • reinício" : string.Empty), "Abrir");
                    DataGridViewRow row = _updateQueueGrid.Rows[index];
                    row.Tag = item;
                    row.Cells["Selected"].ReadOnly = true;
                    row.Cells["Selected"].Style.ForeColor = Theme.Muted;
                    if (item.Mandatory) row.DefaultCellStyle.ForeColor = Theme.Warning;
                }
            }
            if (string.Equals(filter, "Todos", StringComparison.OrdinalIgnoreCase) || string.Equals(filter, "Drivers", StringComparison.OrdinalIgnoreCase))
            {
                foreach (DriverUpdate item in _driverUpdates.Where(value => MatchesUpdateSearch(value.Title + " " + value.Provider + " " + value.Classification, search)))
                {
                    int index = _updateQueueGrid.Rows.Add(item.Selected, "Driver", item.Title, EmptyAsDash(item.InstalledVersion), EmptyAsDash(item.AvailableVersion), item.Classification + (item.RebootRequired ? " • reinício" : string.Empty), "Abrir");
                    _updateQueueGrid.Rows[index].Tag = item;
                    if (item.IsOlderRisk) _updateQueueGrid.Rows[index].DefaultCellStyle.ForeColor = Theme.Warning;
                }
            }
            if (string.Equals(filter, "Todos", StringComparison.OrdinalIgnoreCase) || string.Equals(filter, "Aplicativos", StringComparison.OrdinalIgnoreCase))
            {
                foreach (ProgramUpdate item in _programUpdates.Where(value => MatchesUpdateSearch(value.Name + " " + value.PackageId + " " + value.InstalledVersion + " " + value.AvailableVersion, search)))
                {
                    int index = _updateQueueGrid.Rows.Add(item.Selected, "Aplicativo", item.Name, item.InstalledVersion, item.AvailableVersion, "WinGet", string.Empty);
                    _updateQueueGrid.Rows[index].Tag = item;
                }
            }
            bool hasRows = _updateQueueGrid.Rows.Count > 0;
            _updateQueueGrid.Visible = hasRows;
            _updateQueueEmpty.Visible = !hasRows;
            if (!hasRows)
            {
                bool filtered = !string.IsNullOrEmpty(search) || !string.Equals(filter, "Todos", StringComparison.OrdinalIgnoreCase);
                _updateQueueEmpty.SetMessage(!_updatesSearched ? "Verificação ainda não executada" : filtered ? "Nenhum resultado" : "Tudo atualizado", !_updatesSearched ? "Clique em Verificar atualizações para consultar Windows, drivers e aplicativos." : filtered ? "Ajuste o filtro ou a pesquisa para ver outros itens." : "Nenhuma atualização foi encontrada nas origens consultadas.");
                _updateQueueEmpty.BringToFront();
            }
            UpdateInstallActionState();
        }

        private void SyncUnifiedUpdateSelection()
        {
            if (_updateQueueGrid == null) return;
            foreach (DataGridViewRow row in _updateQueueGrid.Rows)
            {
                if (row.IsNewRow) continue;
                bool selected = Convert.ToBoolean(row.Cells["Selected"].Value ?? false);
                DriverUpdate driver = row.Tag as DriverUpdate;
                ProgramUpdate program = row.Tag as ProgramUpdate;
                if (driver != null) driver.Selected = selected;
                if (program != null) program.Selected = selected;
            }
        }

        private void UpdateInstallActionState()
        {
            if (_installUpdatesButton == null) return;
            bool hasSelection = _driverUpdates.Any(item => item.Selected) || _programUpdates.Any(item => item.Selected);
            _installUpdatesButton.Enabled = hasSelection;
            _installUpdatesButton.Visible = hasSelection;
            SetButtonColor(_installUpdatesButton, hasSelection ? Theme.Success : Theme.Secondary);
        }

        private async Task InstallSelectedUpdatesAsync()
        {
            SyncUnifiedUpdateSelection();
            List<DriverUpdate> drivers = _driverUpdates.Where(item => item.Selected).ToList();
            List<ProgramUpdate> programs = _programUpdates.Where(item => item.Selected).ToList();
            if (drivers.Count + programs.Count == 0)
            {
                MessageBox.Show(this, "Selecione ao menos uma atualização.", "Atualizações", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (drivers.Count > 0 && !Optimizer.IsAdministrator())
            {
                if (MessageBox.Show(this, "Os drivers selecionados exigem administrador. Reabrir o Otimizador agora?", "Atualizações", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) RunAsAdmin(null, EventArgs.Empty);
                return;
            }
            if (drivers.Count > 0)
            {
                DriverSafetyStatus safety = await Task.Run(delegate { return DriverManager.ReadSafetyStatus(); });
                string block = DriverManager.ValidateFirmwareSelection(drivers, safety);
                if (!string.IsNullOrEmpty(block)) { MessageBox.Show(this, block, "Proteção de BIOS e firmware", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            }
            string message = "Instalar as atualizações selecionadas?\r\n\r\nDrivers: " + drivers.Count + "\r\nAplicativos: " + programs.Count + "\r\n\r\nUm backup será criado antes de alterar drivers.";
            if (MessageBox.Show(this, message, "Confirmar atualizações", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string result = await RunWork("Instalando atualizações...", delegate(CancellationToken token, IProgress<string> progress)
            {
                var report = new StringBuilder();
                if (drivers.Count > 0) report.AppendLine(DriverManager.InstallUpdates(drivers.Select(item => item.UpdateId), token, progress));
                if (programs.Count > 0) { if (report.Length > 0) report.AppendLine().AppendLine(); report.AppendLine(ProgramUpdater.InstallUpdates(programs.Select(item => item.PackageId), token, progress)); }
                return report.ToString().TrimEnd();
            });
            ShowTextDialog("Resultado das atualizações", result);
            CachedAnalysis.InvalidateDrivers();
            AnalysisCache.Invalidate("program-updates");
            await SearchAllUpdatesAsync(true);
        }

        private void UpdateQueueLinkClicked(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _updateQueueGrid.Columns[e.ColumnIndex].Name != "OfficialSite") return;
            DriverUpdate driver = _updateQueueGrid.Rows[e.RowIndex].Tag as DriverUpdate;
            if (_updateQueueGrid.Rows[e.RowIndex].Tag is WindowsSystemUpdate) { DriverManager.OpenWindowsUpdate(); return; }
            if (driver == null || string.IsNullOrWhiteSpace(driver.SupportUrl)) return;
            try { DriverManager.OpenOfficialSupport(driver.SupportUrl); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Site oficial", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private static bool MatchesUpdateSearch(string value, string search)
        {
            return string.IsNullOrEmpty(search) || (value ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value;
        }

        private void PopulateDriverUpdates(List<DriverUpdate> found)
        {
            _driverUpdates = found ?? new List<DriverUpdate>();
            _updatesSearched = true;
            ApplyUnifiedUpdateFilter();
            UpdateUnifiedUpdateSummary();
        }

        private void PopulateProgramUpdates(List<ProgramUpdate> found, string wingetVersion)
        {
            _programUpdates = found ?? new List<ProgramUpdate>();
            _updatesSearched = true;
            ApplyUnifiedUpdateFilter();
            UpdateUnifiedUpdateSummary();
            if (_updateQueueSummary != null && !string.IsNullOrEmpty(wingetVersion)) _updateQueueSummary.Text += " • WinGet " + wingetVersion.TrimStart('v', 'V');
        }

        private void UpdateUnifiedUpdateSummary()
        {
            if (_updateQueueSummary == null) return;
            int total = _windowsUpdates.Count + _driverUpdates.Count + _programUpdates.Count;
            _updateQueueSummary.Text = total == 0 ? "Tudo atualizado" : total + (total == 1 ? " atualização disponível" : " atualizações disponíveis");
        }

        private async Task RunEnergyDiagnostic()
        {
            string result = await RunWork("Gerando diagnóstico de energia...", delegate(CancellationToken t, IProgress<string> p) { return WindowsMaintenance.GenerateEnergyReport(t, p); });
            if (!string.IsNullOrWhiteSpace(WindowsMaintenance.LatestEnergyReportPath) && result.IndexOf("relatório criado", StringComparison.OrdinalIgnoreCase) >= 0 &&
                MessageBox.Show(this, "Relatório criado. Abrir agora?", "Diagnóstico de energia", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                WindowsMaintenance.OpenLatestEnergyReport();
        }
    }
}
