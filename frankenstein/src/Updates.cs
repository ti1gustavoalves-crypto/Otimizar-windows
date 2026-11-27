using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
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
        internal static bool AllowedEndpoint(Uri uri)
        {
            return uri!=null&&uri.Scheme=="https"&&uri.IsDefaultPort&&string.IsNullOrEmpty(uri.UserInfo)&&string.IsNullOrEmpty(uri.Fragment)&&new[]{"github.com","raw.githubusercontent.com","release-assets.githubusercontent.com","objects.githubusercontent.com"}.Contains(uri.Host);
        }
        static HttpWebResponse Request(string url)
        {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var uri=new Uri(url);
            for(int redirects=0;redirects<=5;redirects++){
                if(!AllowedEndpoint(uri))throw new IOException("Destino de atualização não autorizado.");
                var req=(HttpWebRequest)WebRequest.Create(uri);req.UserAgent="FrankensteinTI/"+Current;req.Timeout=25000;req.ReadWriteTimeout=25000;req.AllowAutoRedirect=false;
                var response=(HttpWebResponse)req.GetResponse();int code=(int)response.StatusCode;
                if(code>=300&&code<400){string location=response.Headers["Location"];response.Dispose();if(string.IsNullOrEmpty(location))throw new IOException("Redirecionamento sem destino.");uri=new Uri(uri,location);continue;}
                return response;
            }
            throw new IOException("Limite de redirecionamentos excedido.");
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
        internal static void CopyDownload(Stream input,Stream output,long size,Action<int> progress)
        {
            if(size<=0)throw new ArgumentOutOfRangeException("size");
            CopyLimited(input,output,size,n=>{if(progress!=null)progress((int)(100*n/size));});
        }
        internal static bool Transient(WebException e)
        {
            var response=e.Response as HttpWebResponse;
            if(response!=null)return response.StatusCode==HttpStatusCode.RequestTimeout||(int)response.StatusCode==429||(int)response.StatusCode>=500;
            return e.Status==WebExceptionStatus.Timeout||e.Status==WebExceptionStatus.ConnectFailure||e.Status==WebExceptionStatus.ConnectionClosed||e.Status==WebExceptionStatus.ReceiveFailure||e.Status==WebExceptionStatus.NameResolutionFailure;
        }
        internal static void RetryTransfer(Action transfer,CancellationToken token,Action<string> stage)
        {
            for(int attempt=1;;attempt++){token.ThrowIfCancellationRequested();try{if(stage!=null)stage("Baixando instalador · tentativa "+attempt+" de 3");transfer();return;}catch(WebException ex){bool retry=attempt<3&&Transient(ex);if(ex.Response!=null)ex.Response.Dispose();token.ThrowIfCancellationRequested();if(!retry)throw;if(stage!=null)stage("Conexão interrompida. Tentando novamente...");if(token.WaitHandle.WaitOne(attempt*1000))token.ThrowIfCancellationRequested();}catch{token.ThrowIfCancellationRequested();throw;}}
        }
        public static string Download(UpdateManifest m,Action<int> progress)
        {return Download(m,progress,CancellationToken.None,null);}
        public static string Download(UpdateManifest m,Action<int> progress,CancellationToken token,Action<string> stage)
        {
            Validate(m);
            string folder=Path.Combine(DataRoot,"Atualizacoes",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"InstalarFrankensteinTI.exe"), partial=path+".partial";
            try
            {
                RetryTransfer(delegate{using(var response=Request(m.InstallerUrl))using(token.Register(response.Close))using(var stream=response.GetResponseStream())using(var output=File.Create(partial))CopyDownload(stream,output,m.Size,p=>{token.ThrowIfCancellationRequested();if(progress!=null)progress(p);});},token,stage);
                token.ThrowIfCancellationRequested();if(stage!=null)stage("Verificando tamanho, SHA-256 e identidade do instalador...");
                if(new FileInfo(partial).Length!=m.Size || !string.Equals(Engine.Hash(partial),m.Sha256,StringComparison.OrdinalIgnoreCase)) throw new IOException("O instalador não passou na conferência de tamanho/SHA-256.");
                var info=FileVersionInfo.GetVersionInfo(partial);
                if(info.ProductName!="Frankenstein TI Installer" || new Version(info.FileVersion)!=new Version(m.Version)) throw new IOException("A identificação do instalador diverge do manifesto.");
                File.Move(partial,path); return path;
            }
            finally { if(File.Exists(partial))File.Delete(partial); }
        }
    }
}
