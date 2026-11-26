using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace FrankensteinTI
{
    public sealed class Module
    {
        public string Id, Name, Group, Summary, Details, File, Mode, Badge;
        public bool Admin, Changes, Native;
    }
    public sealed class RunRecord
    {
        public string Id, ModuleId, Name, Started, Ended, Status, Detail, Folder;
        public int? ProcessId, ExitCode;
    }
    public sealed class ModuleResult
    {
        public string Status { get; set; }
        public string Detail { get; set; }
    }
    public static class Catalog
    {
        public static List<Module> All()
        {
            return new List<Module> {
                new Module { Id="diagnostico", Name="Raio-X do computador", Group="Diagnóstico", Badge="SOMENTE LEITURA", Native=true, Mode="Diagnose", File="modules\\Central.ps1", Summary="Sistema, espaço, reinicialização e navegadores em um relatório.", Details="Consulta o Windows e os caminhos conhecidos de navegadores. Não encerra aplicativos e não aplica reparos. Gera relatório local; não testa carregamento de páginas." },
                new Module { Id="apps-check", Name="Aplicativos · diagnosticar", Group="Reparos", Native=true, Mode="AppsCheck", File="modules\\Apps.ps1", Summary="Investiga falhas recentes de aplicativos.", Details="Consulta eventos de falha e o estado dos aplicativos. Gera um relatório sem aplicar reparos." },
                new Module { Id="apps-repair", Name="Aplicativos · reparar", Group="Reparos", Native=true, Changes=true, Admin=true, Mode="AppsRepair", File="modules\\Apps.ps1", Summary="Repara componentes do Windows e aplicativos.", Details="Executa reparos do Windows e registra novamente aplicativos. Salve seu trabalho. A operação pode demorar e exigir reinicialização. Consulte o relatório e teste os aplicativos ao concluir." },
                new Module { Id="nx-check", Name="Licença NX · verificar", Group="Softwares técnicos", Badge="SOMENTE LEITURA", Native=true, Mode="NxCheck", File="modules\\Central.ps1", Summary="Consulta as variáveis de licença do usuário e do computador.", Details="Mostra os valores atuais de SPLM_LICENSE_SERVER e UGS_LICENSE_SERVER. Esta verificação não testa a disponibilidade nem a validade de uma licença no servidor." },
                new Module { Id="nx-set", Name="Licença NX · configurar", Group="Softwares técnicos", Badge="COM BACKUP", Native=true, Changes=true, Mode="NxSet", File="modules\\Central.ps1", Summary="Configura o servidor FIEMG e permite restaurar o estado anterior.", Details="Altera somente as duas variáveis de licença no escopo escolhido. Faz backup tipado antes de gravar e verifica os valores depois. Não encerra o NX: feche-o antes e saia/entre no Windows após a alteração. O servidor pode ser editado na próxima tela." },
                new Module { Id="nx-restore", Name="Licença NX · restaurar", Group="Softwares técnicos", Badge="RECUPERAÇÃO", Native=true, Changes=true, Mode="NxRestore", File="modules\\Central.ps1", Summary="Restaura as variáveis a partir de um backup desta central.", Details="Selecione um arquivo nx-backup.json do histórico. A central valida computador, conta, escopo, valores e tipos antes da restauração. Faz um novo backup do estado atual para permitir reverter a restauração." },
                new Module { Id="labx", Name="LabX · instalação assistida", Group="Softwares técnicos", Badge="PROCEDIMENTO ESPECÍFICO", Native=true, Changes=true, Admin=true, Mode="LabX", File="modules\\Central.ps1", Summary="Seleciona o instalador e trata a pendência conhecida de Gaming Services.", Details="Valida o instalador antes de alterar serviços. Só remove pares de exclusão pendente de gamingservicesproxy_*.dll.0, preservando outras operações. Se houver outra pendência, bloqueia a instalação. Salva backup e restaura o estado dos serviços em finally. Não inclui o instalador do LabX." },
                new Module { Id="enrollment", Name="Vínculo corporativo · diagnóstico", Group="Diagnóstico", Badge="SOMENTE LEITURA", Native=true, Mode="Enrollment", File="modules\\Central.ps1", Summary="Consulta associação, tarefas EnterpriseMgmt e inscrições.", Details="Reúne o diagnóstico dos seus scripts de vínculo corporativo. A exclusão indiscriminada de Enrollments não foi automatizada: o arquivo original fica em Referencias para análise técnica." }
            };
        }
    }
    public sealed class Engine
    {
        public readonly string Root, Data;
        public Engine(string root, string data) { Root=Path.GetFullPath(root); Data=Path.GetFullPath(data); Directory.CreateDirectory(Data); }
        public static string SafePath(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new IOException("Caminho de módulo inválido.");
            string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string full=Path.GetFullPath(Path.Combine(prefix,relative));
            if (!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) throw new IOException("Caminho fora do pacote.");
            string part=full;
            while (part.Length>=prefix.Length) { if ((File.Exists(part)||Directory.Exists(part)) && (File.GetAttributes(part)&FileAttributes.ReparsePoint)!=0) throw new IOException("Links de redirecionamento não são aceitos nos módulos."); part=Path.GetDirectoryName(part); }
            return full;
        }
        public static string Hash(string file) { using(var sha=SHA256.Create()) using(var s=File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(s)).Replace("-",""); }
        public void Verify(string relative)
        {
            var hashes=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Root,"integridade.json"),Encoding.UTF8));
            string expected;
            if (!hashes.TryGetValue(relative.Replace('/','\\'),out expected)) throw new IOException("Módulo ausente do manifesto de integridade.");
            string path=SafePath(Root,relative);
            if (!File.Exists(path)) throw new FileNotFoundException("Arquivo do módulo ausente. Extraia o pacote completo.",path);
            if (!string.Equals(Hash(path),expected,StringComparison.OrdinalIgnoreCase)) throw new IOException("Arquivo alterado ou corrompido. Extraia novamente o pacote original.");
        }
        public string VerifyAll()
        {
            var hashes=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Root,"integridade.json"),Encoding.UTF8));
            foreach(string file in hashes.Keys) Verify(file);
            return hashes.Count+" arquivos conferidos com SHA-256. A conferência detecta alterações em relação ao manifesto local; não substitui assinatura digital.";
        }
        public void Save(RunRecord run)
        {
            Directory.CreateDirectory(run.Folder);
            string path=Path.Combine(run.Folder,"execucao.json"), temp=path+".tmp";
            File.WriteAllText(temp,new JavaScriptSerializer().Serialize(run),Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
        }
        public List<RunRecord> History()
        {
            var rows=new List<RunRecord>();
            foreach(string dir in Directory.GetDirectories(Data)) try { string f=Path.Combine(dir,"execucao.json"); if(File.Exists(f)) rows.Add(new JavaScriptSerializer().Deserialize<RunRecord>(File.ReadAllText(f,Encoding.UTF8))); } catch { }
            return rows.OrderByDescending(r=>r.Started).ToList();
        }
        public static string Quote(string value) { if(value.IndexOf('"')>=0 || value.IndexOf('\r')>=0 || value.IndexOf('\n')>=0) throw new ArgumentException("Argumento inválido."); return "\""+value+"\""; }
        public static string Classify(bool native, int code, ModuleResult result)
        {
            if (code==3010) return "Reinicialização necessária";
            if (code!=0) return "Falha";
            if (!native) return "Encerrado · conferir resultado";
            if (result==null || string.IsNullOrWhiteSpace(result.Status)) return "Resultado não confirmado";
            return result.Status=="OK" ? "Concluído e verificado" : result.Status=="REVIEW" ? "Encerrado · conferir resultado" : "Falha";
        }
        public async Task<RunRecord> Execute(Module module, Dictionary<string,object> request, Action<string> update)
        {
            if (!module.Native) throw new InvalidOperationException("Ferramentas externas não fazem parte desta versão integrada.");
            Verify(module.File);
            if(module.Id=="otimizador") { Verify("modules\\optimizer\\release-channel.json"); Verify("modules\\optimizer\\portable.mode"); }
            string id=DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
            var run=new RunRecord { Id=id,ModuleId=module.Id,Name=module.Name,Started=DateTime.Now.ToString("o"),Status="Preparando",Folder=Path.Combine(Data,id),Detail="" };
            Save(run);
            try
            {
                string file=SafePath(Root,module.File);
                var info=new ProcessStartInfo(file) { UseShellExecute=true, WorkingDirectory=Path.GetDirectoryName(file) };
                if(module.Native)
                {
                    request["Mode"]=module.Mode; request["Output"]=run.Folder;
                    string input=Path.Combine(run.Folder,"pedido.json");
                    File.WriteAllText(input,new JavaScriptSerializer().Serialize(request),Encoding.UTF8);
                    info.FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell\\v1.0\\powershell.exe");
                    info.Arguments="-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(file)+" -Request "+Quote(input);
                    if (module.Mode=="AppsCheck" || module.Mode=="AppsRepair") info.Arguments="-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(file)+" -Modo "+(module.Mode=="AppsRepair"?"Reparar":"Diagnosticar")+" -PastaSaida "+Quote(run.Folder)+" -SemPausa";
                    info.WindowStyle=ProcessWindowStyle.Hidden;
                }
                else if(Path.GetExtension(file).Equals(".ps1",StringComparison.OrdinalIgnoreCase))
                {
                    info.FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell\\v1.0\\powershell.exe");
                    info.Arguments="-NoLogo -NoProfile -ExecutionPolicy Bypass -File "+Quote(file);
                }
                if(module.Admin) info.Verb="runas";
                run.Status="Aguardando abertura"; Save(run);
                using(Process process=Process.Start(info))
                {
                    if(process==null) throw new InvalidOperationException("O Windows não devolveu o processo. Confira a ferramenta antes de tentar novamente.");
                    run.ProcessId=process.Id; run.Status="Em execução"; Save(run); update(module.Name+(module.Native?" em execução. Aguarde o resultado nesta central.":" em execução. Aguarde a conclusão na ferramenta."));
                    await Task.Run(delegate { if(module.Native&&!module.Changes) { if(!process.WaitForExit(180000)) { try { process.Kill(); } catch {} throw new TimeoutException("O diagnóstico excedeu 3 minutos. Nenhum reparo foi aplicado. Confira a pasta da execução."); } } else process.WaitForExit(); });
                    run.ExitCode=process.ExitCode;
                    ModuleResult result=null;
                    string output=Path.Combine(run.Folder,"resultado.json");
                    if(module.Native && File.Exists(output)) result=new JavaScriptSerializer().Deserialize<ModuleResult>(File.ReadAllText(output,Encoding.UTF8));
                    run.Status=Classify(module.Native,process.ExitCode,result);
                    run.Detail=result!=null ? result.Detail : (module.Native ? "O módulo não produziu confirmação estruturada. Consulte a pasta desta execução." : "Processo inicial encerrado com código "+process.ExitCode+". Confira o resultado na ferramenta e se há processos derivados ainda abertos. Não há validação automática dos reparos legados.");
                }
            }
            catch(System.ComponentModel.Win32Exception e) { run.Status=e.NativeErrorCode==1223 ? "Abertura cancelada" : "Falha ao abrir"; run.Detail=e.Message; }
            catch(Exception e) { run.Status="Falha"; run.Detail=e.Message; }
            finally { run.Ended=DateTime.Now.ToString("o"); Save(run); }
            update(run.Status); return run;
        }
    }
}
