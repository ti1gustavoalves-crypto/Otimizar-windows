using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2
    {
        private TabPage BuildControlTab()
        {
            var page = NewPage("Controle");
            var automatic = DashboardCard(20, 20, 490, 195);
            automatic.Controls.Add(new Label { Text = "Automação", Location = new Point(20, 16), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 12f) });
            automatic.Controls.Add(new Label { Text = "Comportamentos opcionais e reversíveis", Location = new Point(20, 44), AutoSize = true, ForeColor = Theme.Muted });
            _minimizeToTray = Option("Continuar monitorando ao minimizar", 20, 80, _advancedSettings.MinimizeToTray);
            _automaticProfiles = Option("Desempenho na tomada e equilíbrio na bateria", 20, 116, _advancedSettings.AutomaticPowerProfiles);
            _minimizeToTray.CheckedChanged += delegate { SaveAdvancedPreferences(); };
            _automaticProfiles.CheckedChanged += delegate { _lastPowerLineStatus = null; SaveAdvancedPreferences(); };
            automatic.Controls.Add(_minimizeToTray);
            automatic.Controls.Add(_automaticProfiles);
            automatic.Controls.Add(new Label { Text = "As trocas ocorrem somente quando a alimentação muda.", Location = new Point(22, 153), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) });

            var modes = DashboardCard(530, 20, 506, 195);
            modes.Controls.Add(new Label { Text = "Perfis temporários", Location = new Point(20, 16), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 12f) });
            modes.Controls.Add(new Label { Text = "Ajustam apenas o plano de energia", Location = new Point(20, 44), AutoSize = true, ForeColor = Theme.Muted });
            var game = ButtonFactory("Modo jogo", 20, 80, 140, Theme.Primary);
            var work = ButtonFactory("Modo trabalho", 174, 80, 140, Theme.Secondary);
            var restoreMode = ButtonFactory("Restaurar anterior", 328, 80, 158, Theme.Secondary);
            game.Click += delegate { MessageBox.Show(this, AdvancedEngine.ApplyTemporaryProfile(true), "Perfil temporário"); };
            work.Click += delegate { MessageBox.Show(this, AdvancedEngine.ApplyTemporaryProfile(false), "Perfil temporário"); };
            restoreMode.Click += delegate { MessageBox.Show(this, AdvancedEngine.RestoreTemporaryProfile(), "Perfil temporário"); };
            modes.Controls.Add(game);
            modes.Controls.Add(work);
            modes.Controls.Add(restoreMode);
            modes.Controls.Add(new Label { Text = "Nenhum serviço é encerrado e o plano anterior pode ser restaurado.", Location = new Point(22, 137), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) });

            var undo = DashboardCard(20, 235, 490, 270);
            undo.Controls.Add(new Label { Text = "Desfazer por categoria", Location = new Point(20, 16), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 12f) });
            undo.Controls.Add(new Label { Text = "Restaura somente o grupo escolhido", Location = new Point(20, 44), AutoSize = true, ForeColor = Theme.Muted });
            var section = new ComboBox { Location = new Point(20, 82), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text };
            section.Items.AddRange(new object[] { "Energia", "Tema", "Efeitos visuais", "Segundo plano", "Inicialização" });
            section.SelectedIndex = 0;
            var restoreSection = ButtonFactory("Restaurar", 316, 78, 150, Theme.Warning);
            restoreSection.Click += async delegate
            {
                string selected = Convert.ToString(section.SelectedItem);
                if (MessageBox.Show(this, "Restaurar a categoria " + selected + "?", "Restauração seletiva", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                await RunWork("Restaurando " + selected.ToLowerInvariant() + "...", delegate(CancellationToken t, IProgress<string> p) { return V2Engine.RestoreSection(selected, t, p); });
                await RefreshAudit();
            };
            var restoreQuarantine = ButtonFactory("Restaurar última quarentena", 20, 145, 230, Theme.Secondary);
            restoreQuarantine.Click += delegate { MessageBox.Show(this, AdvancedEngine.RestoreLatestQuarantine(), "Quarentena"); };
            undo.Controls.Add(section);
            undo.Controls.Add(restoreSection);
            undo.Controls.Add(restoreQuarantine);
            undo.Controls.Add(new Label { Text = "Limpezas definitivas não podem recriar arquivos.\r\nDuplicados movidos para quarentena podem ser restaurados.", Location = new Point(22, 201), Size = new Size(440, 50), ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) });

            var updates = DashboardCard(530, 235, 506, 270);
            updates.Controls.Add(new Label { Text = "Atualizações e versão", Location = new Point(20, 16), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 12f) });
            _updateStatus = new Label { Text = "Versão " + GetType().Assembly.GetName().Version + "  •  " + AdvancedEngine.ReadSignatureStatus(Application.ExecutablePath), Location = new Point(20, 48), Size = new Size(466, 44), AutoEllipsis = true, ForeColor = Theme.Muted };
            var check = ButtonFactory("Verificar atualização", 20, 102, 190, Theme.Primary);
            var notes = ButtonFactory("Notas da versão", 224, 102, 160, Theme.Secondary);
            var safetyTests = ButtonFactory("Testes de segurança", 20, 151, 190, Theme.Secondary);
            var logs = ButtonFactory("Logs técnicos", 224, 151, 160, Theme.Secondary);
            check.Click += async delegate { await CheckForUpdates(); };
            notes.Click += delegate { ShowTextDialog("Notas da versão", AdvancedEngine.ReadReleaseNotes()); };
            safetyTests.Click += async delegate
            {
                string result = await RunWork("Executando testes isolados...", delegate(CancellationToken t, IProgress<string> p) { return SafetyTestSuite.Run(t, p); });
                ShowTextDialog("Testes de segurança", result);
            };
            logs.Click += delegate { CrashLogger.OpenFolder(); };
            updates.Controls.Add(_updateStatus);
            updates.Controls.Add(check);
            updates.Controls.Add(notes);
            updates.Controls.Add(safetyTests);
            updates.Controls.Add(logs);
            updates.Controls.Add(new Label { Text = "Downloads exigem HTTPS e SHA-256. Logs removem nomes e caminhos pessoais.", Location = new Point(22, 207), Size = new Size(450, 40), ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) });

            page.Controls.Add(automatic);
            page.Controls.Add(modes);
            page.Controls.Add(undo);
            page.Controls.Add(updates);
            return page;
        }
    }
}
