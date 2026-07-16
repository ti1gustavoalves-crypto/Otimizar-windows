using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace CodexPerformanceOptimizer
{
    internal sealed class CommandExecution
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public bool TimedOut { get; set; }
    }

    internal static class SystemCommand
    {
        public static CommandExecution Execute(string fileName, string arguments, int timeoutMilliseconds)
        {
            try
            {
                Encoding encoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
                var startInfo = new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = encoding,
                    StandardErrorEncoding = encoding
                };
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) return new CommandExecution { ExitCode = -1, Output = "processo não iniciado" };
                    Task<string> output = process.StandardOutput.ReadToEndAsync();
                    Task<string> error = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        try { process.Kill(); } catch { }
                        return new CommandExecution { ExitCode = -1, Output = "tempo limite excedido", TimedOut = true };
                    }
                    try { Task.WaitAll(new Task[] { output, error }, 5000); } catch { }
                    string stdout = output.IsCompleted ? output.Result : string.Empty;
                    string stderr = error.IsCompleted ? error.Result : string.Empty;
                    return new CommandExecution
                    {
                        ExitCode = process.ExitCode,
                        Output = stdout + (string.IsNullOrWhiteSpace(stderr) ? string.Empty : Environment.NewLine + stderr)
                    };
                }
            }
            catch (Exception ex) { return new CommandExecution { ExitCode = -1, Output = ex.Message }; }
        }

        public static string Run(string fileName, string arguments, int timeoutMilliseconds)
        {
            return Execute(fileName, arguments, timeoutMilliseconds).Output;
        }
    }
}
