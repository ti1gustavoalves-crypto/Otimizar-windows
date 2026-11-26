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
[assembly: AssemblyDescription("Central modular de suporte técnico")]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]
namespace FrankensteinTI
{
    internal static class Program
    {
        [STAThread] static void Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Style.LoadTheme();
            Application.ThreadException+=delegate(object s,ThreadExceptionEventArgs e) { LogException(e.Exception); MessageBox.Show(e.Exception.Message,"Frankenstein TI · falha na interface",MessageBoxButtons.OK,MessageBoxIcon.Error); };
            AppDomain.CurrentDomain.UnhandledException+=delegate(object s,UnhandledExceptionEventArgs e) { LogException(e.ExceptionObject as Exception); };
            bool owns;
            using(var mutex=new Mutex(true,"Local\\FrankensteinTI-Central",out owns))
            {
                if(!owns) { MessageBox.Show("A central já está aberta.","Frankenstein TI"); return; }
                try { Application.Run(new MainWindow()); }
                catch(Exception e) { MessageBox.Show("Não foi possível abrir a central. Extraia o ZIP para uma pasta com permissão de gravação.\r\n\r\n"+e.Message,"Frankenstein TI",MessageBoxButtons.OK,MessageBoxIcon.Error); }
                finally { mutex.ReleaseMutex(); }
            }
        }
        static void LogException(Exception error) { try { string folder=Path.Combine(Distribution.DataRoot,"Falhas"); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".txt"),error==null?"Falha não identificada.":error.ToString()); } catch {} }
    }
    internal static partial class Style
    {
        public static Color Bg=Color.FromArgb(13,19,25), Surface=Color.FromArgb(22,31,39), Border=Color.FromArgb(43,57,67), Text=Color.FromArgb(235,242,242), Muted=Color.FromArgb(157,175,185), Mint=Color.FromArgb(153,237,190), Amber=Color.FromArgb(243,201,125);
        public static Label Label(string text,float size,Color color,bool bold=false) { return new Label { Text=text, ForeColor=color, BackColor=Color.Transparent, Font=new Font(FontName,size,bold?FontStyle.Bold:FontStyle.Regular), AutoEllipsis=true }; }
        public static Button Button(string text,bool primary=false) { var b=new Button { Text=text, FlatStyle=FlatStyle.Flat, BackColor=primary?Mint:Surface,ForeColor=primary?Bg:Text,Cursor=Cursors.Hand,Height=42,Font=new Font(FontName,10,FontStyle.Bold),UseVisualStyleBackColor=false }; b.FlatAppearance.BorderColor=Border; b.FlatAppearance.BorderSize=primary?0:1; b.FlatAppearance.MouseOverBackColor=primary?Mint:Hover; return b; }
    }
    internal sealed class ToolCard : Panel
    {
        public Module Module; public bool Selected;
        public ToolCard(Module module,Action<Module> select)
        {
            Module=module; Height=154; BackColor=Style.Surface; Margin=new Padding(0,0,14,14); Padding=new Padding(18); Cursor=Cursors.Hand;
            var badge=Style.Label(module.Badge,8, module.Changes?Style.Amber:Style.Mint,true); badge.SetBounds(18,16,340,20);
            var title=Style.Label(module.Name,12,Style.Text,true); title.SetBounds(18,43,340,28);
            var body=Style.Label(module.Summary,9.5f,Style.Muted); body.SetBounds(18,77,340,54);
            Controls.AddRange(new Control[]{badge,title,body});
            Click+=delegate { select(module); }; foreach(Control c in Controls) c.Click+=delegate { select(module); };
            Resize+=delegate { foreach(Control c in Controls) c.Width=Math.Max(30,ClientSize.Width-36); };
        }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using(var pen=new Pen(Selected?Style.Mint:Style.Border,Selected?2:1)) e.Graphics.DrawRectangle(pen,1,1,Width-3,Height-3); }
    }
    internal sealed class MainWindow : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
        readonly Engine engine;
        readonly List<Module> modules=Catalog.All();
        readonly Dictionary<string,Button> nav=new Dictionary<string,Button>();
        FlowLayoutPanel cards;
        Panel main, detail;
        Label title, subtitle, count, selectedTitle, selectedBadge, status, searchHint;
        TextBox search, description;
        Button execute, openRun, updates;
        ComboBox themes;
        Module selected;
        RunRecord last;
        bool busy, historyView, renderingPreview;
        string group="Todos os módulos";
        DataGridView history;
        public MainWindow(bool preview=false)
        {
            Text="Frankenstein TI  |  Central de suporte"; BackColor=Style.Bg; ForeColor=Style.Text; Font=new Font(Style.FontName,10); Size=new Size(1440,900); MinimumSize=new Size(1120,760); StartPosition=FormStartPosition.CenterScreen; AutoScaleMode=AutoScaleMode.Dpi;
            engine=new Engine(AppDomain.CurrentDomain.BaseDirectory,Path.Combine(Distribution.DataRoot,"Execucoes"));
            Build(); RenderCards(); SelectModule(modules[0]);
            Shown+=async delegate { ActiveControl=search; ApplyTitleTheme(); if(!preview)await CheckUpdates(false); };
            FormClosing+=delegate(object s,FormClosingEventArgs e) { if(busy) { e.Cancel=true; MessageBox.Show(this,"Existe um módulo em execução. Conclua e feche a ferramenta antes de sair da central. A operação não será interrompida à força.","Operação em andamento"); } };
        }
        void Build()
        {
            var sidebar=new Panel { Dock=DockStyle.Left, Width=196, BackColor=Style.Sidebar, Padding=new Padding(16) };
            var mark=Style.Label("F / TI",24,Style.Mint,true); mark.SetBounds(20,24,160,45);
            var brand=Style.Label("FRANKENSTEIN",11,Style.Text,true); brand.SetBounds(20,77,170,25);
            var tagline=Style.Label("Sua bancada de suporte.",9,Style.Muted); tagline.SetBounds(20,104,170,35);
            sidebar.Controls.AddRange(new Control[]{mark,brand,tagline});
            int y=152;
            foreach(string name in new[]{"Todos os módulos","Diagnóstico","Instalação","Navegadores","Desempenho","Reparos","Softwares técnicos","Histórico"})
            {
                string current=name; var b=Style.Button(name); b.TextAlign=ContentAlignment.MiddleLeft; b.Padding=new Padding(8,0,0,0); b.SetBounds(12,y,172,43); b.FlatAppearance.BorderSize=0; b.BackColor=sidebar.BackColor;
                b.Click+=delegate { group=current; historyView=current=="Histórico"; title.Text=historyView?"Histórico de atendimentos":current=="Todos os módulos"?"Uma central. Todas as peças.":current; search.Visible=!historyView; searchHint.Visible=!historyView; if(historyView) ShowHistory(); else { RenderCards(); if(selected!=null)SelectModule(selected); } UpdateNav(); };
                sidebar.Controls.Add(b); nav[name]=b; y+=45;
            }
            var footer=new Panel{Dock=DockStyle.Bottom,Height=150};
            var themeLabel=Style.Label("PERSONALIZE SUA CENTRAL",7.5f,Style.Muted,true);themeLabel.SetBounds(0,0,170,23);
            themes=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=Style.Surface,ForeColor=Style.Text,Font=new Font(Style.FontName,10),AccessibleName="Tema da interface"};themes.SetBounds(0,27,164,30);foreach(Palette p in Style.Themes)themes.Items.Add(p.Name);themes.SelectedItem=Style.Current.Name;
            themes.SelectedIndexChanged+=delegate{if(renderingPreview)return;Palette palette=Style.Themes[themes.SelectedIndex];try{Style.SaveTheme(palette);Style.ApplyTheme(this,palette);UpdateNavigationTheme();ApplyTitleTheme();}catch(Exception e){MessageBox.Show(this,"Não foi possível salvar o tema: "+e.Message);}};
            updates=Style.Button("Atualizações");updates.Font=new Font(Style.FontName,9);updates.SetBounds(0,65,164,36);updates.Click+=async delegate{await CheckUpdates(true);};
            var foot=Style.Label("VERSÃO 0.2.0  /  "+(Distribution.IsInstalled?"INSTALADO":"PORTÁTIL")+"\r\nWindows 10 e 11 · x64",7.5f,Style.Muted);foot.SetBounds(0,113,172,35);footer.Controls.AddRange(new Control[]{themeLabel,themes,updates,foot});sidebar.Controls.Add(footer);
            var bottom=new Panel { Dock=DockStyle.Bottom, Height=42,BackColor=Style.Sidebar,Padding=new Padding(18,10,18,6) };
            status=Style.Label("Pronto. Escolha um módulo para começar.",9,Style.Muted); status.Dock=DockStyle.Fill; bottom.Controls.Add(status);
            main=new Panel { Dock=DockStyle.Fill,Padding=new Padding(26,22,22,8) };
            var header=new Panel { Dock=DockStyle.Top,Height=132 };
            var eyebrow=Style.Label("CENTRAL DE SUPORTE  /  GUSTAVO",8.5f,Style.Mint,true); eyebrow.SetBounds(0,0,600,22);
            title=Style.Label("Uma central. Todas as peças.",25,Style.Text,true); title.SetBounds(0,28,900,46);
            subtitle=Style.Label("Diagnosticar, instalar, reparar e recuperar. Cada ferramenta no seu lugar.",10,Style.Muted); subtitle.SetBounds(0,81,900,28);
            header.Controls.AddRange(new Control[]{eyebrow,title,subtitle});
            var toolbar=new Panel { Dock=DockStyle.Top,Height=66 };
            search=new TextBox { BackColor=Style.Surface,ForeColor=Style.Text,BorderStyle=BorderStyle.FixedSingle,Font=new Font(Style.FontName,11),AccessibleName="Buscar módulos" }; search.SetBounds(0,6,320,29); search.TextChanged+=delegate { if(!historyView) RenderCards(); };
            searchHint=Style.Label("BUSCAR MÓDULOS",7.5f,Style.Muted); searchHint.SetBounds(0,39,320,18);
            count=Style.Label("",9,Style.Muted); count.SetBounds(340,10,350,32); toolbar.Controls.AddRange(new Control[]{search,searchHint,count});
            var content=new Panel { Dock=DockStyle.Fill };
            detail=new Panel { Dock=DockStyle.Right,Width=300,BackColor=Style.Surface,Padding=new Padding(20) };
            selectedBadge=Style.Label("",8,Style.Mint,true); selectedBadge.Dock=DockStyle.Top; selectedBadge.Height=32;
            selectedTitle=Style.Label("",18,Style.Text,true); selectedTitle.Dock=DockStyle.Top; selectedTitle.Height=92;
            description=new TextBox { Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Style.Surface,ForeColor=Style.Muted,Font=new Font(Style.FontName,10),Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,TabStop=true,AccessibleName="Detalhes do módulo" };
            var actions=new Panel { Dock=DockStyle.Bottom,Height=192 };
            execute=Style.Button("Abrir módulo  →",true); execute.SetBounds(0,4,260,44); execute.Click+=async delegate { await Launch(); };
            openRun=Style.Button("Abrir arquivos da execução"); openRun.SetBounds(0,58,260,40); openRun.Enabled=false; openRun.Click+=delegate { if(last!=null) OpenFolder(last.Folder); };
            var integrity=Style.Button("Conferir integridade do pacote"); integrity.Font=new Font(Style.FontName,8.5f); integrity.SetBounds(0,108,260,36); integrity.Click+=async delegate { integrity.Enabled=false; try { status.Text="Conferindo arquivos..."; string message=await System.Threading.Tasks.Task.Run(()=>engine.VerifyAll()); status.Text=message; MessageBox.Show(this,message,"Integridade do pacote"); } catch(Exception e) { status.Text="Falha na integridade"; MessageBox.Show(this,e.Message,"Integridade",MessageBoxButtons.OK,MessageBoxIcon.Error); } finally { integrity.Enabled=true; } };
            var note=Style.Label("Execução local · uma ferramenta por vez",8,Style.Muted); note.SetBounds(0,157,260,30);
            actions.Controls.AddRange(new Control[]{execute,openRun,integrity,note});
            detail.Controls.Add(description); detail.Controls.Add(selectedTitle); detail.Controls.Add(selectedBadge); detail.Controls.Add(actions);
            cards=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoScroll=true,BackColor=Style.Bg,Padding=new Padding(0,0,8,0) };
            cards.Resize+=delegate { LayoutCards(); };
            history=new DataGridView { Dock=DockStyle.Fill,Visible=false,BackgroundColor=Style.Bg,BorderStyle=BorderStyle.None,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,EnableHeadersVisualStyles=false };
            history.DefaultCellStyle.BackColor=Style.Surface; history.DefaultCellStyle.ForeColor=Style.Text; history.DefaultCellStyle.SelectionBackColor=Color.FromArgb(42,76,65); history.DefaultCellStyle.SelectionForeColor=Style.Text; history.ColumnHeadersDefaultCellStyle.BackColor=Style.Surface; history.ColumnHeadersDefaultCellStyle.ForeColor=Style.Mint; history.ColumnHeadersHeight=40; history.RowTemplate.Height=46; history.GridColor=Style.Border;
            history.Columns.Add("When","Início"); history.Columns.Add("Name","Módulo"); history.Columns.Add("Status","Situação");
            history.SelectionChanged+=delegate { if(history.SelectedRows.Count==0)return; var r=history.SelectedRows[0].Tag as RunRecord; if(r==null)return; last=r; openRun.Enabled=true; selectedBadge.Text="REGISTRO LOCAL"; selectedTitle.Text=r.Name; description.Text=r.Status+"\r\n\r\n"+r.Detail+"\r\n\r\nInício: "+r.Started+"\r\nFim: "+(r.Ended??"Não registrado")+"\r\n\r\n"+r.Folder; execute.Enabled=false; };
            var gap=new Panel { Dock=DockStyle.Right,Width=16 };
            content.Controls.Add(cards); content.Controls.Add(history); content.Controls.Add(gap); content.Controls.Add(detail);
            main.Controls.Add(content); main.Controls.Add(toolbar); main.Controls.Add(header);
            Controls.Add(main); Controls.Add(sidebar); Controls.Add(bottom); UpdateNav();
        }
        void UpdateNav() { foreach(var pair in nav) { pair.Value.BackColor=pair.Key==group?Style.Selected:Style.Sidebar; pair.Value.ForeColor=pair.Key==group?Style.Mint:Style.Muted; } }
        void UpdateNavigationTheme(){UpdateNav();foreach(var card in cards.Controls.OfType<ToolCard>())card.Invalidate();}
        void ApplyTitleTheme(){try{int on=Style.Current.Name=="Claro"?0:1;DwmSetWindowAttribute(Handle,20,ref on,4);}catch{}}
        internal void PreviewTheme(Palette palette,string path,Size size)
        {
            renderingPreview=true;themes.SelectedItem=palette.Name;renderingPreview=false;
            Size=size;Style.ApplyTheme(this,palette);UpdateNavigationTheme();ApplyTitleTheme();PerformLayout();Application.DoEvents();
            using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}
            if(themes.Bottom>themes.Parent.ClientSize.Height || nav["Histórico"].Bottom>themes.Parent.Top)throw new InvalidOperationException("Tema ou navegação sobrepostos.");
        }
        async System.Threading.Tasks.Task CheckUpdates(bool manual)
        {
            if(!updates.Enabled)return;
            if(manual&&busy){MessageBox.Show(this,"Conclua o módulo em execução antes de atualizar.");return;}
            updates.Enabled=false;
            if(manual){busy=true;execute.Enabled=false;}
            try
            {
                var manifest=await System.Threading.Tasks.Task.Run(()=>Distribution.Check());
                bool available=new Version(manifest.Version)>Distribution.Current;updates.Text=available?"Nova versão disponível":"Atualizações";
                if(!manual)return;
                if(!available){MessageBox.Show(this,"Você está na versão mais recente: "+Distribution.Current.ToString(3)+".","Atualizações");return;}
                if(MessageBox.Show(this,"Versão "+manifest.Version+" disponível.\r\n\r\n"+manifest.Notes+"\r\n\r\nBaixar e abrir o instalador? A central será fechada. Seus dados e tema serão preservados na instalação existente.","Atualizar Frankenstein TI",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
                busy=true;execute.Enabled=false;themes.Enabled=false;
                var progress=new Progress<int>(p=>status.Text="Baixando atualização: "+p+"%");
                string installer=await System.Threading.Tasks.Task.Run(()=>Distribution.Download(manifest,p=>((IProgress<int>)progress).Report(p)));
                Process.Start(new ProcessStartInfo(installer){UseShellExecute=true});busy=false;Close();
            }
            catch(Exception e){if(manual)MessageBox.Show(this,"Não foi possível verificar ou baixar a atualização.\r\n\r\n"+e.Message,"Atualizações");else updates.Text="Atualizações · offline";}
            finally{if(manual)busy=false;if(!IsDisposed){updates.Enabled=true;if(manual){execute.Enabled=!historyView;themes.Enabled=true;}}}
        }
        void LayoutCards() { int columns=cards.ClientSize.Width>=610?2:1; int w=Math.Max(220,(cards.ClientSize.Width-32)/columns-14); foreach(Control c in cards.Controls) c.Width=w; }
        void RenderCards()
        {
            history.Visible=false; cards.Visible=true; cards.SuspendLayout(); foreach(Control c in cards.Controls.Cast<Control>().ToArray()) c.Dispose(); cards.Controls.Clear();
            string q=search.Text.Trim(); var visible=modules.Where(m=>(group=="Todos os módulos" || m.Group==group) && (m.Name+" "+m.Group+" "+m.Summary).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0).ToList();
            foreach(Module m in visible) { var card=new ToolCard(m,SelectModule); card.Selected=selected!=null&&selected.Id==m.Id; cards.Controls.Add(card); }
            if(visible.Count==0) { var empty=Style.Label("Nenhum módulo encontrado.\r\nTente outro nome ou categoria.",12,Style.Muted); empty.Height=100; cards.Controls.Add(empty); }
            count.Text=visible.Count+" de "+modules.Count+" módulos"; LayoutCards(); cards.ResumeLayout(); execute.Enabled=!busy&&selected!=null;
        }
        void SelectModule(Module module)
        {
            selected=module; selectedBadge.Text=module.Badge; selectedBadge.ForeColor=module.Changes?Style.Amber:Style.Mint; selectedTitle.Text=module.Name; description.Text=module.Details+"\r\n\r\n"+(module.Admin?"Permissão: administrador, solicitada pelo Windows.":"Permissão: usuário atual; ações específicas podem pedir elevação.");
            execute.Text=module.Changes?"Preparar execução  →":"Executar diagnóstico  →"; execute.Enabled=!busy;
            foreach(ToolCard card in cards.Controls.OfType<ToolCard>()) { card.Selected=card.Module.Id==module.Id; card.Invalidate(); }
        }
        void ShowHistory()
        {
            history.Rows.Clear(); foreach(var r in engine.History()) { string state=r.Status; if(string.IsNullOrEmpty(r.Ended)) state="Sem encerramento registrado"; int i=history.Rows.Add(DateTime.Parse(r.Started).ToString("dd/MM HH:mm"),r.Name,state); history.Rows[i].Tag=r; }
            count.Text=history.Rows.Count+" execuções registradas"; cards.Visible=false; history.Visible=true; execute.Enabled=false;
            if(history.Rows.Count==0) { selectedBadge.Text="HISTÓRICO"; selectedTitle.Text="Tudo começa aqui."; description.Text="As próximas execuções aparecerão nesta lista, com seus resultados e arquivos. Nenhum reparo é iniciado ao abrir a central."; }
        }
        async System.Threading.Tasks.Task Launch()
        {
            if(busy||selected==null)return;
            Module module=selected; var req=new Dictionary<string,object>();
            try
            {
                if(module.Mode=="NxSet")
                {
                    using(var dialog=new NxDialog()) { if(dialog.ShowDialog(this)!=DialogResult.OK)return; req["Server"]=dialog.Server; req["Scope"]=dialog.Scope; module.Admin=dialog.Scope=="Machine"; }
                }
                if(module.Mode=="NxRestore")
                {
                    using(var picker=new OpenFileDialog { Title="Selecione nx-backup.json",Filter="Backup NX|nx-backup.json",InitialDirectory=engine.Data }) { if(picker.ShowDialog(this)!=DialogResult.OK)return; req["Backup"]=picker.FileName; var backup=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(picker.FileName)); if(!backup.ContainsKey("Scope"))throw new IOException("Backup inválido."); module.Admin=Convert.ToString(backup["Scope"])=="Machine"; }
                }
                if(module.Mode=="LabX") { using(var picker=new OpenFileDialog { Title="Selecione o instalador LabXBundle.exe",Filter="Instalador LabX|LabXBundle.exe",CheckFileExists=true }) { if(picker.ShowDialog(this)!=DialogResult.OK)return; req["Installer"]=picker.FileName; req["InstallerHash"]=Engine.Hash(picker.FileName); } }
                if(module.Changes && MessageBox.Show(this,module.Details+"\r\n\r\n"+(req.ContainsKey("Scope")?"Escopo: "+(Convert.ToString(req["Scope"])=="Machine"?"computador inteiro":"usuário atual")+"\r\nServidor: "+req["Server"]+"\r\n\r\n":"")+"Deseja iniciar este módulo?","Revisar · "+module.Name,MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
                busy=true; execute.Enabled=false; status.Text="Preparando "+module.Name+"...";
                last=await engine.Execute(module,req,s=>status.Text=s); openRun.Enabled=true;
                SelectModule(module); selectedBadge.Text=last.Status.ToUpperInvariant(); selectedTitle.Text=module.Name; description.Text=last.Detail+"\r\n\r\nArquivos desta execução:\r\n"+last.Folder;
                if(historyView)ShowHistory();
            }
            catch(Exception e) { status.Text="A operação não foi concluída."; MessageBox.Show(this,e.Message,"Frankenstein TI",MessageBoxButtons.OK,MessageBoxIcon.Error); }
            finally { busy=false; execute.Enabled=!historyView; }
        }
        void OpenFolder(string path) { try { Process.Start(new ProcessStartInfo("explorer.exe",Engine.Quote(path)) { UseShellExecute=true }); } catch(Exception e) { MessageBox.Show(this,e.Message); } }
    }
    internal sealed class NxDialog : Form
    {
        TextBox server; ComboBox scope;
        public string Server { get { return server.Text.Trim(); } }
        public string Scope { get { return scope.SelectedIndex==0?"User":"Machine"; } }
        public NxDialog()
        {
            Text="Configurar licença NX"; ClientSize=new Size(500,260); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false; StartPosition=FormStartPosition.CenterParent; BackColor=Style.Bg; ForeColor=Style.Text; Font=new Font(Style.FontName,10);
            var a=Style.Label("Servidor de licença (porta@servidor)",10,Style.Text); a.SetBounds(22,18,450,25);
            server=new TextBox {Text="28000@minduri.fiemg.com.br",BackColor=Style.Surface,ForeColor=Style.Text}; server.SetBounds(22,50,450,28);
            var b=Style.Label("Aplicar em",10,Style.Text); b.SetBounds(22,95,450,25);
            scope=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Style.Surface,ForeColor=Style.Text }; scope.Items.AddRange(new object[]{"Usuário atual","Computador inteiro (administrador)"}); scope.SelectedIndex=0; scope.SetBounds(22,125,450,30);
            var ok=Style.Button("Continuar",true); ok.SetBounds(302,193,170,42); ok.Click+=delegate { if(!System.Text.RegularExpressions.Regex.IsMatch(Server,@"^[0-9]{1,5}@[a-zA-Z0-9][a-zA-Z0-9.-]*$")) { MessageBox.Show(this,"Use o formato porta@servidor.");return; } int port=int.Parse(Server.Split('@')[0]); if(port<1||port>65535){MessageBox.Show(this,"Porta deve estar entre 1 e 65535.");return;} DialogResult=DialogResult.OK; };
            var cancel=Style.Button("Cancelar"); cancel.SetBounds(118,193,170,42); cancel.DialogResult=DialogResult.Cancel; CancelButton=cancel; AcceptButton=ok;
            Controls.AddRange(new Control[]{a,server,b,scope,ok,cancel});
        }
    }
}
