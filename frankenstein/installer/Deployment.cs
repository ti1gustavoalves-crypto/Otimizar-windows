using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace FrankensteinSetup
{
    internal static class Deployment
    {
        public static string Hash(string file) { using(var sha=SHA256.Create())using(var s=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(s)).Replace("-",""); }
        public static string Under(string root,string relative)
        {
            string prefix=Path.GetFullPath(root).TrimEnd('\\')+"\\";
            if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(":"))throw new IOException("Caminho inválido no pacote.");
            string full=Path.GetFullPath(Path.Combine(prefix,relative));
            if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("Arquivo fora da pasta de instalação.");
            CheckLinks(full); return full;
        }
        public static void CheckLinks(string path)
        {
            string part=Path.GetFullPath(path);
            while(!string.IsNullOrEmpty(part)) { if((Directory.Exists(part)||File.Exists(part)) && (File.GetAttributes(part)&FileAttributes.ReparsePoint)!=0)throw new IOException("A instalação não aceita pastas redirecionadas: "+part); part=Path.GetDirectoryName(part); }
        }
        public static void Extract(string zipFile,string destination)
        {
            CheckLinks(destination); Directory.CreateDirectory(destination); long total=0;
            using(var zip=ZipFile.OpenRead(zipFile))
            {
                if(zip.Entries.Count>500)throw new IOException("Pacote com arquivos em excesso.");
                foreach(var item in zip.Entries)
                {
                    if(((item.ExternalAttributes>>16)&0xF000)==0xA000)throw new IOException("Link simbólico bloqueado no pacote.");
                    string target=Under(destination,item.FullName.Replace('/','\\'));
                    if(string.IsNullOrEmpty(item.Name)) { Directory.CreateDirectory(target);continue; }
                    total+=item.Length; if(item.Length>100*1024*1024 || total>300*1024*1024)throw new IOException("Pacote excede o tamanho permitido.");
                    Directory.CreateDirectory(Path.GetDirectoryName(target)); item.ExtractToFile(target,false);
                }
            }
        }
        public static void Verify(string folder)
        {
            string path=Under(folder,"package-files.json");
            var hashes=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(path,Encoding.UTF8));
            if(!hashes.ContainsKey("FrankensteinTI.exe")||!hashes.ContainsKey("integridade.json"))throw new IOException("Pacote incompleto.");
            foreach(var pair in hashes) if(!string.Equals(Hash(Under(folder,pair.Key)),pair.Value,StringComparison.OrdinalIgnoreCase))throw new IOException("Arquivo corrompido: "+pair.Key);
            foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories)) { string relative=file.Substring(folder.TrimEnd('\\').Length+1); if(relative!="package-files.json"&&!hashes.ContainsKey(relative))throw new IOException("Arquivo extra no pacote: "+relative); }
        }
        static void CopyTree(string source,string destination)
        {
            CheckLinks(source); CheckLinks(destination); Directory.CreateDirectory(destination);
            foreach(string file in Directory.GetFiles(source)) { CheckLinks(file); File.Copy(file,Under(destination,Path.GetFileName(file)),false); }
            foreach(string dir in Directory.GetDirectories(source))CopyTree(dir,Under(destination,Path.GetFileName(dir)));
        }
        // simulateFailure é utilizado exclusivamente pela suíte isolada de rollback.
        public static string Install(string root,string zipFile,string expectedHash,bool simulateFailure=false)
        {
            root=Path.GetFullPath(root); CheckLinks(root); Directory.CreateDirectory(root);
            if(!string.Equals(Hash(zipFile),expectedHash,StringComparison.OrdinalIgnoreCase))throw new IOException("O pacote embutido falhou na verificação SHA-256.");
            using(var locked=new FileStream(Under(root,"installation.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                string stage=Under(root,"stage-"+Guid.NewGuid().ToString("N"));
                string app=Under(root,"App"), previous=Under(root,"previous-"+DateTime.Now.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
                bool moved=false, activated=false;
                try
                {
                    Extract(zipFile,stage); Verify(stage);
                    if(File.Exists(Path.Combine(app,"FrankensteinTI.exe")))
                    {
                        Version installed, incoming;
                        string oldVersion=System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(app,"FrankensteinTI.exe")).FileVersion;
                        string newVersion=System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(stage,"FrankensteinTI.exe")).FileVersion;
                        if(Version.TryParse(oldVersion,out installed)&&Version.TryParse(newVersion,out incoming)&&incoming<installed)throw new IOException("Uma versão mais recente já está instalada. O downgrade foi bloqueado.");
                        foreach(string executable in Directory.GetFiles(app,"*.exe",SearchOption.AllDirectories))using(var probe=new FileStream(executable,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
                    }
                    string optimizerData=Path.Combine(app,"modules","optimizer","Dados do Otimizador");
                    if(Directory.Exists(optimizerData))CopyTree(optimizerData,Path.Combine(stage,"modules","optimizer","Dados do Otimizador"));
                    if(Directory.Exists(app)) { Directory.Move(app,previous);moved=true; }
                    if(simulateFailure)throw new IOException("Falha de troca simulada pelo teste.");
                    Directory.Move(stage,app);activated=true;
                    if(!File.Exists(Path.Combine(app,"FrankensteinTI.exe")))throw new IOException("Executável não encontrado após a troca.");
                    return app;
                }
                catch
                {
                    if(activated&&Directory.Exists(app))Directory.Move(app,Under(root,"failed-"+Guid.NewGuid().ToString("N")));
                    if(moved&&Directory.Exists(previous)&&!Directory.Exists(app))Directory.Move(previous,app);
                    throw;
                }
                finally { if(Directory.Exists(stage)) { CheckLinks(stage); Directory.Delete(stage,true); } }
            }
        }
        public static void RemoveApp(string root)
        {
            root=Path.GetFullPath(root); CheckLinks(root);
            using(var locked=new FileStream(Under(root,"installation.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                string app=Under(root,"App"), data=Path.Combine(app,"modules","optimizer","Dados do Otimizador");
                if(Directory.Exists(data))CopyTree(data,Under(root,"Dados\\Otimizador-"+DateTime.Now.ToString("yyyyMMddHHmmss")));
                if(Directory.Exists(app)) { foreach(string entry in Directory.GetFileSystemEntries(app,"*",SearchOption.AllDirectories))CheckLinks(entry); Directory.Delete(app,true); }
            }
        }
    }
}
