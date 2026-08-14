using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2
    {
        internal string ValidateInterfaceForTesting(Size windowSize)
        {
            Size = windowSize;
            CreateControl();
            if (!Visible) Show();
            ApplyResponsiveShell();
            PerformLayout();
            Application.DoEvents();
            var problems = new List<string>();
            if (_tabs == null || _tabs.TabPages.Count != 5) problems.Add("A navegação deve possuir cinco áreas.");
            if (_navigationButtons == null || _tabs == null || _navigationButtons.Length != _tabs.TabPages.Count) problems.Add("A navegação lateral não corresponde às áreas disponíveis.");
            if (_tabs != null)
            {
                int selected = _tabs.SelectedIndex;
                string[] names = _tabs.TabPages.Cast<TabPage>().Select(page => page.Text).ToArray();
                if (names.Any(string.IsNullOrWhiteSpace) || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length) problems.Add("Existem áreas sem nome ou com nomes repetidos.");
                foreach (TabPage page in _tabs.TabPages)
                {
                    _tabs.SelectedTab = page;
                    page.CreateControl();
                    page.PerformLayout();
                    if (!page.AutoScroll) problems.Add("A área " + page.Text + " não possui proteção contra conteúdo excedente.");
                    ValidateControlTree(page, page.Text, problems);
                }
                ValidateNestedTabs(_maintenanceTabs, "Manutenção", problems);
                ValidateNestedTabs(_systemTabs, "Sistema", problems);
                ValidateUpdateModes(problems);
                try
                {
                    for (int index = 0; index < _tabs.TabPages.Count; index++)
                    {
                        _tabs.SelectedIndex = index;
                        _tabs.SelectedTab.PerformLayout();
                    }
                }
                catch (Exception ex) { problems.Add("A navegação entre áreas falhou: " + ex.GetType().Name + "."); }
                finally { _tabs.SelectedIndex = selected; }
            }
            if (_fullServiceButton == null || _fullServiceButton.Width < 180 || _fullServiceButton.Text.IndexOf("atendimento", StringComparison.OrdinalIgnoreCase) < 0) problems.Add("A ação principal do atendimento não está acessível no painel.");
            return string.Join(" ", problems.Distinct().ToArray());
        }

        private static void ValidateNestedTabs(TabControl tabs, string area, List<string> problems)
        {
            if (tabs == null) { problems.Add(area + ": navegação interna indisponível."); return; }
            int selected = tabs.SelectedIndex;
            foreach (TabPage page in tabs.TabPages)
            {
                tabs.SelectedTab = page;
                page.CreateControl();
                page.PerformLayout();
                ValidateControlTree(page, area + " / " + page.Text, problems);
            }
            tabs.SelectedIndex = selected;
        }

        private void ValidateUpdateModes(List<string> problems)
        {
            TabPage page = _tabs.TabPages[(int)AppSection.Updates];
            _tabs.SelectedTab = page;
            Button programs = FindButton(page, "Aplicativos");
            Button drivers = FindButton(page, "Drivers");
            if (programs == null || drivers == null) { problems.Add("Atualizações: filtros de origem indisponíveis."); return; }
            programs.PerformClick();
            Application.DoEvents();
            page.PerformLayout();
            ValidateControlTree(page, "Atualizações / Aplicativos", problems);
            drivers.PerformClick();
            Application.DoEvents();
        }

        private static Button FindButton(Control parent, string text)
        {
            foreach (Control control in parent.Controls)
            {
                var button = control as Button;
                if (button != null && string.Equals(button.Text, text, StringComparison.OrdinalIgnoreCase)) return button;
                Button child = FindButton(control, text);
                if (child != null) return child;
            }
            return null;
        }

        private static void ValidateControlTree(Control parent, string area, List<string> problems)
        {
            foreach (Control control in parent.Controls)
            {
                if (!control.Visible) continue;
                if (control.Width <= 0 || control.Height <= 0) problems.Add(area + ": controle sem dimensão válida.");
                if (!(control is TabControl) && parent.ClientSize.Width > 0 && (control.Left < -4 || control.Right > parent.ClientSize.Width + 6))
                    problems.Add(area + ": controle fora da largura disponível: " + (string.IsNullOrWhiteSpace(control.Text) ? control.GetType().Name : control.Text) + ".");
                var scrollableParent = parent as ScrollableControl;
                bool allowsVerticalScroll = scrollableParent != null && scrollableParent.AutoScroll;
                if (!(control is TabControl) && !allowsVerticalScroll && parent.ClientSize.Height > 0 && (control.Top < -4 || control.Bottom > parent.ClientSize.Height + 6))
                    problems.Add(area + ": controle fora da altura disponível: " + (string.IsNullOrWhiteSpace(control.Text) ? control.GetType().Name : control.Text) + ".");
                var button = control as Button;
                if (button != null && button.Visible && !string.IsNullOrWhiteSpace(button.Text))
                {
                    int required = TextRenderer.MeasureText(button.Text, button.Font).Width + 22;
                    if (required > button.ClientSize.Width + 4) problems.Add(area + ": texto cortado no botão " + button.Text + ".");
                }
                var grid = control as DataGridView;
                if (grid != null && grid.Columns.Cast<DataGridViewColumn>().Any(column => column.Visible && column.Width <= 0)) problems.Add(area + ": coluna visível sem largura.");
                if (control.HasChildren && !(control is DataGridView)) ValidateControlTree(control, area, problems);
            }
        }
    }
}
