using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal static class V2SelfTest
    {
        [STAThread]
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
                string cachedUpdate = Path.Combine(Path.GetTempPath(), "OtimizadorUpdateCacheTest-" + Guid.NewGuid().ToString("N") + ".bin");
                try
                {
                    File.WriteAllText(cachedUpdate, "update-cache-test", Encoding.UTF8);
                    string cachedHash;
                    using (SHA256 sha = SHA256.Create())
                    using (FileStream stream = File.OpenRead(cachedUpdate)) cachedHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                    if (!AdvancedEngine.IsVerifiedUpdateFileForTesting(cachedUpdate, cachedHash) || AdvancedEngine.IsVerifiedUpdateFileForTesting(cachedUpdate, new string('0', 64))) throw new InvalidOperationException("Cache verificado do atualizador falhou.");
                }
                finally { try { if (File.Exists(cachedUpdate)) File.Delete(cachedUpdate); } catch { } }
                string benchmark = BenchmarkManager.BuildComparison(new BenchmarkSession
                {
                    PendingRestart = false,
                    Before = new BenchmarkSample { AverageCpuPercent = 20, AverageFreeRamGb = 4, FreeDiskGb = 30, BootDurationMilliseconds = 60000 },
                    After = new BenchmarkSample { AverageCpuPercent = 10, AverageFreeRamGb = 5, FreeDiskGb = 35, BootDurationMilliseconds = 45000 }
                });
                if (benchmark.IndexOf("BENCHMARK CONCLUÍDO", StringComparison.OrdinalIgnoreCase) < 0) throw new InvalidOperationException("Comparativo pós-reinicialização falhou.");
                string safety = SafetyTestSuite.Run(CancellationToken.None, new Progress<string>());
                if (safety.IndexOf("11 de 11 testes aprovados", StringComparison.OrdinalIgnoreCase) < 0) throw new InvalidOperationException("Suíte de segurança falhou.\r\n" + safety);
                if (!DriverManager.IsValidUpdateIdForTesting("11111111-2222-3333-4444-555555555555") || DriverManager.IsValidUpdateIdForTesting("driver-inválido")) throw new InvalidOperationException("Validação segura de drivers falhou.");
                string intelSupport = DriverManager.ResolveOfficialSupportForTesting("Intel Corporation", "Display Driver");
                string microsoftSupport = DriverManager.ResolveOfficialSupportForTesting("Microsoft Corporation", "AudioProcessingObject Driver Update");
                string amdSupport = DriverManager.ResolveOfficialSupportForTesting("Advanced Micro Devices, Inc.", "Radeon Display Driver");
                string catalog = DriverManager.BuildCatalogUrlForTesting(@"PCI\VEN_8086&DEV_1234", "Intel Driver");
                if (!DriverManager.IsOfficialSupportUrlForTesting(intelSupport) || microsoftSupport.IndexOf("catalog.update.microsoft.com", StringComparison.OrdinalIgnoreCase) < 0 || amdSupport.IndexOf("amd.com", StringComparison.OrdinalIgnoreCase) < 0 || catalog.IndexOf("PCI", StringComparison.OrdinalIgnoreCase) < 0 || DriverManager.IsOfficialSupportUrlForTesting("https://intel.com.exemplo.invalid/driver")) throw new InvalidOperationException("Validação dos links oficiais falhou.");
                string firmwareBlock = DriverManager.ValidateFirmwareSelection(new[] { new DriverUpdate { IsFirmware = true, Provider = "Dell", Title = "Dell Firmware" } }, new DriverSafetyStatus { FirmwareSafe = false, Summary = "teste" });
                if (string.IsNullOrWhiteSpace(firmwareBlock)) throw new InvalidOperationException("Proteção de BIOS e firmware falhou.");
                string wingetSample = "Name                 Id                    Version        Available\r\n----------------------------------------------------------------------------\r\nGoogle Chrome        Google.Chrome.EXE     150.0.7871.127 150.0.7871.129\r\nPowerShell 7         Microsoft.PowerShell  7.4.0          7.5.0\r\n2 upgrades available.";
                var programUpdates = ProgramUpdater.ParseUpgradeOutputForTesting(wingetSample);
                if (programUpdates.Count != 2 || programUpdates[0].PackageId != "Google.Chrome.EXE" || !ProgramUpdater.IsValidPackageIdForTesting("Microsoft.PowerShell") || ProgramUpdater.IsValidPackageIdForTesting("pacote & comando")) throw new InvalidOperationException("Parser seguro do WinGet falhou.");
                string windowsTitle = Convert.ToBase64String(Encoding.UTF8.GetBytes("Atualização cumulativa de teste"));
                var windowsUpdates = WindowsUpdateInventory.ParseForTesting(windowsTitle + "|00000000-0000-0000-0000-000000000001|1024|True|False");
                if (windowsUpdates.Count != 1 || !windowsUpdates[0].Mandatory || windowsUpdates[0].Title.IndexOf("cumulativa", StringComparison.OrdinalIgnoreCase) < 0) throw new InvalidOperationException("Parser do Windows Update falhou.");
                var driverInventory = DriverManager.ReadInstalledDrivers();
                if (driverInventory == null || driverInventory.Any(item => string.IsNullOrWhiteSpace(item.Category) || string.IsNullOrWhiteSpace(item.Device) || string.IsNullOrWhiteSpace(item.Version))) throw new InvalidOperationException("Inventário de drivers retornou dados inválidos.");
                var startupEntries = V2Engine.ReadStartupEntries();
                if (startupEntries == null || startupEntries.Any(item => string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Source))) throw new InvalidOperationException("Inventário de inicialização falhou.");
                var guidedPlan = MaintenanceWorkflow.BuildPlan(ServiceProfile.SlowComputer,
                    new SystemMetrics { TotalRamGb = 16, FreeRamGb = 1, TotalDiskGb = 500, FreeDiskGb = 30, FreeDiskPercent = 6, CpuUsagePercent = 40 },
                    new DiagnosticSnapshot { Stability = new StabilityDiagnostic { PendingRestart = true } },
                    new[] { new StartupEntry { Name = "Teste", Enabled = true, CanChange = true, Impact = "alto", Source = "Teste" } }, 2, 3);
                if (guidedPlan.SelectedCount < 5 || !guidedPlan.RequiresAdministrator || !guidedPlan.Issues.Any(item => item.Id == "storage" && item.Severity == "Crítico") || !guidedPlan.Issues.Any(item => item.Id == "restart" && !item.CanFix)) throw new InvalidOperationException("Plano guiado não priorizou as pendências corretamente.");
                MaintenancePlan completePlan = MaintenanceWorkflow.BuildPlan(ServiceProfile.Complete, new SystemMetrics(), null, new StartupEntry[0], 0, 0);
                if (completePlan.SelectedCount != 7 || !completePlan.RequiresAdministrator) throw new InvalidOperationException("Perfil de atendimento completo não selecionou todas as ações seguras.");
                int[] testedDpis = { 96, 120, 144, 168, 192 };
                if (testedDpis.Any(dpi => !string.IsNullOrEmpty(ResponsiveLayoutPolicy.Validate(1024, 680, dpi))) || string.IsNullOrEmpty(ResponsiveLayoutPolicy.Validate(900, 640, 96))) throw new InvalidOperationException("Política de responsividade falhou.");
                if (!AppPaths.IsPortableConfiguration("OtimizadorDeDesempenho-Portatil.exe", new string[0], false) || !AppPaths.IsPortableConfiguration("Otimizador.exe", new[] { "--portable" }, false) || AppPaths.IsPortableConfiguration("Otimizador.exe", new string[0], false)) throw new InvalidOperationException("Detecção do modo portátil falhou.");
                HealthAssessment healthy = SystemHealthEngine.Assess(new SystemMetrics { TotalRamGb = 16, FreeRamGb = 8, TotalDiskGb = 500, FreeDiskGb = 200, FreeDiskPercent = 40, CpuUsagePercent = 20 }, null, new ProcessActivity[0], 0, 0);
                HealthAssessment limited = SystemHealthEngine.Assess(new SystemMetrics { TotalRamGb = 16, FreeRamGb = 1, TotalDiskGb = 500, FreeDiskGb = 20, FreeDiskPercent = 4, CpuUsagePercent = 92 }, null, new[] { new ProcessActivity { Name = "Browser", CpuPercent = 80, WorkingSetBytes = 2147483648 } }, 4, 3);
                BottleneckCause cause = BottleneckAnalyzer.Analyze(new SystemMetrics { TotalRamGb = 16, FreeRamGb = 1, CpuUsagePercent = 92, FreeDiskPercent = 4 }, null, new[] { new ProcessActivity { Name = "Browser", CpuPercent = 80, WorkingSetBytes = 2147483648 } });
                if (healthy.Score <= limited.Score || limited.Level != "Crítica" || cause.Title.IndexOf("Browser", StringComparison.OrdinalIgnoreCase) < 0) throw new InvalidOperationException("Saúde e causa provável não priorizaram os gargalos corretamente.");
                AnalysisCache.Set("self-test", "A", "valor");
                string cachedValue;
                if (!AnalysisCache.TryGet("self-test", "A", TimeSpan.FromMinutes(1), out cachedValue) || cachedValue != "valor" || AnalysisCache.TryGet("self-test", "B", TimeSpan.FromMinutes(1), out cachedValue)) throw new InvalidOperationException("Cache inteligente não respeitou validade e impressão digital.");
                AnalysisCache.Invalidate("self-test");
                using (var form = new MainFormV2(null, false, true))
                {
                    string compactLayout = form.ValidateInterfaceForTesting(new Size(1024, 680));
                    string minimumLayout = form.ValidateInterfaceForTesting(new Size(1260, 760));
                    string wideLayout = form.ValidateInterfaceForTesting(new Size(1600, 900));
                    if (!string.IsNullOrEmpty(compactLayout) || !string.IsNullOrEmpty(minimumLayout) || !string.IsNullOrEmpty(wideLayout)) throw new InvalidOperationException("Validação automatizada da interface falhou. " + compactLayout + " " + minimumLayout + " " + wideLayout);
                    form.Close();
                    Application.DoEvents();
                }
                Console.WriteLine("CPU em tempo real: " + sampledCpu.Value.ToString("N0") + "%");
                Console.WriteLine("Memória em tempo real: " + sampledFreeRam.ToString("N1") + " GB livres");
                Console.WriteLine("Processos em destaque: " + processes.Count);
                Console.WriteLine("Alertas sustentados: OK");
                Console.WriteLine("Discos diagnosticados: " + diagnostics.Disks.Count);
                Console.WriteLine("Sensores de temperatura: " + diagnostics.Temperatures.Count);
                Console.WriteLine("Medições de inicialização: " + diagnostics.Startup.Count);
                Console.WriteLine("Estabilidade e atualizações: OK");
                Console.WriteLine("Cache SHA-256 do atualizador: OK");
                Console.WriteLine("Benchmark pós-reinicialização: OK");
                Console.WriteLine("Testes de segurança isolados: 11/11");
                Console.WriteLine("Volumes: " + V2Engine.ReadVolumes().Count);
                Console.WriteLine("Inicialização: " + startupEntries.Count);
                Console.WriteLine("Hardware: " + V2Engine.ReadImportantHardware(CancellationToken.None, new Progress<string>()).Count);
                Console.WriteLine("Drivers relevantes: " + driverInventory.Count + (driverInventory.Count == 0 ? " (inventário bloqueado por política)" : string.Empty));
                Console.WriteLine("Parser do WinGet: OK");
                Console.WriteLine("Manutenção guiada: " + guidedPlan.SelectedCount + " ações");
                Console.WriteLine("Layouts: 100%, 125%, 150%, 175% e 200% OK");
                Console.WriteLine("Modo portátil: OK");
                Console.WriteLine("Saúde e causa provável: " + healthy.Score + " → " + limited.Score + " OK");
                Console.WriteLine("Cache inteligente: OK");
                Console.WriteLine("Interface real em 1024x680, 1260x760 e 1600x900: OK");
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
