using System;
using System.Linq;
using System.Threading;

namespace CodexPerformanceOptimizer
{
    internal static class V2SelfTest
    {
        public static int Main()
        {
            try
            {
                string version = typeof(V2SelfTest).Assembly.GetName().Version.ToString(3);
                Console.WriteLine("SELF-TEST " + version);
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
                if (!DriverManager.IsValidUpdateIdForTesting("11111111-2222-3333-4444-555555555555") || DriverManager.IsValidUpdateIdForTesting("driver-inválido")) throw new InvalidOperationException("Validação segura de drivers falhou.");
                string intelSupport = DriverManager.ResolveOfficialSupportForTesting("Intel Corporation", "Display Driver");
                string microsoftSupport = DriverManager.ResolveOfficialSupportForTesting("Microsoft Corporation", "AudioProcessingObject Driver Update");
                string amdSupport = DriverManager.ResolveOfficialSupportForTesting("Advanced Micro Devices, Inc.", "Radeon Display Driver");
                string catalog = DriverManager.BuildCatalogUrlForTesting(@"PCI\VEN_8086&DEV_1234", "Intel Driver");
                if (!DriverManager.IsOfficialSupportUrlForTesting(intelSupport) || microsoftSupport.IndexOf("catalog.update.microsoft.com", StringComparison.OrdinalIgnoreCase) < 0 || amdSupport.IndexOf("amd.com", StringComparison.OrdinalIgnoreCase) < 0 || catalog.IndexOf("PCI", StringComparison.OrdinalIgnoreCase) < 0 || DriverManager.IsOfficialSupportUrlForTesting("https://intel.com.exemplo.invalid/driver")) throw new InvalidOperationException("Validação dos links oficiais falhou.");
                string firmwareBlock = DriverManager.ValidateFirmwareSelection(new[] { new DriverUpdate { IsFirmware = true, Provider = "Dell", Title = "Dell Firmware" } }, new DriverSafetyStatus { FirmwareSafe = false, Summary = "teste" });
                if (string.IsNullOrWhiteSpace(firmwareBlock)) throw new InvalidOperationException("Proteção de BIOS e firmware falhou.");
                if (DriverManager.CountInstalledDrivers() <= 0) throw new InvalidOperationException("Inventário de drivers falhou.");
                var driverInventory = DriverManager.ReadInstalledDrivers();
                if (driverInventory == null || driverInventory.Count == 0 || driverInventory.Any(item => string.IsNullOrWhiteSpace(item.Category) || string.IsNullOrWhiteSpace(item.Device) || string.IsNullOrWhiteSpace(item.Version))) throw new InvalidOperationException("Versões dos drivers importantes não foram lidas.");
                var startupEntries = V2Engine.ReadStartupEntries();
                if (startupEntries == null || startupEntries.Any(item => string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Source))) throw new InvalidOperationException("Inventário de inicialização falhou.");
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
                Console.WriteLine("Inicialização: " + startupEntries.Count);
                Console.WriteLine("Hardware: " + V2Engine.ReadImportantHardware(CancellationToken.None, new Progress<string>()).Count);
                Console.WriteLine("Drivers importantes: " + driverInventory.Count);
                Console.WriteLine("SELF-TEST " + version + " OK");
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
