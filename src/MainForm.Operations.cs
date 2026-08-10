using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2
    {
        private async Task<string> RunWork(string initialStatus, Func<CancellationToken, IProgress<string>, string> worker, bool saveReport = true)
        {
            if (_cts != null) return "Outra operação está em andamento. Aguarde a conclusão ou cancele a operação atual.";
            _cts = new CancellationTokenSource();
            _progress.Visible = true;
            _status.Location = new Point(194, 12);
            _progress.Style = ProgressBarStyle.Marquee;
            _cancel.Enabled = true;
            _status.Text = initialStatus;
            var progress = new Progress<string>(delegate(string message) { _status.Text = message; });
            try
            {
                string result = await Task.Run(delegate { return worker(_cts.Token, progress); }, _cts.Token);
                _status.Text = "Concluído em " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                if (saveReport) V2Engine.SaveReport(result);
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
                if (saveReport) V2Engine.SaveReport(result);
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
    }
}
