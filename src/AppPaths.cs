using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal static class AppPaths
    {
        private static readonly bool Portable = DetectPortableMode();
        private static readonly string Root = Portable
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dados do Otimizador")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Codex", "PerformanceOptimizer");

        public static bool IsPortable { get { return Portable; } }
        public static string RootFolder { get { return Root; } }
        public static string ReportsFolder { get { return Path.Combine(Root, "Reports"); } }
        public static string LogsFolder { get { return Path.Combine(Root, "Logs"); } }
        public static string DriverBackupsFolder { get { return Path.Combine(Root, "DriverBackups"); } }
        public static string EnergyReportsFolder { get { return Path.Combine(Root, "EnergyReports"); } }
        public static string QuarantineFolder { get { return Path.Combine(Root, "Quarantine"); } }
        public static string SnapshotPath { get { return Path.Combine(Root, "state-v2.json"); } }
        public static string ComparisonPath { get { return Path.Combine(Root, "comparison-v2.json"); } }
        public static string SettingsPath { get { return Path.Combine(Root, "advanced-settings.json"); } }
        public static string BenchmarkPath { get { return Path.Combine(Root, "benchmark-session.json"); } }
        public static string ModeDescription { get { return Portable ? "Portátil • dados nesta pasta" : "Instalado • dados no perfil local"; } }

        private static bool DetectPortableMode()
        {
            try
            {
                string[] arguments = Environment.GetCommandLineArgs();
                string executable = Application.ExecutablePath;
                return IsPortableConfiguration(executable, arguments, File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.mode")));
            }
            catch { return false; }
        }

        internal static bool IsPortableConfiguration(string executable, string[] arguments, bool markerExists)
        {
            if ((arguments ?? new string[0]).Any(item => string.Equals(item, "--portable", StringComparison.OrdinalIgnoreCase))) return true;
            if (Path.GetFileNameWithoutExtension(executable ?? string.Empty).IndexOf("Portatil", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return markerExists;
        }
    }
}
