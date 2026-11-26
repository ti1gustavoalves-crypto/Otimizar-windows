using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using FrankensteinTI;
internal static class SelfTest
{
    static int passed;
    static void Assert(bool condition,string name) { if(!condition)throw new Exception(name); passed++; Console.WriteLine("OK: "+name); }
    public static int Main(string[] args)
    {
        try { Run(args[0]).GetAwaiter().GetResult(); Console.WriteLine(passed+" testes da central aprovados."); return 0; }
        catch(Exception e){Console.WriteLine("FALHOU: "+e);return 1;}
    }
    static async Task Run(string package)
    {
        var modules=Catalog.All(); Assert(modules.Count==12 && modules.Select(m=>m.Id).Distinct().Count()==12,"catálogo completo e identificadores únicos");
        var engine=new Engine(package,Path.Combine(package,"TestData"));
        Console.WriteLine(engine.VerifyAll()); Assert(true,"integridade de todos os arquivos empacotados");
        bool blocked=false; try { Engine.SafePath(package,"..\\fora.exe"); } catch(IOException){blocked=true;} Assert(blocked,"travessia de diretório bloqueada");
        blocked=false; try { Engine.SafePath(package,"C:\\Windows\\explorer.exe"); } catch(IOException){blocked=true;} Assert(blocked,"caminho absoluto bloqueado");
        Assert(Engine.Classify(false,0,null)=="Encerrado · conferir resultado","legado não gera sucesso falso");
        Assert(Engine.Classify(true,0,null)=="Resultado não confirmado","resultado nativo ausente não vira sucesso");
        Assert(Engine.Classify(true,1,new ModuleResult{Status="OK"})=="Falha","código de falha prevalece sobre JSON");
        Assert(Engine.Classify(true,3010,null)=="Reinicialização necessária","reinício tratado separadamente");
        Assert(Engine.Classify(true,0,new ModuleResult{Status="REVIEW"})=="Encerrado · conferir resultado","instalação sem validação permanece para conferência");
        // Um módulo ausente é bloqueado antes de executar qualquer processo.
        blocked=false; try { await engine.Execute(new Module{Id="missing",Name="Teste",File="missing.exe"},new Dictionary<string,object>(),s=>{}); } catch(IOException){blocked=true;} Assert(blocked,"módulo não autorizado no manifesto bloqueado");
        string script=Path.Combine(package,"modules","Central.ps1"); string original=File.ReadAllText(script,Encoding.UTF8); byte[] originalBytes=File.ReadAllBytes(script);
        try { File.AppendAllText(script,"\r\n# alterado para teste\r\n"); blocked=false; try{engine.Verify("modules\\Central.ps1");}catch(IOException){blocked=true;} Assert(blocked,"módulo adulterado bloqueado"); }
        finally { File.WriteAllBytes(script,originalBytes); }
        var request=new Dictionary<string,object>(); var read=modules.First(m=>m.Id=="nx-check");
        RunRecord result=await engine.Execute(read,request,s=>{});
        Assert(result.Status=="Concluído e verificado" && result.ExitCode==0,"integração real com PowerShell somente leitura");
        Assert(File.Exists(Path.Combine(result.Folder,"relatorio.html")),"relatório HTML produzido");
        var reread=new Engine(package,engine.Data).History(); Assert(reread.Any(r=>r.Id==result.Id && r.Ended!=null),"histórico persiste ao reabrir");
        // Modo desconhecido: passa pelo mesmo processo, sem qualquer alteração no Windows.
        result=await engine.Execute(new Module{Id="bad-mode",Name="Teste de falha",Native=true,Mode="Unknown",File="modules\\Central.ps1"},new Dictionary<string,object>(),s=>{});
        Assert(result.Status=="Falha" && result.ExitCode!=0,"falha nativa registrada sem derrubar a central");
    }
}
