using System;
using System.IO;
using System.Text;
using System.Threading;
using CodexPerformanceOptimizer;
namespace FrankensteinTI
{
    internal static class Protection
    {
        internal static bool RequiresCheckpoint(string operation)
        {
            foreach(string prefix in new[]{"Lendo ","Verificando integridade", "Iniciando verificação", "Consultando ", "Gerando diagnóstico", "Procurando ", "Criando backup", "Analisando ", "Calculando ", "Mapeando ", "Atualizando duplicados"})
                if(operation.StartsWith(prefix,StringComparison.Ordinal))return false;
            return true;
        }
        internal static CommandExecution PowerShell(string script,int timeout=120000,CancellationToken token=default(CancellationToken))
        {
            return SystemCommand.Execute(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"),"-NoProfile -NonInteractive -OutputFormat Text -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes("$ProgressPreference='SilentlyContinue';\n"+script)),timeout,token);
        }
        internal static string EnsureCheckpoint()
        {return EnsureCheckpoint(script=>PowerShell(script,180000));}
        internal static string EnsureCheckpoint(Func<string,CommandExecution> execute)
        {
            Session.Report("Criando e verificando ponto de restauração antes da alteração...");
            string label="FrankensteinTI-"+Guid.NewGuid().ToString("N");
            var result=execute("$ErrorActionPreference='Stop'; try { Checkpoint-Computer -Description '"+label+"' -RestorePointType MODIFY_SETTINGS; $p=Get-ComputerRestorePoint | Where-Object Description -eq '"+label+"'; if(!$p){throw 'Novo ponto de restauracao nao confirmado. Verifique a Protecao do Sistema e o limite de frequencia do Windows.'}; 'RESTORE_POINT_OK:'+($p | Select-Object -Last 1 -ExpandProperty SequenceNumber) } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }");
            if(result.ExitCode!=0||result.TimedOut||!result.Output.Contains("RESTORE_POINT_OK:"))throw new InvalidOperationException("Alteração bloqueada: não foi possível confirmar um novo ponto de restauração. Nenhum reparo foi iniciado.\r\n"+result.Output);
            string folder=Path.Combine(Distribution.DataRoot,"Protecao");Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,label+".txt"),DateTime.Now.ToString("o")+"\r\n"+result.Output+"\r\nRecuperação pelo Windows: rstrui.exe. Não substitui backup de arquivos pessoais.",Encoding.UTF8);
            Session.Report("Ponto de restauração confirmado. Iniciando a operação.");return result.Output.Trim();
        }
    }
}
