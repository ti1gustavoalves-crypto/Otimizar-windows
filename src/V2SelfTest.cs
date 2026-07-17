using System;
using System.Threading;

namespace CodexPerformanceOptimizer
{
    internal static class V2SelfTest
    {
        public static int Main()
        {
            try
            {
                Console.WriteLine("SELF-TEST 3.3");
                SystemMetrics metrics = V2Engine.ReadMetrics();
                if (metrics.TotalRamGb <= 0) throw new InvalidOperationException("Memória total não detectada.");
                if (metrics.TotalDiskGb <= 0) throw new InvalidOperationException("Disco C: não detectado.");

                var sampler = new SystemActivitySampler();
                sampler.Prime();
                Thread.Sleep(300);
                double sampledTotalRam;
                double sampledFreeRam;
                double? sampledCpu = sampler.Sample(out sampledTotalRam, out sampledFreeRam);
                if (!sampledCpu.HasValue || sampledCpu.Value < 0 || sampledCpu.Value > 100) throw new InvalidOperationException("Amostragem contínua da CPU falhou.");
                if (sampledTotalRam <= 0 || sampledFreeRam <= 0) throw new InvalidOperationException("Amostragem contínua da memória falhou.");

                var processSampler = new ProcessActivitySampler();
                processSampler.Prime();
                Thread.Sleep(350);
                var processes = processSampler.Sample(3);
                if (processes.Count > 3) throw new InvalidOperationException("O limite da lista de processos não foi respeitado.");
                foreach (ProcessActivity process in processes)
                    if (process.CpuPercent < 0 || process.CpuPercent > 100 || process.WorkingSetBytes < 0) throw new InvalidOperationException("Métrica inválida de processo.");
                var processHistory = new ProcessHistoryTracker();
                processHistory.Record(processes);
                if (processes.Count > 0 && processHistory.Summaries(3).Count == 0) throw new InvalidOperationException("Histórico de processos falhou.");

                var alertMonitor = new SustainedAlertMonitor(TimeSpan.Zero);
                SustainedAlert alert = alertMonitor.Evaluate(new SystemMetrics { CpuUsagePercent = 95, TotalRamGb = 16, FreeRamGb = 8, TotalDiskGb = 500, FreeDiskPercent = 30 });
                if (alert == null || alert.Title.IndexOf("Processador", StringComparison.OrdinalIgnoreCase) < 0) throw new InvalidOperationException("Regra de alerta sustentado falhou.");

                DiagnosticSnapshot diagnostics = AdvancedEngine.ReadDiagnostics();
                if (diagnostics == null || diagnostics.Disks == null || diagnostics.Temperatures == null || diagnostics.Startup == null || diagnostics.Recommendations == null) throw new InvalidOperationException("Diagnóstico avançado incompleto.");
                if (diagnostics.Stability == null || diagnostics.Stability.Uptime <= TimeSpan.Zero) throw new InvalidOperationException("Diagnóstico de estabilidade falhou.");
                UpdateCheckResult update = AdvancedEngine.CheckForUpdates();
                if (update == null || string.IsNullOrWhiteSpace(update.Message)) throw new InvalidOperationException("Verificação de atualização falhou.");
                string benchmark = BenchmarkManager.BuildComparison(new BenchmarkSession
                {
                    PendingRestart = false,
                    Before = new BenchmarkSample { AverageCpuPercent = 20, AverageFreeRamGb = 4, FreeDiskGb = 30, BootDurationMilliseconds = 60000 },
                    After = new BenchmarkSample { AverageCpuPercent = 10, AverageFreeRamGb = 5, FreeDiskGb = 35, BootDurationMilliseconds = 45000 }
                });
                if (benchmark.IndexOf("BENCHMARK CONCLUÍDO", StringComparison.OrdinalIgnoreCase) < 0) throw new InvalidOperationException("Comparativo pós-reinicialização falhou.");
                string safety = SafetyTestSuite.Run(CancellationToken.None, new Progress<string>());
                if (safety.IndexOf("10 de 10 testes aprovados", StringComparison.OrdinalIgnoreCase) < 0) throw new InvalidOperationException("Suíte de segurança falhou.\r\n" + safety);
                TrendSummary trend = PersistentMetricStore.Read(1);
                if (trend == null || trend.Points == null || trend.Processes == null) throw new InvalidOperationException("Histórico persistente falhou.");

                Console.WriteLine("CPU em tempo real: " + sampledCpu.Value.ToString("N0") + "%");
                Console.WriteLine("Memória em tempo real: " + sampledFreeRam.ToString("N1") + " GB livres");
                Console.WriteLine("Processos em destaque: " + processes.Count);
                Console.WriteLine("Alertas sustentados: OK");
                Console.WriteLine("Discos diagnosticados: " + diagnostics.Disks.Count);
                Console.WriteLine("Sensores de temperatura: " + diagnostics.Temperatures.Count);
                Console.WriteLine("Medições de inicialização: " + diagnostics.Startup.Count);
                Console.WriteLine("Estabilidade e atualizações: OK");
                Console.WriteLine("Benchmark e histórico persistente: OK");
                Console.WriteLine("Testes de segurança isolados: 10/10");
                Console.WriteLine("Volumes: " + V2Engine.ReadVolumes().Count);
                Console.WriteLine("Inicialização: " + V2Engine.ReadStartupEntries().Count);
                Console.WriteLine("Hardware: " + V2Engine.ReadImportantHardware(CancellationToken.None, new Progress<string>()).Count);
                Console.WriteLine("Histórico: " + V2Engine.ReadReportHistory(5).Count);
                Console.WriteLine("SELF-TEST 3.3 OK");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("SELF-TEST FALHOU: " + ex);
                return 1;
            }
        }
    }
}
