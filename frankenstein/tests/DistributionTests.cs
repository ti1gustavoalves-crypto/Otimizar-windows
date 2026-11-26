using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using FrankensteinTI;
using FrankensteinSetup;

internal static class DistributionTests
{
    static int passed;
    static void Assert(bool condition,string name){if(!condition)throw new Exception(name);passed++;Console.WriteLine("OK: "+name);}
    static bool Reject(Action action){try{action();return false;}catch{return true;}}
    [STAThread] public static int Main(string[] args)
    {
        try { Run(Path.GetFullPath(args[0]));Console.WriteLine(passed+" testes de distribuição/interface aprovados.");return 0; }
        catch(Exception e){Console.WriteLine("FALHOU: "+e);return 1;}
    }
    static string Zip(string root,string name,string version)
    {
        string folder=Path.Combine(root,name);Directory.CreateDirectory(folder);
        File.Copy(typeof(DistributionTests).Assembly.Location,Path.Combine(folder,"FrankensteinTI.exe"));
        File.WriteAllText(Path.Combine(folder,"version.txt"),version);
        File.WriteAllText(Path.Combine(folder,"integridade.json"),"{}");
        var hashes=Directory.GetFiles(folder).ToDictionary(f=>Path.GetFileName(f),f=>Deployment.Hash(f));
        File.WriteAllText(Path.Combine(folder,"package-files.json"),new JavaScriptSerializer().Serialize(hashes));
        string zip=folder+".zip";ZipFile.CreateFromDirectory(folder,zip);return zip;
    }
    static UpdateManifest Manifest(){return new UpdateManifest{Product="FrankensteinTI",Version="0.2.0.0",InstallerUrl="https://github.com/ti1gustavoalves-crypto/Otimizar-windows/releases/download/frankenstein-v0.2.0/InstalarFrankensteinTI.exe",Sha256=new string('A',64),Size=2000000};}
    static void Run(string root)
    {
        Directory.CreateDirectory(root);
        string first=Zip(root,"first","first"),second=Zip(root,"second","second"),install=Path.Combine(root,"installed");
        Deployment.Install(install,first,Deployment.Hash(first));Assert(File.ReadAllText(Path.Combine(install,"App","version.txt"))=="first","instalação inicial em pasta isolada");
        Directory.CreateDirectory(Path.Combine(install,"Dados"));File.WriteAllText(Path.Combine(install,"Dados","interface.json"),"tema gamer");
        string optimizer=Path.Combine(install,"App","modules","optimizer","Dados do Otimizador");Directory.CreateDirectory(optimizer);File.WriteAllText(Path.Combine(optimizer,"backup.txt"),"preservar");
        Deployment.Install(install,second,Deployment.Hash(second));
        Assert(File.ReadAllText(Path.Combine(install,"App","version.txt"))=="second","atualização troca os arquivos");
        Assert(File.ReadAllText(Path.Combine(install,"Dados","interface.json"))=="tema gamer","preferências e dados externos preservados");
        Assert(File.ReadAllText(Path.Combine(install,"App","modules","optimizer","Dados do Otimizador","backup.txt"))=="preservar","backups do otimizador preservados");
        Assert(Directory.GetDirectories(install,"previous-*").Length==1,"versão anterior mantida");
        Assert(Reject(()=>Deployment.Install(install,first,Deployment.Hash(first),true)),"falha de troca simulada é reportada");
        Assert(File.ReadAllText(Path.Combine(install,"App","version.txt"))=="second","rollback recupera a versão vigente");
        Assert(Reject(()=>Deployment.Install(install,first,new string('0',64))),"pacote com hash incorreto bloqueado");
        Assert(Reject(()=>Deployment.Under(root,"..\\outside.exe")),"extração fora do destino bloqueada");
        Assert(Reject(()=>Deployment.Under(root,"file:stream")),"fluxos alternativos NTFS bloqueados");
        string hostile=Path.Combine(root,"hostile.zip");using(var zip=ZipFile.Open(hostile,ZipArchiveMode.Create)){using(var writer=new StreamWriter(zip.CreateEntry("../escape.txt").Open()))writer.Write("x");}
        Assert(Reject(()=>Deployment.Extract(hostile,Path.Combine(root,"extract"))),"ZIP com caminho malicioso bloqueado");
        File.AppendAllText(Path.Combine(root,"first","version.txt"),"tampered");Assert(Reject(()=>Deployment.Verify(Path.Combine(root,"first"))),"arquivo adulterado no payload bloqueado");
        Deployment.RemoveApp(install);Assert(!Directory.Exists(Path.Combine(install,"App"))&&File.Exists(Path.Combine(install,"Dados","interface.json")),"desinstalação remove app e preserva dados");
        Assert(Directory.GetDirectories(Path.Combine(install,"Dados"),"Otimizador-*").Length==1,"desinstalação arquiva dados do otimizador");
        var manifest=Manifest();Distribution.Validate(manifest);Assert(true,"manifesto do produto e repositório corretos aceito");
        manifest.InstallerUrl=manifest.InstallerUrl.Replace("https:","http:");Assert(Reject(()=>Distribution.Validate(manifest)),"download HTTP bloqueado");
        manifest=Manifest();manifest.InstallerUrl=manifest.InstallerUrl.Replace("github.com","github.com.example.com");Assert(Reject(()=>Distribution.Validate(manifest)),"domínio enganoso bloqueado");
        manifest=Manifest();manifest.Product="Other";Assert(Reject(()=>Distribution.Validate(manifest)),"manifesto de outro produto bloqueado");
        manifest=Manifest();manifest.Sha256="123";Assert(Reject(()=>Distribution.Validate(manifest)),"checksum malformado bloqueado");
        manifest=Manifest();manifest.Version="9.0.0.0";Assert(Reject(()=>Distribution.Validate(manifest)),"URL divergente da versão bloqueada");
        using(var source=new MemoryStream(new byte[100]))using(var target=new MemoryStream())Assert(Reject(()=>Distribution.CopyLimited(source,target,50,null)),"download com tamanho excessivo interrompido");
        Assert(Style.Themes.Length==5&&Style.Themes.Select(p=>p.Name).Distinct().Count()==5,"cinco temas disponíveis");
        foreach(var p in Style.Themes){Assert(Contrast(p.Text,p.Surface)>=4.5&&Contrast(p.Muted,p.Surface)>=4.5&&Contrast(p.Accent,p.Bg)>=4.5,"contraste de texto e ação: "+p.Name);}
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        using(var form=new MainWindow(true))
        {
            form.Show();
            foreach(var p in Style.Themes)form.PreviewTheme(p,Path.Combine(root,"tema-"+p.Name+".png"),new Size(1440,900));
            form.PreviewTheme(Style.Themes[1],Path.Combine(root,"tema-minimo.png"),new Size(1120,760));
            form.Close();
        }
        Assert(true,"cinco temas renderizados e navegação validada no tamanho mínimo");
    }
    static double L(Color c){Func<byte,double> f=x=>{double s=x/255.0;return s<=0.04045?s/12.92:Math.Pow((s+0.055)/1.055,2.4);};return .2126*f(c.R)+.7152*f(c.G)+.0722*f(c.B);}
    static double Contrast(Color a,Color b){double x=L(a),y=L(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
}
