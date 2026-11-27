using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Web.Script.Serialization;
using Microsoft.Win32;
namespace FrankensteinTI
{
    internal sealed class ProxyValue {public string Name {get;set;}public bool Exists {get;set;}public string Kind {get;set;}public string Value {get;set;}}
    internal sealed class ProxyBackup {public string User {get;set;}public string Machine {get;set;}public string State {get;set;}public List<ProxyValue> Values {get;set;}}
    internal static class ProxyRecovery
    {
        const string Key="Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings";
        static readonly string[] Names={"ProxyEnable","ProxyServer","ProxyOverride","AutoConfigURL"};
        internal static void Validate(ProxyBackup b)
        {
            if(b==null||b.Values==null||b.Values.Count!=4||b.Values.Select(v=>v.Name).Distinct().Count()!=4||b.Values.Any(v=>!Names.Contains(v.Name)||v.Value==null||v.Value.Length>32768||v.Kind!=(v.Name=="ProxyEnable"?"DWord":"String")))throw new InvalidDataException("Backup de proxy inválido.");
            int n;if(!int.TryParse(b.Values.Single(v=>v.Name=="ProxyEnable").Value,out n))throw new InvalidDataException("Valor DWORD inválido.");
        }
        internal static string Capture()
        {
            var b=new ProxyBackup{User=WindowsIdentity.GetCurrent().User.Value,Machine=Environment.MachineName,State="Preparado",Values=new List<ProxyValue>()};
            using(var key=Registry.CurrentUser.OpenSubKey(Key)){foreach(string name in Names){bool exists=key!=null&&key.GetValueNames().Contains(name);string kind=name=="ProxyEnable"?"DWord":"String";if(exists&&key.GetValueKind(name).ToString()!=kind)throw new IOException("Tipo inesperado em "+name+"; alteração bloqueada.");b.Values.Add(new ProxyValue{Name=name,Exists=exists,Kind=kind,Value=exists?Convert.ToString(key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames)):(name=="ProxyEnable"?"0":"")});}}
            string folder=Path.Combine(Distribution.DataRoot,"RecuperacaoProxy");Directory.CreateDirectory(folder);string path=Path.Combine(folder,Guid.NewGuid().ToString("N")+".json");Write(path,b);return path;
        }
        static void Write(string path,ProxyBackup b){string temp=path+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(b));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
        internal static void Applied(string path){var b=Read(path);b.State="Aplicado";Write(path,b);}
        internal static ProxyBackup Read(string path){string root=Distribution.DataRoot;string full=Path.GetFullPath(path);string prefix=Path.GetFullPath(root).TrimEnd('\\')+"\\";if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("Backup fora da pasta de dados.");Engine.SafePath(root,full.Substring(prefix.Length));if(new FileInfo(full).Length>200000)throw new IOException("Backup excede o limite.");var b=new JavaScriptSerializer().Deserialize<ProxyBackup>(File.ReadAllText(full));Validate(b);return b;}
        internal static string Restore(string path)
        {
            var b=Read(path);if(b.State!="Aplicado"||b.User!=WindowsIdentity.GetCurrent().User.Value||b.Machine!=Environment.MachineName)throw new IOException("Backup já restaurado, incompleto ou pertencente a outro usuário/computador.");
            using(var key=Registry.CurrentUser.CreateSubKey(Key)){
                if(Convert.ToInt32(key.GetValue("ProxyEnable",-1))!=0||Names.Skip(1).Any(n=>key.GetValueNames().Contains(n)))throw new IOException("O proxy mudou após o reparo. Restauração automática bloqueada para preservar alterações posteriores; revise o backup manualmente.");
                foreach(var v in b.Values){if(!v.Exists)key.DeleteValue(v.Name,false);else if(v.Kind=="DWord")key.SetValue(v.Name,int.Parse(v.Value),RegistryValueKind.DWord);else key.SetValue(v.Name,v.Value,RegistryValueKind.String);}
                foreach(var v in b.Values)if(key.GetValueNames().Contains(v.Name)!=v.Exists||(v.Exists&&Convert.ToString(key.GetValue(v.Name))!=v.Value))throw new IOException("Falha na verificação de "+v.Name);
            }
            b.State="Restaurado";Write(path,b);return "Proxy do usuário restaurado e conferido. Cache DNS não é restaurável; WinHTTP e servidores DNS foram preservados.";
        }
    }
}
