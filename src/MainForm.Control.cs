using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2
    {
        private TabPage BuildSettingsTab()
        {
            var page = NewPage("Ajustes");

            var automatic = DashboardCard(20, 20, 490, 260);
            automatic.Controls.Add(new Label { Text = "Automação", Location = new Point(20, 16), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 12f) });
            _minimizeToTray = Option("Continuar monitorando ao minimizar", 20, 58, _advancedSettings.MinimizeToTray);
            _automaticProfiles = Option("Adequar energia à tomada ou bateria", 20, 94, _advancedSettings.AutomaticPowerProfiles);
            _minimizeToTray.CheckedChanged += delegate { SaveAdvancedPreferences(); };
            _automaticProfiles.CheckedChanged += delegate { _lastPowerLineStatus = null; SaveAdvancedPreferences(); };
            automatic.Controls.Add(_minimizeToTray);
            automatic.Controls.Add(_automaticProfiles);
            automatic.Controls.Add(new Label { Text = "Manutenção automática", Location = new Point(20, 139), AutoSize = true, ForeColor = Theme.Muted });
            _schedule = new ComboBox { Location = new Point(20, 164), Width = 245, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text };
            _schedule.Items.AddRange(new object[] { "Desativada", "Semanal — segunda-feira", "Mensal — dia 1" });
            _schedule.SelectedIndex = V2Engine.ReadScheduleIndex();
            var scheduleSave = ButtonFactory("Salvar", 278, 160, 120, Theme.Primary);
            _maintenanceResult = new Label { Text = "", Location = new Point(20, 210), Size = new Size(440, 28), AutoEllipsis = true, ForeColor = Theme.Muted };
            scheduleSave.Click += async delegate
            {
                string result = await RunWork("Configurando agendamento...", delegate(CancellationToken t, IProgress<string> p) { return V2Engine.ConfigureSchedule(_schedule.SelectedIndex); });
                _maintenanceResult.Text = FirstResultLine(result, "Agendamento atualizado");
            };
            automatic.Controls.Add(_schedule);
            automatic.Controls.Add(scheduleSave);
            automatic.Controls.Add(_maintenanceResult);

            var recovery = DashboardCard(530, 20, 506, 260);
            recovery.Controls.Add(new Label { Text = "Recuperação", Location = new Point(20, 16), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 12f) });
            recovery.Controls.Add(new Label { Text = "Desfaça somente o necessário", Location = new Point(20, 45), AutoSize = true, ForeColor = Theme.Muted });
            var section = new ComboBox { Location = new Point(20, 82), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text };
            section.Items.AddRange(new object[] { "Energia", "Tema", "Efeitos visuais", "Segundo plano", "Inicialização" });
            section.SelectedIndex = 0;
            var restoreSection = ButtonFactory("Restaurar seção", 316, 78, 166, Theme.Warning);
            restoreSection.Click += async delegate
            {
                string selected = Convert.ToString(section.SelectedItem);
                if (MessageBox.Show(this, "Restaurar a categoria " + selected + "?", "Restauração seletiva", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await RunWork("Restaurando " + selected.ToLowerInvariant() + "...", delegate(CancellationToken t, IProgress<string> p) { return V2Engine.RestoreSection(selected, t, p); });
                await RefreshAudit();
            };
            var restoreQuarantine = ButtonFactory("Restaurar quarentena", 20, 139, 205, Theme.Secondary);
            restoreQuarantine.Click += delegate { MessageBox.Show(this, AdvancedEngine.RestoreLatestQuarantine(), "Quarentena"); };
            var restoreAll = ButtonFactory("Restaurar configuração original", 237, 139, 245, Theme.Secondary);
            restoreAll.Click += async delegate
            {
                if (MessageBox.Show(this, "Restaurar todas as configurações registradas antes da primeira otimização?", "Recuperação completa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                await RunWork("Restaurando configurações...", delegate(CancellationToken t, IProgress<string> p) { return V2Engine.Restore(t, p); });
                await RefreshAudit();
            };
            recovery.Controls.Add(section);
            recovery.Controls.Add(restoreSection);
            recovery.Controls.Add(restoreQuarantine);
            recovery.Controls.Add(restoreAll);

            var application = DashboardCard(20, 300, 1016, 190);
            application.Controls.Add(new Label { Text = "Aplicativo e suporte", Location = new Point(20, 16), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 12f) });
            _updateStatus = new Label { Text = "Versão " + GetType().Assembly.GetName().Version + "  •  " + AdvancedEngine.ReadSignatureStatus(Application.ExecutablePath), Location = new Point(20, 49), Size = new Size(965, 28), AutoEllipsis = true, ForeColor = Theme.Muted };
            var check = ButtonFactory("Verificar atualização", 20, 94, 190, Theme.Primary);
            var report = ButtonFactory("Relatório técnico", 222, 94, 175, Theme.Secondary);
            var reports = ButtonFactory("Relatórios salvos", 409, 94, 175, Theme.Secondary);
            var logs = ButtonFactory("Logs técnicos", 596, 94, 155, Theme.Secondary);
            check.Click += async delegate { await CheckForUpdates(); };
            report.Click += delegate { ShowTechnicalServiceReport(); };
            reports.Click += delegate { V2Engine.OpenReportsFolder(); };
            logs.Click += delegate { CrashLogger.OpenFolder(); };
            application.Controls.Add(_updateStatus);
            application.Controls.Add(check);
            application.Controls.Add(report);
            application.Controls.Add(reports);
            application.Controls.Add(logs);

            page.Controls.Add(automatic);
            page.Controls.Add(recovery);
            page.Controls.Add(application);
            return page;
        }
    }
}
