using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace FrankensteinTI
{
    internal sealed class Finding
    {
        public string Level {get;set;} public string Title {get;set;} public string Evidence {get;set;} public string Advice {get;set;} public string Route {get;set;}
        public override string ToString(){return Level+" | "+Title+"\r\n"+Evidence+"\r\nPróximo passo: "+Advice;}
    }
    internal static class Diagnostics
    {
        internal static List<Finding> Scan(CancellationToken token)
        {return Scan(token,(script,timeout,cancellation)=>Protection.PowerShell(script,timeout,cancellation));}
        internal static List<Finding> Scan(CancellationToken token,Func<string,int,CancellationToken,CodexPerformanceOptimizer.CommandExecution> execute)
        {
            var parts=Script.Split(new[]{"# SECTION "},StringSplitOptions.None);var all=new List<Finding>();
            for(int i=1;i<parts.Length;i++){
                token.ThrowIfCancellationRequested();int newline=parts[i].IndexOf('\n');string title=parts[i].Substring(0,newline).Trim();
                Session.Report("Diagnóstico: "+title,(i-1)*100/(parts.Length-1));
                var command=execute(parts[0]+parts[i].Substring(newline)+EncodedResult,20000,token);
                if(command.ExitCode!=0||command.TimedOut){all.Add(new Finding{Level="Indisponível",Title=title,Evidence=command.TimedOut?"A consulta excedeu 20 segundos.":command.Output,Advice="O provedor não respondeu. Tente novamente ou use a ferramenta específica; nenhuma alteração foi aplicada.",Route=title=="Rede"?"Navegadores":"Sistema"});continue;}
                try{var match=System.Text.RegularExpressions.Regex.Match(command.Output,@"(?m)^FRANKENSTEIN_RESULT:([A-Za-z0-9+/=]+)\r?$");if(!match.Success)throw new IOException("Marcador de resultado ausente.");var rows=new JavaScriptSerializer().Deserialize<List<Finding>>(Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[1].Value)));if(rows==null||rows.Count==0)throw new IOException("Nenhum dado retornado.");all.AddRange(rows);}
                catch(Exception ex){all.Add(new Finding{Level="Indisponível",Title=title,Evidence="Resposta não interpretável: "+ex.Message,Advice="Consulte a ferramenta específica e repita a análise.",Route="Sistema"});}
            }
            token.ThrowIfCancellationRequested();Session.Report("Diagnóstico finalizado",100);return all;
        }
        const string EncodedResult="\n'FRANKENSTEIN_RESULT:'+ [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject @($rows.ToArray()) -Depth 5 -Compress)))\n";
        internal const string Script=@"
