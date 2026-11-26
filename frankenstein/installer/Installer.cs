using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Instalar Frankenstein TI")]
[assembly: AssemblyProduct("Frankenstein TI Installer")]
[assembly: AssemblyVersion("0.3.2.0")]
[assembly: AssemblyFileVersion("0.3.2.0")]
namespace FrankensteinSetup
{
    internal static class Setup
    {
        internal static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","FrankensteinTI");
        internal const string RegPath=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FrankensteinTI";
        [STAThread] static void Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm(Array.IndexOf(args,"--uninstall")>=0));
        }
        public static void Shortcut(string folder,bool create)
        {
            string path=Path.Combine(folder,"Frankenstein TI.lnk");
            Type type=Type.GetTypeFromProgID("WScript.Shell"); object shell=Activator.CreateInstance(type); object link=null;
            try
            {
                if(!create&&!File.Exists(path))return;
                link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path}); Type t=link.GetType();
                if(create)
                {
                    t.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{Path.Combine(Root,"App","FrankensteinTI.exe")});
                    t.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{Path.Combine(Root,"App")});
                    t.InvokeMember("Description",BindingFlags.SetProperty,null,link,new object[]{"Frankenstein TI · central de suporte"});
                    t.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
                }
                else
                {
                    string target=Convert.ToString(t.InvokeMember("TargetPath",BindingFlags.GetProperty,null,link,null));
                    if(string.Equals(target,Path.Combine(Root,"App","FrankensteinTI.exe"),StringComparison.OrdinalIgnoreCase))File.Delete(path);
                }
            }
            finally { if(link!=null)Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(shell); }
        }
        public static void Register()
        {
            string maintenance=Path.Combine(Root,"ManutencaoFrankensteinTI.exe");
            if(!string.Equals(Application.ExecutablePath,maintenance,StringComparison.OrdinalIgnoreCase))File.Copy(Application.ExecutablePath,maintenance,true);
            using(var key=Registry.CurrentUser.CreateSubKey(RegPath))
            {
                key.SetValue("DisplayName","Frankenstein TI");key.SetValue("DisplayVersion","0.3.2");key.SetValue("Publisher","Gustavo Alves");key.SetValue("InstallLocation",Root);key.SetValue("DisplayIcon",Path.Combine(Root,"App","FrankensteinTI.exe"));
                key.SetValue("UninstallString","\""+maintenance+"\" --uninstall");key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);
            }
        }
    }
    internal sealed class SetupForm : Form
    {
        bool running, uninstall;
        Button action, launch; Label status; CheckBox desktop;
        public SetupForm(bool remove)
        {
            uninstall=remove;Text=remove?"Desinstalar Frankenstein TI":"Instalar Frankenstein TI 0.3.2";ClientSize=new Size(620,400);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(13,19,25);ForeColor=Color.White;Font=new Font("Segoe UI",10);
            var brand=new Label{Text="F / TI",Font=new Font("Segoe UI",27,FontStyle.Bold),ForeColor=Color.FromArgb(153,237,190),Location=new Point(28,22),Size=new Size(550,55)};
            var title=new Label{Text=remove?"Desinstalar a central":"Sua bancada de suporte. Instalada.",Font=new Font("Segoe UI",20,FontStyle.Bold),Location=new Point(28,90),Size=new Size(565,45)};
            status=new Label{Text=remove?"Remove o aplicativo e seus atalhos. Históricos, backups e versões anteriores permanecem na pasta de instalação.":"Instalação para seu usuário, com temas e atualização pelo GitHub.\r\n\r\nDestino: "+Setup.Root,Location=new Point(30,151),Size=new Size(554,111)};
            desktop=new CheckBox{Text="Criar atalho na Área de Trabalho",Checked=true,Location=new Point(30,270),Size=new Size(400,26),Visible=!remove};
            action=new Button{Text=remove?"Desinstalar":"Instalar / atualizar",Location=new Point(350,324),Size=new Size(235,44),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(153,237,190),ForeColor=Color.FromArgb(13,19,25)};
            launch=new Button{Text="Abrir Frankenstein TI",Location=new Point(30,324),Size=new Size(290,44),FlatStyle=FlatStyle.Flat,Visible=false};
            launch.Click+=delegate{Process.Start(new ProcessStartInfo(Path.Combine(Setup.Root,"App","FrankensteinTI.exe")){UseShellExecute=true});Close();};
            action.Click+=async delegate{await Run();};Controls.AddRange(new Control[]{brand,title,status,desktop,action,launch});
            FormClosing+=delegate(object s,FormClosingEventArgs e){if(running)e.Cancel=true;};
        }
        async Task Run()
        {
            if(Convert.ToString(action.Tag)=="done"){Close();return;}
            if(running)return;running=true;action.Enabled=false;desktop.Enabled=false;
            try
            {
                status.Text="Conferindo arquivos e preparando a instalação...";
                await Task.Run(delegate
                {
                    using(var gate=AppShutdown.Acquire(Setup.Root,message=>BeginInvoke((Action)delegate{status.Text=message;})))
                    {
                        try
                        {
                            // Arquivos travados de módulos ainda abertos impedem a troca; a versão anterior é preservada.
                            if(uninstall)Deployment.RemoveApp(Setup.Root);
                            else
                            {
                                string payload=Path.Combine(Path.GetTempPath(),"FrankensteinTI-"+Guid.NewGuid().ToString("N")+".zip");
                                try { using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload"))using(var file=File.Create(payload))stream.CopyTo(file);Deployment.Install(Setup.Root,payload,PackageInfo.Sha256); }
                                finally { if(File.Exists(payload))File.Delete(payload); }
                            }
                        }
                        finally{gate.ReleaseMutex();}
                    }
                });
                // COM de atalhos é executado na thread STA da interface.
                if(uninstall)
                {
                    Setup.Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),false);Setup.Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.Programs),false);
                    Registry.CurrentUser.DeleteSubKeyTree(Setup.RegPath,false);status.Text="Aplicativo removido. Seus dados e as cópias anteriores foram preservados em:\r\n"+Setup.Root;action.Text="Fechar";
                }
                else
                {
                    Setup.Register();Setup.Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.Programs),true);if(desktop.Checked)Setup.Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),true);
                    status.Text="Frankenstein TI 0.3.2 instalado.\r\n\r\nUse o botão Atualizações dentro da central para buscar novas versões. Históricos e temas são preservados nas atualizações.";launch.Visible=true;action.Text="Concluir";
                }
                action.Enabled=true;action.Tag="done";
            }
            catch(Exception e){status.Text="Não foi possível concluir: "+e.Message;action.Enabled=true;desktop.Enabled=!uninstall;}
            finally{running=false;}
        }
    }
}
