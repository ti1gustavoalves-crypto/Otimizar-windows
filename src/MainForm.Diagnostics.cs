using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2
    {
        private TabPage BuildDiagnosticsTab()
        {
            var page = NewPage("Diagnóstico");
            page.Controls.Add(new Label { Text = "Diagnóstico do sistema", Location = new Point(20, 17), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 13f) });
            _diagnosticStatus = new Label { Text = "Temperaturas, discos, inicialização e estabilidade", Location = new Point(210, 20), Size = new Size(300, 24), AutoEllipsis = true, ForeColor = Theme.Muted };
            var trends = ButtonFactory("Tendências", 520, 10, 105, Theme.Secondary);
            var benchmark = ButtonFactory("Benchmark", 635, 10, 125, Theme.Secondary);
            var details = ButtonFactory("Ver detalhes", 770, 10, 120, Theme.Secondary);
            var refresh = ButtonFactory("Atualizar", 900, 10, 110, Theme.Primary);
            trends.Click += delegate { using (var dialog = new TrendForm()) dialog.ShowDialog(this); };
            benchmark.Click += async delegate { await StartBenchmark(); };
            details.Click += delegate { if (_diagnosticSnapshot != null) ShowTextDialog("Diagnóstico detalhado", DetailedDiagnosticText(_diagnosticSnapshot)); };
            refresh.Click += async delegate { await LoadDiagnostics(true); };
            _diagnosticCards = new FlowLayoutPanel
            {
                Location = new Point(20, 58),
                Size = new Size(1000, 330),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Theme.SurfaceDark,
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true,
                Padding = new Padding(9),
                WrapContents = true
            };
            page.Controls.Add(new Label { Text = "Histórico de processos — últimos 5 minutos", Location = new Point(20, 405), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 9f) });
            _processHistoryGrid = Grid(20, 432, 1000, 160);
            _processHistoryGrid.Columns.Add("Name", "Aplicativo");
            _processHistoryGrid.Columns[0].Width = 390;
            _processHistoryGrid.Columns.Add("AverageCpu", "CPU média");
            _processHistoryGrid.Columns[1].Width = 130;
            _processHistoryGrid.Columns.Add("PeakCpu", "Pico de CPU");
            _processHistoryGrid.Columns[2].Width = 130;
            _processHistoryGrid.Columns.Add("PeakMemory", "Pico de RAM");
            _processHistoryGrid.Columns[3].Width = 150;
            _processHistoryGrid.Columns.Add("Samples", "Amostras");
            _processHistoryGrid.Columns[4].Width = 100;
            _processHistoryGrid.ReadOnly = true;
            page.Controls.Add(_diagnosticStatus);
            page.Controls.Add(trends);
            page.Controls.Add(benchmark);
            page.Controls.Add(details);
            page.Controls.Add(refresh);
            page.Controls.Add(_diagnosticCards);
            page.Controls.Add(_processHistoryGrid);
            page.Enter += async delegate
            {
                if (_diagnosticsLoaded) return;
                while (_cts != null && !IsDisposed) await Task.Delay(200);
                if (!IsDisposed) await LoadDiagnostics(false);
            };
            return page;
        }

        private async Task LoadDiagnostics(bool force)
        {
            if (_diagnosticsLoaded && !force) return;
            DiagnosticSnapshot snapshot = null;
            _diagnosticStatus.Text = "Lendo diagnósticos...";
            await RunWork("Lendo diagnósticos...", delegate(CancellationToken t, IProgress<string> p)
            {
                p.Report("Lendo sensores e saúde dos discos...");
                snapshot = AdvancedEngine.ReadDiagnostics();
                return DiagnosticReport(snapshot);
            }, false);
            if (snapshot == null) { _diagnosticStatus.Text = "Não foi possível concluir o diagnóstico"; return; }
            _diagnosticSnapshot = snapshot;
            _diagnosticsLoaded = true;
            PopulateDiagnostics(snapshot);
            UpdateProcessHistoryGrid();
            _diagnosticStatus.Text = "Atualizado em " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        }

        private async Task StartBenchmark()
        {
            BenchmarkSession existing = BenchmarkManager.ReadSession();
            if (existing != null && existing.PendingRestart)
            {
                if (MessageBox.Show(this, "Já existe um benchmark aguardando reinicialização. Substituir a linha de base?", "Benchmark", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    ShowTextDialog("Benchmark", BenchmarkManager.BuildComparison(existing));
                    return;
                }
            }
            else if (existing != null && existing.After != null && MessageBox.Show(this, "Iniciar um novo benchmark e substituir o comparativo anterior?", "Benchmark", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (MessageBox.Show(this, "O computador será medido por 10 segundos. Depois será necessário reiniciar o Windows normalmente. Continuar?", "Benchmark pós-reinicialização", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            string result = await RunWork("Medindo linha de base...", delegate(CancellationToken t, IProgress<string> p) { return BenchmarkManager.Start(t, p); });
            _diagnosticsLoaded = false;
            ShowTextDialog("Benchmark iniciado", result);
            await LoadDiagnostics(true);
        }

        private async Task TryCompletePendingBenchmark()
        {
            BenchmarkSession session = BenchmarkManager.ReadSession();
            if (session == null || !session.PendingRestart) return;
            string result = await RunWork("Verificando benchmark pendente...", delegate(CancellationToken t, IProgress<string> p) { return BenchmarkManager.TryComplete(t, p); }, false);
            if (result.IndexOf("BENCHMARK CONCLUÍDO", StringComparison.OrdinalIgnoreCase) < 0) return;
            V2Engine.SaveReport(result);
            _diagnosticsLoaded = false;
            ShowTextDialog("Benchmark concluído", result);
        }

        private void BeginHistoryCapture()
        {
            if (_liveMetrics == null || Interlocked.Exchange(ref _historyWriteInProgress, 1) != 0) return;
            var metrics = new SystemMetrics
            {
                TotalRamGb = _liveMetrics.TotalRamGb,
                FreeRamGb = _liveMetrics.FreeRamGb,
                FreeDiskGb = _liveMetrics.FreeDiskGb,
                TotalDiskGb = _liveMetrics.TotalDiskGb,
                FreeDiskPercent = _liveMetrics.FreeDiskPercent,
                CpuUsagePercent = _liveMetrics.CpuUsagePercent
            };
            List<ProcessActivity> processes = _lastProcessActivities.Select(item => new ProcessActivity { Name = item.Name, CpuPercent = item.CpuPercent, WorkingSetBytes = item.WorkingSetBytes }).ToList();
            Task.Run(delegate
            {
                try { PersistentMetricStore.Append(metrics, processes, AdvancedEngine.ReadTemperatures()); }
                finally { Interlocked.Exchange(ref _historyWriteInProgress, 0); }
            });
        }

        private void PopulateDiagnostics(DiagnosticSnapshot snapshot)
        {
            _diagnosticCards.SuspendLayout();
            _diagnosticCards.Controls.Clear();
            BenchmarkSession benchmark = BenchmarkManager.ReadSession();
            PerformanceComparison comparison = snapshot.Comparison;
            string comparisonMain;
            string comparisonDetail;
            if (benchmark != null && benchmark.PendingRestart)
            {
                comparisonMain = "Aguardando reinicialização";
                comparisonDetail = "A linha de base está salva e será comparada no próximo login";
            }
            else if (benchmark != null && benchmark.Before != null && benchmark.After != null)
            {
                comparisonMain = string.Format(CultureInfo.CurrentCulture, "CPU {0:N1}% → {1:N1}%", benchmark.Before.AverageCpuPercent, benchmark.After.AverageCpuPercent);
                comparisonDetail = string.Format(CultureInfo.CurrentCulture, "RAM livre {0:N1} → {1:N1} GB  •  Disco {2:+0.0;-0.0;0.0} GB", benchmark.Before.AverageFreeRamGb, benchmark.After.AverageFreeRamGb, benchmark.After.FreeDiskGb - benchmark.Before.FreeDiskGb);
            }
            else
            {
                comparisonMain = comparison == null ? "Aguardando medição" : string.Format(CultureInfo.CurrentCulture, "RAM {0:+0.0;-0.0;0.0} GB  •  Disco {1:+0.0;-0.0;0.0} GB", comparison.AfterFreeRamGb - comparison.BeforeFreeRamGb, comparison.AfterFreeDiskGb - comparison.BeforeFreeDiskGb);
                comparisonDetail = comparison == null ? "Use Benchmark para comparar após reiniciar" : comparison.Operation + "  •  CPU " + comparison.BeforeCpuPercent.ToString("N0") + "% → " + comparison.AfterCpuPercent.ToString("N0") + "%";
            }
            _diagnosticCards.Controls.Add(DiagnosticCard("BENCHMARK / ANTES E DEPOIS", comparisonMain, comparisonDetail, false));

            TemperatureReading hottest = snapshot.Temperatures.OrderByDescending(item => item.Celsius).FirstOrDefault();
            string temperatureMain = hottest == null ? "Sensor não exposto" : hottest.Celsius.ToString("N0", CultureInfo.CurrentCulture) + " °C  •  " + hottest.Name;
            string temperatureDetail = hottest == null ? OptionalSensorProvider.Status + "  •  Compatível com ACPI/WMI" : snapshot.ClockStatus + "  •  " + hottest.Source;
            _diagnosticCards.Controls.Add(DiagnosticCard("TEMPERATURA / THROTTLING", temperatureMain, temperatureDetail, hottest != null && hottest.Celsius >= 85));

            bool diskWarning = snapshot.Disks.Any(item => item.Warning);
            string diskMain = snapshot.Disks.Count == 0 ? "Nenhum dado disponível" : snapshot.Disks.Count + (snapshot.Disks.Count == 1 ? " dispositivo" : " dispositivos") + "  •  " + (diskWarning ? "atenção" : "sem alertas");
            string diskDetail = snapshot.Disks.Count == 0 ? snapshot.TrimStatus : snapshot.Disks[0].Name + "  •  " + snapshot.Disks[0].Health + "  •  " + snapshot.Disks[0].Life + "  •  " + snapshot.TrimStatus;
            _diagnosticCards.Controls.Add(DiagnosticCard("SAÚDE DOS DISCOS", diskMain, diskDetail, diskWarning));

            StartupMeasurement boot = snapshot.Startup.FirstOrDefault(item => item.Name == "Inicialização do Windows");
            StartupMeasurement slowest = snapshot.Startup.FirstOrDefault(item => item.Name != "Inicialização do Windows");
            string bootMain = boot == null ? "Medição não disponível" : TimeSpan.FromMilliseconds(boot.DurationMilliseconds).TotalSeconds.ToString("N1", CultureInfo.CurrentCulture) + " s para iniciar";
            string bootDetail = slowest == null ? (Optimizer.IsAdministrator() ? "O Windows ainda não registrou impacto por aplicativo" : "Reabra como administrador para acessar os eventos") : slowest.Name + "  •  " + (slowest.DurationMilliseconds / 1000.0).ToString("N1", CultureInfo.CurrentCulture) + " s de impacto";
            _diagnosticCards.Controls.Add(DiagnosticCard("INICIALIZAÇÃO MEDIDA", bootMain, bootDetail, boot != null && boot.DurationMilliseconds > 60000));

            StabilityDiagnostic stability = snapshot.Stability;
            string stabilityMain = stability.UnexpectedShutdowns + " desligamentos inesperados  •  " + stability.SystemFailures + " falhas";
            string stabilityDetail = "Ligado há " + Math.Floor(stability.Uptime.TotalDays).ToString("N0", CultureInfo.CurrentCulture) + " dia(s)" + (stability.PendingRestart ? "  •  Reinicialização pendente" : "  •  Sem reinicialização pendente");
            _diagnosticCards.Controls.Add(DiagnosticCard("ESTABILIDADE — 30 DIAS", stabilityMain, stabilityDetail, stability.UnexpectedShutdowns > 0 || stability.SystemFailures > 0));

            string recommendationMain = string.Join("  •  ", snapshot.Recommendations.Take(2).Select(item => item.Title).ToArray());
            string recommendationDetail = string.Join("  |  ", snapshot.Recommendations.Take(2).Select(item => item.Detail).ToArray());
            _diagnosticCards.Controls.Add(DiagnosticCard("RECOMENDAÇÕES", recommendationMain, recommendationDetail, snapshot.Recommendations.Any(item => item.Severity == "Alta")));
            for (int i = 0; i < _diagnosticCards.Controls.Count; i++)
                _diagnosticCards.SetFlowBreak(_diagnosticCards.Controls[i], (i + 1) % 3 == 0);
            _diagnosticCards.ResumeLayout();
        }

        private DashboardPanel DiagnosticCard(string title, string main, string detail, bool warning)
        {
            var card = DashboardCard(0, 0, 306, 137);
            card.Margin = new Padding(5);
            card.Controls.Add(new Label { Text = title, Location = new Point(16, 13), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8.3f) });
            card.Controls.Add(new Label { Text = main, Location = new Point(15, 39), Size = new Size(274, 35), AutoEllipsis = true, ForeColor = warning ? Theme.Warning : Theme.Text, Font = new Font("Segoe UI Semibold", 12f) });
            card.Controls.Add(new Label { Text = detail, Location = new Point(16, 80), Size = new Size(274, 42), AutoEllipsis = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8.5f) });
            return card;
        }

        private static string DiagnosticReport(DiagnosticSnapshot snapshot)
        {
            if (snapshot == null) return "Diagnóstico indisponível.";
            var report = new StringBuilder("DIAGNÓSTICO DO SISTEMA\r\n" + new string('=', 72) + "\r\n");
            foreach (RecommendationItem item in snapshot.Recommendations) report.AppendLine(item.Severity + " | " + item.Title + " | " + item.Detail);
            report.AppendLine("TRIM: " + snapshot.TrimStatus);
            report.AppendLine("Frequência: " + snapshot.ClockStatus);
            report.AppendLine("Assinatura: " + snapshot.SignatureStatus);
            return report.ToString();
        }

        private static string DetailedDiagnosticText(DiagnosticSnapshot snapshot)
        {
            var text = new StringBuilder(DiagnosticReport(snapshot));
            text.AppendLine("\r\nTEMPERATURAS");
            if (snapshot.Temperatures.Count == 0) text.AppendLine("Sensores não expostos ao Windows.");
            foreach (TemperatureReading item in snapshot.Temperatures) text.AppendLine(item.Name + " | " + item.Celsius.ToString("N1", CultureInfo.CurrentCulture) + " °C | " + item.Source);
            text.AppendLine("\r\nDISCOS");
            foreach (DiskDiagnostic item in snapshot.Disks) text.AppendLine(item.Name + " | " + item.Type + " | " + item.Health + " | Vida: " + item.Life + " | " + item.Temperature);
            text.AppendLine("\r\nINICIALIZAÇÃO");
            if (snapshot.Startup.Count == 0) text.AppendLine(Optimizer.IsAdministrator() ? "O Windows não forneceu medições recentes." : "Reabra como administrador para acessar as medições do Windows.");
            foreach (StartupMeasurement item in snapshot.Startup) text.AppendLine(item.Name + " | " + (item.DurationMilliseconds / 1000.0).ToString("N1", CultureInfo.CurrentCulture) + " s");
            return text.ToString();
        }

        private void UpdateProcessHistoryGrid()
        {
            if (_processHistoryGrid == null) return;
            _processHistoryGrid.Rows.Clear();
            foreach (ProcessHistorySummary item in _processHistory.Summaries(12))
                _processHistoryGrid.Rows.Add(item.Name, item.AverageCpu.ToString("N1", CultureInfo.CurrentCulture) + "%", item.PeakCpu.ToString("N1", CultureInfo.CurrentCulture) + "%", V2Engine.FormatBytes(item.PeakMemory), item.Samples);
        }
    }
}