$ErrorActionPreference='Stop'
$rows=New-Object 'System.Collections.Generic.List[object]'
function Add($level,$title,$evidence,$advice,$route){$rows.Add([pscustomobject]@{Level=$level;Title=$title;Evidence=[string]$evidence;Advice=$advice;Route=$route})}
# SECTION Hardware
try {
 $cpu=Get-CimInstance Win32_Processor -OperationTimeoutSec 10
 $pc=Get-CimInstance Win32_ComputerSystem -OperationTimeoutSec 10
 Add 'Informação' 'Hardware' (($cpu.Name -join ', ')+('; RAM: {0:N1} GB; {1} {2}' -f ($pc.TotalPhysicalMemory/1GB),$pc.Manufacturer,$pc.Model)) 'Confira inventário e drivers na área Sistema.' 'Sistema'
 if($pc.PartOfDomain){Add 'Atenção' 'Computador de domínio' 'Esta máquina está vinculada a um domínio.' 'Revise alterações de rede, políticas e software com a TI responsável.' 'Sistema'}
} catch {Add 'Indisponível' 'Hardware' $_.Exception.Message 'Tente novamente com acesso ao WMI.' 'Sistema'}
# SECTION Volumes
try {
 foreach($v in @(Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' -OperationTimeoutSec 10)) {
  if($v.Size -gt 0){$free=100*$v.FreeSpace/$v.Size;$level=if($free -lt 15){'Atenção'}else{'Informação'};Add $level ('Espaço '+$v.DeviceID) ('{0:N1}% livre ({1:N1} GB)' -f $free,($v.FreeSpace/1GB)) 'Revise arquivos e simule a limpeza antes de remover dados.' 'Manutenção'}
 }
} catch {Add 'Indisponível' 'Volumes' $_.Exception.Message 'Confira o Gerenciamento de Disco.' 'Sistema'}
# SECTION Saúde de HDs e SSDs
try {
 $disks=@(Get-PhysicalDisk)
 if(!$disks.Count){throw 'Nenhum disco físico retornado pelo provedor.'}
 foreach($disk in $disks){
  $level=if([string]$disk.HealthStatus -eq 'Healthy'){'Informação'}else{'Atenção'}
  $evidence='Estado informado pelo Windows: '+$disk.HealthStatus+'; tipo: '+$disk.MediaType+'; operacional: '+($disk.OperationalStatus -join ', ')
  try {$r=$disk | Get-StorageReliabilityCounter; if(!$r){throw 'Contadores não expostos'}; $evidence+='; temperatura: '+$(if($null -ne $r.Temperature -and $r.Temperature -gt 0){[string]$r.Temperature+' °C'}else{'indisponível'})+'; desgaste informado: '+$(if($null -ne $r.Wear){[string]$r.Wear+'%'}else{'indisponível'})}catch{$evidence+='; contadores de confiabilidade/SMART indisponíveis'}
  Add $level ('HD/SSD: '+$disk.FriendlyName+' [disco '+$disk.DeviceId+']') $evidence 'Mantenha backup. Estado saudável não garante ausência de falha; confirme alertas na ferramenta do fabricante.' 'Sistema'
 }
} catch {Add 'Indisponível' 'Saúde dos HDs/SSDs' $_.Exception.Message 'O controlador pode não expor SMART. Consulte a ferramenta do fabricante.' 'Sistema'}
# SECTION Sensores
$sensorFound=$false
foreach($ns in @('root/LibreHardwareMonitor','root/OpenHardwareMonitor')){
 try{foreach($s in @(Get-CimInstance -Namespace $ns -ClassName Sensor -Filter ""SensorType='Temperature'"" -OperationTimeoutSec 5)) {if($null -ne $s.Value -and $s.Value -gt 0 -and $s.Value -lt 150){$sensorFound=$true;Add 'Informação' ('Temperatura: '+$s.Name) ($s.Value.ToString()+' °C; fonte: '+$ns+'; '+$s.Identifier) 'Identifique CPU/GPU pelo nome do sensor e compare com o limite do fabricante.' 'Sistema'}}}catch{}
}
if(!$sensorFound){Add 'Indisponível' 'Temperatura do processador' 'Nenhum provedor compatível de sensores está ativo.' 'Abra LibreHardwareMonitor/OpenHardwareMonitor se disponível. Sensores ACPI genéricos não são tratados como temperatura da CPU.' 'Sistema'}
# SECTION Rede
try {
 $adapters=@(Get-NetIPConfiguration | Where-Object {$_.NetAdapter.Status -eq 'Up'})
 if(!$adapters.Count){Add 'Atenção' 'Conexão de rede' 'Nenhuma interface ativa encontrada.' 'Verifique cabo, Wi-Fi e adaptador antes de alterar configurações.' 'Navegadores'}
 foreach($a in $adapters){$gateway=($a.IPv4DefaultGateway.NextHop -join ', ');$dns=($a.DNSServer.ServerAddresses -join ', ');$level=if(!$gateway -or !$dns){'Atenção'}else{'Informação'};Add $level ('Rede: '+$a.InterfaceAlias) ('IP: '+($a.IPv4Address.IPAddress -join ', ')+'; gateway: '+$gateway+'; DNS: '+$dns) 'Ausência de gateway/DNS pode ser intencional em redes isoladas. O inventário não comprova acesso à internet.' 'Navegadores'}
 $proxy=Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings'
 Add 'Informação' 'Proxy do usuário' ('Ativo: '+$proxy.ProxyEnable+'; servidor: '+$proxy.ProxyServer+'; PAC: '+$proxy.AutoConfigURL) 'Proxy e PAC podem ser exigidos pela empresa. Não remova sem revisar.' 'Navegadores'
} catch {Add 'Indisponível' 'Rede' $_.Exception.Message 'Consulte o diagnóstico de navegadores e a equipe de rede.' 'Navegadores'}
# SECTION Eventos de tela azul
try {
 $events=@(Get-WinEvent -FilterHashtable @{LogName='System';ProviderName='Microsoft-Windows-WER-SystemErrorReporting';Id=1001;StartTime=(Get-Date).AddDays(-30)} -MaxEvents 20 -ErrorAction Stop)
 foreach($e in $events){Add 'Atenção' 'Registro de bugcheck / tela azul' ($e.TimeCreated.ToString('s')+'; '+$e.Message) 'Analise o minidump no WinDbg. Driver, memória e hardware são hipóteses; este evento sozinho não identifica a causa.' 'Sistema'}
} catch {if($_.FullyQualifiedErrorId -like 'NoMatchingEventsFound*'){Add 'Informação' 'Tela azul nos últimos 30 dias' 'Nenhum evento 1001 desse provedor foi encontrado.' 'Ausência de eventos não exclui falhas, limpeza do log ou registro desativado.' 'Sistema'}else{Add 'Indisponível' 'Eventos de tela azul' $_.Exception.Message 'Verifique acesso ao log Sistema.' 'Sistema'}}
# SECTION Minidumps
try {$d=Join-Path $env:SystemRoot 'Minidump';$files=@(Get-ChildItem -LiteralPath $d -Filter '*.dmp' -ErrorAction Stop);Add 'Informação' 'Minidumps' ([string]$files.Count+' arquivo(s) em '+$d) 'Arquivos de dump podem conter dados sensíveis. Analise localmente; não há envio automático.' 'Sistema'}catch{Add 'Informação' 'Minidumps' 'Pasta ausente ou não acessível.' 'Confira a configuração de geração de dumps se ocorrer tela azul.' 'Sistema'}
";
    }
}
