using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace FrankensteinTI
{
    internal sealed class UpdateManifest
    {
        public string Product { get; set; }
        public string Version { get; set; }
        public string InstallerUrl { get; set; }
        public string Sha256 { get; set; }
        public long Size { get; set; }
        public string Notes { get; set; }
    }
    internal static class Distribution
    {
        public const string ChannelUrl="https://raw.githubusercontent.com/ti1gustavoalves-crypto/Otimizar-windows/main/frankenstein/releases/update-manifest.json";
        public static string InstallRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","FrankensteinTI"); } }
        public static bool IsInstalled { get { return string.Equals(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\'),Path.Combine(InstallRoot,"App"),StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"installed.mode")); } }
        public static string DataRoot { get { return IsInstalled?Path.Combine(InstallRoot,"Dados"):Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Dados"); } }
        public static Version Current { get { return typeof(Distribution).Assembly.GetName().Version; } }
        public static void Validate(UpdateManifest m)
        {
            Version version; Uri uri;
            if(m==null || m.Product!="FrankensteinTI" || !Version.TryParse(m.Version,out version)) throw new InvalidDataException("Manifesto de atualização inválido para este aplicativo.");
            if(!Uri.TryCreate(m.InstallerUrl,UriKind.Absolute,out uri) || uri.Scheme!="https" || uri.Host!="github.com" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidDataException("Origem do instalador não autorizada.");
            string expected="/ti1gustavoalves-crypto/Otimizar-windows/releases/download/frankenstein-v"+version.ToString(3)+"/InstalarFrankensteinTI.exe";
            if(uri.AbsolutePath!=expected || !Regex.IsMatch(m.Sha256??"","\\A[0-9a-fA-F]{64}\\z") || m.Size<1024 || m.Size>100*1024*1024) throw new InvalidDataException("Versão, tamanho ou checksum do instalador inválido.");
        }
        static HttpWebResponse Request(string url)
        {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var req=(HttpWebRequest)WebRequest.Create(url); req.UserAgent="FrankensteinTI/"+Current; req.Timeout=25000; req.ReadWriteTimeout=25000; req.AllowAutoRedirect=true;
            var response=(HttpWebResponse)req.GetResponse();
            if(response.ResponseUri.Scheme!="https") { response.Dispose(); throw new IOException("Redirecionamento sem HTTPS bloqueado."); }
            return response;
        }
        public static UpdateManifest Check()
        {
            using(var response=Request(ChannelUrl)) using(var stream=response.GetResponseStream()) using(var memory=new MemoryStream())
            {
                CopyLimited(stream,memory,65536,null);
                var m=new JavaScriptSerializer().Deserialize<UpdateManifest>(Encoding.UTF8.GetString(memory.ToArray()).TrimStart('\ufeff'));
                Validate(m); return m;
            }
        }
        internal static void CopyLimited(Stream input,Stream output,long limit,Action<long> report)
        {
            byte[] buffer=new byte[32768]; long total=0; int n;
            while((n=input.Read(buffer,0,buffer.Length))>0) { total+=n; if(total>limit)throw new IOException("Download excedeu o tamanho permitido."); output.Write(buffer,0,n); if(report!=null)report(total); }
        }
        public static string Download(UpdateManifest m,Action<int> progress)
        {
            Validate(m);
            string folder=Path.Combine(DataRoot,"Atualizacoes",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"InstalarFrankensteinTI.exe"), partial=path+".partial";
            try
            {
                using(var response=Request(m.InstallerUrl)) using(var stream=response.GetResponseStream()) using(var output=File.Create(partial)) CopyLimited(stream,output,m.Size,n=>progress((int)(100*n/m.Size)));
                if(new FileInfo(partial).Length!=m.Size || !string.Equals(Engine.Hash(partial),m.Sha256,StringComparison.OrdinalIgnoreCase)) throw new IOException("O instalador não passou na conferência de tamanho/SHA-256.");
                var info=FileVersionInfo.GetVersionInfo(partial);
                if(info.ProductName!="Frankenstein TI Installer" || new Version(info.FileVersion)!=new Version(m.Version)) throw new IOException("A identificação do instalador diverge do manifesto.");
                File.Move(partial,path); return path;
            }
            finally { if(File.Exists(partial))File.Delete(partial); }
        }
    }
}
