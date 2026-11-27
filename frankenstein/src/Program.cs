using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Frankenstein TI")]
[assembly: AssemblyProduct("Frankenstein TI")]
[assembly: AssemblyDescription("Bancada integrada de suporte técnico")]
[assembly: AssemblyVersion("0.4.0.0")]
[assembly: AssemblyFileVersion("0.4.0.0")]
namespace FrankensteinTI
{
    internal static class Program
    {
        [STAThread] static void Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Style.LoadTheme();
            Application.ThreadException+=delegate(object s,ThreadExceptionEventArgs e) { LogException(e.Exception); MessageBox.Show(e.Exception.Message,"Frankenstein TI · falha na interface",MessageBoxButtons.OK,MessageBoxIcon.Error); };
            AppDomain.CurrentDomain.UnhandledException+=delegate(object s,UnhandledExceptionEventArgs e) { LogException(e.ExceptionObject as Exception); };
            bool owns;
            using(var mutex=new Mutex(true,"Local\\FrankensteinTI-Central",out owns))
            {
                if(!owns && Array.IndexOf(args,"--wait-for-instance")>=0) { try { owns=mutex.WaitOne(15000); } catch(AbandonedMutexException) { owns=true; } }
                if(!owns) { if(Array.IndexOf(args,"--maintenance")<0)MessageBox.Show("O Frankenstein já está aberto.","Frankenstein TI"); return; }
                try { MigrateData(); if(Array.IndexOf(args,"--maintenance")>=0){Session.Begin("Manutenção agendada",false);try{Protection.EnsureCheckpoint();CodexPerformanceOptimizer.V2Engine.RunMaintenance();Session.End("Encerrado · conferir relatório","Manutenção agendada");}catch(Exception ex){Session.End("Manutenção bloqueada ou interrompida",ex.Message);LogException(ex);}}else Application.Run(new MainWindow()); }
                catch(Exception e) { LogException(e); MessageBox.Show("Não foi possível iniciar o Frankenstein. Confira o acesso à pasta de dados.\r\n\r\n"+e.Message,"Frankenstein TI",MessageBoxButtons.OK,MessageBoxIcon.Error); }
                finally { mutex.ReleaseMutex(); }
            }
        }
        static void MigrateData() { string old=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"modules","optimizer","Dados do Otimizador"), target=Path.Combine(Distribution.DataRoot,"Otimizador"); if(Directory.Exists(old)&&!Directory.Exists(target)){string stage=target+"-migracao-"+Guid.NewGuid().ToString("N");CopyData(old,stage);Directory.Move(stage,target);} }
        static void CopyData(string source,string target){Directory.CreateDirectory(target);foreach(string f in Directory.GetFiles(source))File.Copy(f,Path.Combine(target,Path.GetFileName(f)),false);foreach(string d in Directory.GetDirectories(source)){if((File.GetAttributes(d)&FileAttributes.ReparsePoint)!=0)continue;CopyData(d,Path.Combine(target,Path.GetFileName(d)));}}
        internal static void LogException(Exception error) { try { string folder=Path.Combine(Distribution.DataRoot,"Falhas"); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".txt"),error==null?"Falha não identificada.":error.ToString()); } catch {} }
    }
    internal static partial class Style
    {
        public static Color Bg=Color.FromArgb(13,19,25), Surface=Color.FromArgb(22,31,39), Border=Color.FromArgb(43,57,67), Text=Color.FromArgb(235,242,242), Muted=Color.FromArgb(157,175,185), Mint=Color.FromArgb(153,237,190), Amber=Color.FromArgb(243,201,125);
        public static Label Label(string text,float size,Color color,bool bold=false) { return new Label { Text=text, ForeColor=color, BackColor=Color.Transparent, Font=new Font(FontName,size,bold?FontStyle.Bold:FontStyle.Regular), AutoEllipsis=true }; }
        public static Button Button(string text,bool primary=false) { var b=new Button { Text=text, Tag=primary?"primary":null, FlatStyle=FlatStyle.Flat, BackColor=primary?Mint:Surface,ForeColor=primary?Bg:Text,Cursor=Cursors.Hand,Height=42,Font=new Font(FontName,10,FontStyle.Bold),UseVisualStyleBackColor=false }; b.FlatAppearance.BorderColor=Border; b.FlatAppearance.BorderSize=primary?0:1; b.FlatAppearance.MouseOverBackColor=primary?Mint:Hover; return b; }
    }
}
