using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
            for (int i = 0; i < 10; i++)
            {
                token.ThrowIfCancellationRequested();
                Thread.Sleep(500);
            }
            progress.Report("Medindo o sistema após a reinicialização...");
            session.After = CaptureSample(token);
            session.PendingRestart = false;
            Save(session);
            using (RegistryKey runOnce = Registry.CurrentUser.OpenSubKey(RunOnceKey, true))
                if (runOnce != null) runOnce.DeleteValue(RunOnceName, false);
            return BuildComparison(session);
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
                    foreach (ManagementObject os in searcher.Get())
                        return ManagementDateTimeConverter.ToDateTime(Convert.ToString(os["LastBootUpTime"])).ToUniversalTime();
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
}
