using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace CodexPerformanceOptimizer
{
    internal sealed class BenchmarkSample
    {
        public string CapturedUtc { get; set; }
        public string BootUtc { get; set; }
        public double AverageCpuPercent { get; set; }
        public double AverageFreeRamGb { get; set; }
        public double FreeDiskGb { get; set; }
        public long BootDurationMilliseconds { get; set; }
    }

    internal sealed class BenchmarkSession
    {
        public string StartedUtc { get; set; }
        public bool PendingRestart { get; set; }
        public BenchmarkSample Before { get; set; }
        public BenchmarkSample After { get; set; }
    }

    internal sealed class HistoricalProcessEntry
    {
        public string Name { get; set; }
        public double CpuPercent { get; set; }
        public long MemoryBytes { get; set; }
    }

    internal sealed class HistoricalMetricEntry
    {
        public string CapturedUtc { get; set; }
        public double CpuPercent { get; set; }
        public double MemoryUsedPercent { get; set; }
        public double DiskUsedPercent { get; set; }
        public double? HottestTemperatureCelsius { get; set; }
        public List<HistoricalProcessEntry> Processes { get; set; }
    }

    internal sealed class TrendProcessSummary
    {
        public string Name { get; set; }
        public double AverageCpuPercent { get; set; }
        public double PeakCpuPercent { get; set; }
        public long PeakMemoryBytes { get; set; }
        public int Samples { get; set; }
    }

    internal sealed class TrendSummary
    {
        public List<HistoricalMetricEntry> Points { get; set; }
        public List<TrendProcessSummary> Processes { get; set; }
        public double AverageCpuPercent { get; set; }
        public double PeakCpuPercent { get; set; }
        public double AverageMemoryUsedPercent { get; set; }
        public double PeakMemoryUsedPercent { get; set; }
        public double? PeakTemperatureCelsius { get; set; }
    }

    internal static class BenchmarkManager
    {
        private const string RunOnceKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
        private const string RunOnceName = "CodexPerformanceOptimizerBenchmark";
        private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Codex", "PerformanceOptimizer");
        private static readonly string SessionPath = Path.Combine(AppFolder, "benchmark-session.json");

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        public static BenchmarkSession ReadSession()
        {
            try
            {
                if (!File.Exists(SessionPath)) return null;
                return new JavaScriptSerializer().Deserialize<BenchmarkSession>(File.ReadAllText(SessionPath, Encoding.UTF8));
            }
            catch { return null; }
        }

        public static string Start(CancellationToken token, IProgress<string> progress)
        {
            progress.Report("Medindo o sistema em repouso por 10 segundos...");
            BenchmarkSample before = CaptureSample(token);
            var session = new BenchmarkSession { StartedUtc = DateTime.UtcNow.ToString("o"), PendingRestart = true, Before = before };
            Save(session);
            using (RegistryKey runOnce = Registry.CurrentUser.CreateSubKey(RunOnceKey))
                runOnce.SetValue(RunOnceName, "\"" + Application.ExecutablePath + "\" --benchmark-after-reboot", RegistryValueKind.String);
            return "BENCHMARK PÓS-REINICIALIZAÇÃO\r\n" + new string('=', 72) +
                "\r\nLinha de base registrada. Reinicie o Windows normalmente.\r\nO Otimizador abrirá uma vez após o login e concluirá a comparação automaticamente.\r\n" + Describe(before);
        }

        public static string TryComplete(CancellationToken token, IProgress<string> progress)
        {
            BenchmarkSession session = ReadSession();
            if (session == null || !session.PendingRestart || session.Before == null) return string.Empty;
            DateTime beforeBoot = ParseUtc(session.Before.BootUtc);
            DateTime currentBoot = ReadBootTimeUtc();
            if (currentBoot <= beforeBoot.AddMinutes(1)) return "Benchmark aguardando uma reinicialização real.";
            progress.Report("Aguardando o sistema estabilizar...");
            for (int i = 0; i < 10; i++) { token.ThrowIfCancellationRequested(); Thread.Sleep(500); }
            progress.Report("Medindo o sistema após a reinicialização...");
            session.After = CaptureSample(token);
            session.PendingRestart = false;
            Save(session);
            using (RegistryKey runOnce = Registry.CurrentUser.OpenSubKey(RunOnceKey, true)) if (runOnce != null) runOnce.DeleteValue(RunOnceName, false);
            return BuildComparison(session);
        }

        public static string Cancel()
        {
            try { if (File.Exists(SessionPath)) File.Delete(SessionPath); } catch { }
            using (RegistryKey runOnce = Registry.CurrentUser.OpenSubKey(RunOnceKey, true)) if (runOnce != null) runOnce.DeleteValue(RunOnceName, false);
            return "Benchmark pendente cancelado.";
        }

        public static string BuildComparison(BenchmarkSession session)
        {
            if (session == null || session.Before == null) return "Nenhum benchmark foi iniciado.";
            if (session.PendingRestart || session.After == null) return "Benchmark aguardando reinicialização.\r\n\r\n" + Describe(session.Before);
            BenchmarkSample before = session.Before;
            BenchmarkSample after = session.After;
            return "BENCHMARK CONCLUÍDO\r\n" + new string('=', 72) +
                string.Format(CultureInfo.CurrentCulture, "\r\nCPU em repouso: {0:N1}% → {1:N1}% ({2:+0.0;-0.0;0.0} p.p.)", before.AverageCpuPercent, after.AverageCpuPercent, after.AverageCpuPercent - before.AverageCpuPercent) +
                string.Format(CultureInfo.CurrentCulture, "\r\nMemória livre: {0:N1} GB → {1:N1} GB ({2:+0.0;-0.0;0.0} GB)", before.AverageFreeRamGb, after.AverageFreeRamGb, after.AverageFreeRamGb - before.AverageFreeRamGb) +
                string.Format(CultureInfo.CurrentCulture, "\r\nDisco livre: {0:N1} GB → {1:N1} GB ({2:+0.0;-0.0;0.0} GB)", before.FreeDiskGb, after.FreeDiskGb, after.FreeDiskGb - before.FreeDiskGb) +
                FormatBootComparison(before.BootDurationMilliseconds, after.BootDurationMilliseconds);
        }

        private static BenchmarkSample CaptureSample(CancellationToken token)
        {
            var sampler = new SystemActivitySampler();
            sampler.Prime();
            var cpu = new List<double>();
            var ram = new List<double>();
            for (int i = 0; i < 20; i++)
            {
                token.ThrowIfCancellationRequested();
                Thread.Sleep(500);
                double total;
                double free;
                double? usage = sampler.Sample(out total, out free);
                if (usage.HasValue) cpu.Add(usage.Value);
                if (free > 0) ram.Add(free);
            }
            SystemMetrics metrics = V2Engine.ReadMetrics();
            StartupMeasurement boot = AdvancedEngine.ReadBootMeasurements().FirstOrDefault(item => item.Name == "Inicialização do Windows");
            return new BenchmarkSample
            {
                CapturedUtc = DateTime.UtcNow.ToString("o"),
                BootUtc = ReadBootTimeUtc().ToString("o"),
                AverageCpuPercent = cpu.Count == 0 ? metrics.CpuUsagePercent : TrimmedAverage(cpu),
                AverageFreeRamGb = ram.Count == 0 ? metrics.FreeRamGb : TrimmedAverage(ram),
                FreeDiskGb = metrics.FreeDiskGb,
                BootDurationMilliseconds = boot == null ? 0 : boot.DurationMilliseconds
            };
        }

        private static double TrimmedAverage(List<double> values)
        {
            if (values.Count < 5) return values.Count == 0 ? 0 : values.Average();
            List<double> ordered = values.OrderBy(value => value).ToList();
            int trim = Math.Max(1, ordered.Count / 10);
            return ordered.Skip(trim).Take(ordered.Count - (trim * 2)).Average();
        }

        private static DateTime ReadBootTimeUtc()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))
                foreach (ManagementObject os in searcher.Get()) return ManagementDateTimeConverter.ToDateTime(Convert.ToString(os["LastBootUpTime"])).ToUniversalTime();
            }
            catch { }
            return DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(GetTickCount64()));
        }

        private static DateTime ParseUtc(string value)
        {
            DateTime parsed;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out parsed) ? parsed.ToUniversalTime() : DateTime.MinValue;
        }

        private static string Describe(BenchmarkSample sample)
        {
            return string.Format(CultureInfo.CurrentCulture, "CPU média: {0:N1}%\r\nMemória livre média: {1:N1} GB\r\nDisco livre: {2:N1} GB", sample.AverageCpuPercent, sample.AverageFreeRamGb, sample.FreeDiskGb);
        }

        private static string FormatBootComparison(long before, long after)
        {
            if (before <= 0 || after <= 0) return "\r\nTempo de boot: indisponível sem acesso aos eventos administrativos.";
            return string.Format(CultureInfo.CurrentCulture, "\r\nTempo de boot: {0:N1} s → {1:N1} s ({2:+0.0;-0.0;0.0} s)", before / 1000.0, after / 1000.0, (after - before) / 1000.0);
        }

        private static void Save(BenchmarkSession session)
        {
            Directory.CreateDirectory(AppFolder);
            File.WriteAllText(SessionPath, new JavaScriptSerializer().Serialize(session), Encoding.UTF8);
        }
    }

    internal static class PersistentMetricStore
    {
        private static readonly string HistoryFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Codex", "PerformanceOptimizer", "Metrics");

        public static void Append(SystemMetrics metrics, IEnumerable<ProcessActivity> processes, IEnumerable<TemperatureReading> temperatures)
        {
            try
            {
                Directory.CreateDirectory(HistoryFolder);
                var entry = new HistoricalMetricEntry
                {
                    CapturedUtc = DateTime.UtcNow.ToString("o"),
                    CpuPercent = metrics.CpuUsagePercent,
                    MemoryUsedPercent = metrics.TotalRamGb <= 0 ? 0 : 100.0 - (metrics.FreeRamGb * 100.0 / metrics.TotalRamGb),
                    DiskUsedPercent = 100.0 - metrics.FreeDiskPercent,
                    HottestTemperatureCelsius = temperatures == null || !temperatures.Any() ? (double?)null : temperatures.Max(item => item.Celsius),
                    Processes = (processes ?? Enumerable.Empty<ProcessActivity>()).Take(5).Select(item => new HistoricalProcessEntry { Name = item.Name, CpuPercent = item.CpuPercent, MemoryBytes = item.WorkingSetBytes }).ToList()
                };
                string path = Path.Combine(HistoryFolder, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".jsonl");
                File.AppendAllText(path, new JavaScriptSerializer().Serialize(entry) + Environment.NewLine, Encoding.UTF8);
                DeleteExpiredFiles();
            }
            catch { }
        }

        public static TrendSummary Read(int days)
        {
            DateTime cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, Math.Min(30, days)));
            var points = new List<HistoricalMetricEntry>();
            if (Directory.Exists(HistoryFolder))
            foreach (string path in Directory.GetFiles(HistoryFolder, "*.jsonl").OrderBy(item => item))
            {
                foreach (string line in File.ReadLines(path, Encoding.UTF8))
                {
                    try
                    {
                        HistoricalMetricEntry entry = new JavaScriptSerializer().Deserialize<HistoricalMetricEntry>(line);
                        DateTime captured;
                        if (entry != null && DateTime.TryParse(entry.CapturedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out captured) && captured.ToUniversalTime() >= cutoff) points.Add(entry);
                    }
                    catch { }
                }
            }
            var processRows = points.SelectMany(point => point.Processes ?? new List<HistoricalProcessEntry>()).GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase).Select(group => new TrendProcessSummary
            {
                Name = group.Key,
                AverageCpuPercent = group.Average(item => item.CpuPercent),
                PeakCpuPercent = group.Max(item => item.CpuPercent),
                PeakMemoryBytes = group.Max(item => item.MemoryBytes),
                Samples = group.Count()
            }).OrderByDescending(item => item.AverageCpuPercent * 10 + item.PeakMemoryBytes / 104857600.0).Take(12).ToList();
            return new TrendSummary
            {
                Points = points.OrderBy(item => item.CapturedUtc).ToList(),
                Processes = processRows,
                AverageCpuPercent = points.Count == 0 ? 0 : points.Average(item => item.CpuPercent),
                PeakCpuPercent = points.Count == 0 ? 0 : points.Max(item => item.CpuPercent),
                AverageMemoryUsedPercent = points.Count == 0 ? 0 : points.Average(item => item.MemoryUsedPercent),
                PeakMemoryUsedPercent = points.Count == 0 ? 0 : points.Max(item => item.MemoryUsedPercent),
                PeakTemperatureCelsius = points.Where(item => item.HottestTemperatureCelsius.HasValue).Select(item => item.HottestTemperatureCelsius).DefaultIfEmpty(null).Max()
            };
        }

        public static string FolderPath { get { Directory.CreateDirectory(HistoryFolder); return HistoryFolder; } }

        private static void DeleteExpiredFiles()
        {
            DateTime cutoff = DateTime.UtcNow.AddDays(-31);
            foreach (string path in Directory.GetFiles(HistoryFolder, "*.jsonl"))
                try { if (File.GetLastWriteTimeUtc(path) < cutoff) File.Delete(path); } catch { }
        }
    }

    internal sealed class HistoryChart : Control
    {
        private readonly List<float> _values = new List<float>();
        public Color LineColor { get; set; }

        public HistoryChart()
        {
            DoubleBuffered = true;
            LineColor = Theme.Primary;
            BackColor = Theme.SurfaceDark;
            AccessibleRole = AccessibleRole.Graphic;
            TabStop = false;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        public void SetValues(IEnumerable<double> values)
        {
            _values.Clear();
            List<double> source = (values ?? Enumerable.Empty<double>()).ToList();
            int step = Math.Max(1, (int)Math.Ceiling(source.Count / 180.0));
            for (int i = 0; i < source.Count; i += step) _values.Add((float)Math.Max(0, Math.Min(100, source.Skip(i).Take(step).Average())));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var grid = new Pen(Color.FromArgb(35, Theme.Muted)))
                for (int i = 1; i < 4; i++) e.Graphics.DrawLine(grid, 0, Height * i / 4, Width, Height * i / 4);
            if (_values.Count < 2) return;
            var points = new PointF[_values.Count];
            for (int i = 0; i < _values.Count; i++) points[i] = new PointF(i * (Width - 1f) / (_values.Count - 1), (Height - 2f) * (1f - _values[i] / 100f) + 1f);
            using (var pen = new Pen(LineColor, 2f)) e.Graphics.DrawLines(pen, points);
        }
    }

    internal sealed class TrendForm : Form
    {
        private readonly ComboBox _period;
        private readonly Label _summary;
        private readonly HistoryChart _cpu;
        private readonly HistoryChart _memory;
        private readonly HistoryChart _disk;
        private readonly DataGridView _processes;

        public TrendForm()
        {
            Text = "Tendências do sistema";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(860, 620);
            Size = new Size(980, 700);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;

            Controls.Add(new Label { Text = "Histórico persistente", Location = new Point(24, 18), AutoSize = true, Font = new Font("Segoe UI Semibold", 15f), ForeColor = Theme.Text });
            _period = new ComboBox { Location = new Point(748, 18), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text };
            _period.AccessibleName = "Período do histórico";
            _period.Items.AddRange(new object[] { "Hoje", "Últimos 7 dias", "Últimos 30 dias" });
            _period.SelectedIndex = 0;
            _period.SelectedIndexChanged += delegate { LoadTrend(); };
            _summary = new Label { Location = new Point(26, 53), Size = new Size(900, 36), AutoEllipsis = true, ForeColor = Theme.Muted };
            _cpu = ChartAt("CPU", 24, 100, Theme.Success);
            _memory = ChartAt("MEMÓRIA USADA", 332, 100, Theme.Warning);
            _disk = ChartAt("DISCO USADO", 640, 100, Theme.Primary);
            _processes = new DataGridView { Location = new Point(24, 320), Size = new Size(904, 290), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackgroundColor = Theme.SurfaceDark, ForeColor = Theme.Text, GridColor = Theme.Border, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false };
            _processes.ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface;
            _processes.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
            _processes.DefaultCellStyle.BackColor = Theme.SurfaceDark;
            _processes.DefaultCellStyle.ForeColor = Theme.Text;
            _processes.Columns.Add("Name", "Aplicativo");
            _processes.Columns.Add("Average", "CPU média");
            _processes.Columns.Add("Peak", "Pico CPU");
            _processes.Columns.Add("Memory", "Pico RAM");
            Controls.Add(_period);
            Controls.Add(_summary);
            Controls.Add(_processes);
            Shown += delegate { LoadTrend(); };
        }

        private HistoryChart ChartAt(string title, int x, int y, Color color)
        {
            Controls.Add(new Label { Text = title, Location = new Point(x, y), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI Semibold", 8.5f) });
            var chart = new HistoryChart { Location = new Point(x, y + 26), Size = new Size(288, 150), LineColor = color, Anchor = AnchorStyles.Top | AnchorStyles.Left, AccessibleName = "Gráfico histórico de " + title };
            Controls.Add(chart);
            return chart;
        }

        private void LoadTrend()
        {
            int days = _period.SelectedIndex == 0 ? 1 : _period.SelectedIndex == 1 ? 7 : 30;
            TrendSummary trend = PersistentMetricStore.Read(days);
            _cpu.SetValues(trend.Points.Select(item => item.CpuPercent));
            _memory.SetValues(trend.Points.Select(item => item.MemoryUsedPercent));
            _disk.SetValues(trend.Points.Select(item => item.DiskUsedPercent));
            _summary.Text = trend.Points.Count == 0 ? "O histórico começará a aparecer após a primeira coleta automática." : string.Format(CultureInfo.CurrentCulture, "{0} amostras  •  CPU média {1:N1}% / pico {2:N1}%  •  Memória média {3:N1}%{4}", trend.Points.Count, trend.AverageCpuPercent, trend.PeakCpuPercent, trend.AverageMemoryUsedPercent, trend.PeakTemperatureCelsius.HasValue ? "  •  Temperatura máxima " + trend.PeakTemperatureCelsius.Value.ToString("N0") + " °C" : string.Empty);
            _processes.Rows.Clear();
            foreach (TrendProcessSummary process in trend.Processes) _processes.Rows.Add(process.Name, process.AverageCpuPercent.ToString("N1") + "%", process.PeakCpuPercent.ToString("N1") + "%", V2Engine.FormatBytes(process.PeakMemoryBytes));
        }
    }
}
