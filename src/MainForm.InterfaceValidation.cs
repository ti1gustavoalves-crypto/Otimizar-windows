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
            PerformLayout();
            var problems = new List<string>();
            if (_tabs == null || _tabs.TabPages.Count != 7) problems.Add("A navegação deve possuir sete áreas.");
            if (_navigationButtons == null || _tabs == null || _navigationButtons.Length != _tabs.TabPages.Count) problems.Add("A navegação lateral não corresponde às áreas disponíveis.");
            if (_tabs != null)
            {
                int selected = _tabs.SelectedIndex;
                string[] names = _tabs.TabPages.Cast<TabPage>().Select(page => page.Text).ToArray();
                if (names.Any(string.IsNullOrWhiteSpace) || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length) problems.Add("Existem áreas sem nome ou com nomes repetidos.");
                foreach (TabPage page in _tabs.TabPages)
                {
                    page.CreateControl();
                    page.PerformLayout();
                    if (!page.AutoScroll) problems.Add("A área " + page.Text + " não possui proteção contra conteúdo excedente.");
                    ValidateControlTree(page, page.Text, problems);
                }
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
            if (_fullServiceButton == null || _fullServiceButton.Width < 180) problems.Add("O atendimento completo não está acessível no painel principal.");
            return string.Join(" ", problems.Distinct().ToArray());
        }

        private static void ValidateControlTree(Control parent, string area, List<string> problems)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Width <= 0 || control.Height <= 0) problems.Add(area + ": controle sem dimensão válida.");
                var button = control as Button;
                if (button != null && button.Visible && !string.IsNullOrWhiteSpace(button.Text))
                {
                    int required = TextRenderer.MeasureText(button.Text, button.Font).Width + 22;
                    if (required > button.ClientSize.Width + 4) problems.Add(area + ": texto cortado no botão " + button.Text + ".");
                }
                var grid = control as DataGridView;
                if (grid != null && grid.Columns.Cast<DataGridViewColumn>().Any(column => column.Visible && column.Width <= 0)) problems.Add(area + ": coluna visível sem largura.");
                if (control.HasChildren) ValidateControlTree(control, area, problems);
            }
        }
    }
}
