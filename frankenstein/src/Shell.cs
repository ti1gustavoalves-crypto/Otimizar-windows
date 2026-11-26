using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FrankensteinTI
{
    internal sealed class MainWindow : Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
        static readonly uint updateCloseMessage=RegisterWindowMessage("FrankensteinTI.CloseForUpdate.v1");
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
        readonly Dictionary<string,Button> navigation=new Dictionary<string,Button>();
        readonly Dictionary<string,Form> workspaces=new Dictionary<string,Form>();
        Panel canvas;Label status,heading;Button updates;string section="Visão geral";
        CodexPerformanceOptimizer.MainFormV2 optimizer;
        readonly bool preview;
        public MainWindow(bool preview=false)
        {
            this.preview=preview;Text="Frankenstein TI";FormBorderStyle=FormBorderStyle.None;Size=new Size(1440,900);MinimumSize=new Size(1120,760);StartPosition=FormStartPosition.CenterScreen;
            BackColor=Style.Bg;ForeColor=Style.Text;Font=new Font(Style.FontName,10);Padding=new Padding(2);AutoScaleMode=AutoScaleMode.Dpi;
            try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
            Build();Session.Changed+=OnSessionChanged;
            FormClosing+=delegate(object s,FormClosingEventArgs e){if(Session.Busy){e.Cancel=true;MessageBox.Show(this,"Conclua ou cancele a operação em andamento antes de fechar.","Operação em andamento");}};
            FormClosed+=delegate{Session.Changed-=OnSessionChanged;if(optimizer!=null)optimizer.StopWorkspace();foreach(var view in workspaces.Values)view.Dispose();};
            Shown+=async delegate{Navigate("Visão geral");if(!preview)await CheckUpdates(false);};
        }
        void OnSessionChanged(){if(IsDisposed)return;if(InvokeRequired){BeginInvoke((Action)OnSessionChanged);return;}status.Text=Session.Status;if(Session.ClosingForUpdate&&!Session.Busy)BeginInvoke((Action)Close);}
        internal void RequestUpdateClose(){Session.ClosingForUpdate=true;status.Text="Atualização aguardando a conclusão da operação. O aplicativo fechará automaticamente.";if(!Session.Busy)Close();}
        void Build()
        {
            var rail=new Panel{Dock=DockStyle.Left,Width=210,BackColor=Style.Sidebar};
            var logo=new Panel{Location=new Point(22,28),Size=new Size(50,50)};logo.Paint+=delegate(object s,PaintEventArgs e){Brand.Draw(e.Graphics,logo.ClientRectangle,Style.Mint,Style.Sidebar);};
            var name=Style.Label("FRANKEN\nSTEIN / TI",12,Style.Text,true);name.SetBounds(82,32,126,48);
            var cap=Style.Label("SUA BANCADA, CONECTADA",7.5f,Style.Muted,true);cap.SetBounds(23,100,184,28);rail.Controls.AddRange(new Control[]{logo,name,cap});
            int y=151,index=0;
            foreach(string label in new[]{"Visão geral","Desempenho","Manutenção","Programas","Navegadores","Windows e apps","Sistema","Ferramentas técnicas","Histórico","Aparência"}){
                string key=label;var b=Style.Button((++index).ToString("00")+"   "+(label=="Ferramentas técnicas"?"NX e LabX":label));b.TextAlign=ContentAlignment.MiddleLeft;b.Font=new Font(Style.FontName,9.5f);b.SetBounds(12,y,186,44);b.FlatAppearance.BorderSize=0;b.Click+=delegate{Navigate(key);};navigation.Add(key,b);rail.Controls.Add(b);y+=48;
            }
            var foot=new Panel{Dock=DockStyle.Bottom,Height=99,Padding=new Padding(16,4,16,10)};
            updates=Style.Button("Verificar atualização");updates.Dock=DockStyle.Top;updates.Height=34;updates.Font=new Font(Style.FontName,8);updates.Click+=async delegate{await CheckUpdates(true);};
            var version=Style.Label("FRANKENSTEIN  0.3.3\n"+(CodexPerformanceOptimizer.Optimizer.IsAdministrator()?"Administrador":"Usuário padrão"),8,Style.Muted);version.Dock=DockStyle.Bottom;version.Height=39;foot.Controls.Add(updates);foot.Controls.Add(version);rail.Controls.Add(foot);
            var close=Style.Button("×");close.AccessibleName="Fechar aplicativo";close.SetBounds(160,9,30,30);close.Font=new Font("Segoe UI",15);close.Click+=delegate{Close();};
            var max=Style.Button("□");max.AccessibleName="Maximizar ou restaurar";max.SetBounds(124,9,30,30);max.Click+=delegate{ToggleMaximize();};
            var min=Style.Button("−");min.AccessibleName="Minimizar";min.SetBounds(88,9,30,30);min.Click+=delegate{WindowState=FormWindowState.Minimized;};
            var chrome=new Panel{Dock=DockStyle.Right,Width=202};chrome.Controls.AddRange(new Control[]{min,max,close});
            var top=new Panel{Dock=DockStyle.Top,Height=54,Padding=new Padding(28,13,0,0)};
            heading=Style.Label("BANCADA  /  VISÃO GERAL",8.5f,Style.Muted,true);heading.Dock=DockStyle.Fill;heading.MouseDown+=Drag;heading.DoubleClick+=delegate{ToggleMaximize();};top.MouseDown+=Drag;top.Controls.Add(heading);top.Controls.Add(chrome);
            var bottom=new Panel{Dock=DockStyle.Bottom,Height=34,Padding=new Padding(28,7,12,2)};status=Style.Label(Session.Status,8,Style.Muted);status.Dock=DockStyle.Fill;bottom.Controls.Add(status);
            canvas=new Panel{Dock=DockStyle.Fill,Padding=new Padding(24,8,24,12)};
            var body=new Panel{Dock=DockStyle.Fill};body.Controls.Add(canvas);body.Controls.Add(bottom);body.Controls.Add(top);Controls.Add(body);Controls.Add(rail);
        }
        void Drag(object s,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}
        void ToggleMaximize(){MaximizedBounds=Screen.FromHandle(Handle).WorkingArea;WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
        protected override void WndProc(ref Message m){if((uint)m.Msg==updateCloseMessage){RequestUpdateClose();return;}base.WndProc(ref m);if(m.Msg==0x84&&WindowState==FormWindowState.Normal){Point p=PointToClient(new Point(unchecked((short)(long)m.LParam),unchecked((short)((long)m.LParam>>16))));bool l=p.X<7,r=p.X>Width-8,t=p.Y<7,b=p.Y>Height-8;if(t&&l)m.Result=(IntPtr)13;else if(t&&r)m.Result=(IntPtr)14;else if(b&&l)m.Result=(IntPtr)16;else if(b&&r)m.Result=(IntPtr)17;else if(l)m.Result=(IntPtr)10;else if(r)m.Result=(IntPtr)11;else if(t)m.Result=(IntPtr)12;else if(b)m.Result=(IntPtr)15;}}
        internal void Navigate(string key)
        {
            section=key;heading.Text="BANCADA  /  "+key.ToUpperInvariant();
            foreach(var p in navigation){p.Value.BackColor=p.Key==key?Style.Selected:Style.Sidebar;p.Value.ForeColor=p.Key==key?Style.Mint:Style.Muted;}
            foreach(Control c in canvas.Controls.Cast<Control>().ToArray()){canvas.Controls.Remove(c);if(!(c is Form))c.Dispose();else c.Hide();}
            if(key=="Visão geral"){BuildHome();return;}if(key=="Aparência"){BuildAppearance();return;}if(key=="Histórico"){BuildHistory();return;}
            Form view;
            if(key=="Desempenho"||key=="Manutenção"||key=="Sistema"||key=="Atualizações do PC"||key=="Recuperação"){
                if(optimizer==null){IntegratedStyle.EnginePalette();optimizer=new CodexPerformanceOptimizer.MainFormV2(null,false,true,false);optimizer.AttachWorkspace(!preview);workspaces["optimizer"]=optimizer;}
                optimizer.SelectWorkspace(key=="Desempenho"?0:key=="Manutenção"?1:key=="Atualizações do PC"?2:key=="Sistema"?3:4,!preview);view=optimizer;
            }else if(!workspaces.TryGetValue(key,out view)){
                if(key=="Programas")view=new CentralInstalacaoTI.MainForm();
                else if(key=="Navegadores")view=new NavegadorSOS.MainForm();
                else view=new NativeWorkspace(key=="Ferramentas técnicas");
                view.TopLevel=false;view.FormBorderStyle=FormBorderStyle.None;view.MinimumSize=Size.Empty;view.Dock=DockStyle.Fill;workspaces[key]=view;
            }
            canvas.Controls.Add(view);IntegratedStyle.Apply(view);view.Show();
            if(view==optimizer){var tools=new FlowLayoutPanel{Dock=DockStyle.Top,Height=42};foreach(string name in new[]{"Desempenho","Manutenção","Sistema","Atualizações do PC","Recuperação"}){string destination=name;var button=Style.Button(name);button.AutoSize=true;button.Height=33;button.Font=new Font(Style.FontName,8);button.Click+=delegate{Navigate(destination);};tools.Controls.Add(button);}canvas.Controls.Add(tools);}
        }
        void BuildHome()
        {
            var page=new Panel{Dock=DockStyle.Fill,AutoScroll=true};canvas.Controls.Add(page);
            var hero=new Atmosphere{Hero=true,Dock=DockStyle.Top,Height=242,BackColor=Style.Surface,Padding=new Padding(28)};
            var tag=Style.Label("FRANKENSTEIN / WORKBENCH",8.5f,Style.Mint,true);tag.SetBounds(28,24,420,22);
            var title=Style.Label("Peças reunidas.\nPotência em conjunto.",28,Style.Text,true);title.SetBounds(25,57,570,99);
            var sub=Style.Label("Diagnostique, cuide e prepare seu computador em um só lugar.",10,Style.Muted);sub.SetBounds(28,178,580,38);hero.Controls.AddRange(new Control[]{tag,title,sub});
            var flows=new TableLayoutPanel{Dock=DockStyle.Top,Height=225,ColumnCount=3,RowCount=1,Padding=new Padding(0,20,0,8)};
            for(int i=0;i<3;i++)flows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));
            AddFlow(flows,0,"01 / ENTENDER","O que está acontecendo?","Diagnóstico do Windows, falhas de apps e relatórios para orientar o próximo passo.","Windows e apps");
            AddFlow(flows,1,"02 / CUIDAR","Mais fôlego para o PC.","Manutenção, armazenamento e inicialização com ações revisadas por você.","Manutenção");
            AddFlow(flows,2,"03 / PREPARAR","Tudo para começar.","Catálogo de programas, instalação em lote e ferramentas do seu dia a dia.","Programas");
            var activity=new Panel{Dock=DockStyle.Top,Height=170,Padding=new Padding(20),BackColor=Style.Surface};
            var a=Style.Label("ÚLTIMO ATENDIMENTO",8,Style.Mint,true);a.Dock=DockStyle.Top;a.Height=30;
            var journal=new Engine(AppDomain.CurrentDomain.BaseDirectory,Path.Combine(Distribution.DataRoot,"Execucoes"));var last=journal.History().FirstOrDefault();
            var b=Style.Label(last==null?"Sua próxima solução começa aqui.":last.Name,16,Style.Text,true);b.Dock=DockStyle.Top;b.Height=39;
            var c=Style.Label(last==null?"As operações ficam registradas em um histórico compartilhado.":last.Status+"  ·  "+last.Started,10,Style.Muted);c.Dock=DockStyle.Top;c.Height=40;activity.Controls.Add(c);activity.Controls.Add(b);activity.Controls.Add(a);
            page.Controls.Add(activity);page.Controls.Add(flows);page.Controls.Add(hero);
        }
        void AddFlow(TableLayoutPanel table,int column,string step,string title,string text,string destination)
        {
            var card=new Panel{Dock=DockStyle.Fill,BackColor=Style.Surface,Margin=new Padding(column==0?0:10,0,column==2?0:10,0),Padding=new Padding(18)};
            var label=Style.Label(step,8,Style.Mint,true);label.Dock=DockStyle.Top;label.Height=27;
            var headline=Style.Label(title,13,Style.Text,true);headline.Dock=DockStyle.Top;headline.Height=38;
            var detail=Style.Label(text,9.5f,Style.Muted);detail.Dock=DockStyle.Fill;
            var action=Style.Button("Abrir  →");action.Dock=DockStyle.Bottom;action.Height=32;action.Click+=delegate{Navigate(destination);};card.Controls.Add(detail);card.Controls.Add(headline);card.Controls.Add(label);card.Controls.Add(action);table.Controls.Add(card,column,0);
        }
        void BuildAppearance()
        {
            var list=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true};canvas.Controls.Add(list);
            var intro=Style.Label("Escolha o universo da sua bancada.",23,Style.Text,true);intro.Dock=DockStyle.Top;intro.Height=85;canvas.Controls.Add(intro);
            string[] descriptions={"Costuras, energia verde e metal escuro.","Blocos, terra e tipografia de terminal.","Órbitas neon, roxo e diagonais elétricas.","Circuitos, grade técnica e azul ciano.","Superfícies claras e detalhes em verde."};
            for(int i=0;i<Style.Themes.Length;i++){
                Palette palette=Style.Themes[i];var card=new Atmosphere{Preview=palette,Width=300,Height=208,BackColor=palette.Bg,Margin=new Padding(0,0,18,18)};
                var title=Style.Label(palette.Name,19,palette.Text,true);title.SetBounds(18,20,260,35);
                var description=Style.Label(descriptions[i],10,palette.Muted);description.SetBounds(18,71,260,55);
                var button=Style.Button(palette==Style.Current?"Tema ativo":"Usar este tema");button.BackColor=palette.Accent;button.ForeColor=palette.Bg;button.SetBounds(18,147,264,39);button.Click+=delegate{ChangeTheme(palette,true);};card.Controls.AddRange(new Control[]{title,description,button});list.Controls.Add(card);
            }
        }
        void ChangeTheme(Palette p,bool save){if(save)Style.SaveTheme(p);Style.ApplyTheme(this,p);IntegratedStyle.EnginePalette();foreach(var form in workspaces.Values)IntegratedStyle.Apply(form);Navigate(section);}
        void BuildHistory()
        {
            var grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
            grid.Columns.Add("time","Data");grid.Columns.Add("name","Operação");grid.Columns.Add("status","Resultado");grid.RowTemplate.Height=45;grid.ColumnHeadersHeight=40;
            var engine=new Engine(AppDomain.CurrentDomain.BaseDirectory,Path.Combine(Distribution.DataRoot,"Execucoes"));foreach(var run in engine.History()){int row=grid.Rows.Add(run.Started,run.Name,string.IsNullOrEmpty(run.Ended)?"Sem encerramento registrado":run.Status);grid.Rows[row].Tag=run;}
            grid.CellDoubleClick+=delegate(object s,DataGridViewCellEventArgs e){if(e.RowIndex>=0){var r=(RunRecord)grid.Rows[e.RowIndex].Tag;MessageBox.Show(this,r.Status+"\n\n"+r.Detail+"\n\n"+r.Folder,r.Name);}};IntegratedStyle.Apply(grid);canvas.Controls.Add(grid);
        }
        async Task CheckUpdates(bool manual)
        {
            if(!updates.Enabled)return;if(manual&&Session.Busy){MessageBox.Show(this,"Conclua a operação atual antes de atualizar.");return;}updates.Enabled=false;
            try
            {
                var manifest=await Task.Run(()=>Distribution.Check());
                bool available=new Version(manifest.Version)>Distribution.Current;
                updates.Text=available?"Nova versão disponível":"Verificar atualização";
                if(!manual)return;
                if(!available){MessageBox.Show(this,"Versão atual: "+Distribution.Current.ToString(3));return;}
                if(MessageBox.Show(this,manifest.Notes+"\n\nBaixar a versão "+manifest.Version+" e abrir o instalador?","Atualização",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
                if(!Session.Begin("Atualização do Frankenstein"))return;
                try
                {
                    var progress=new Progress<int>(value=>{if(!IsDisposed)status.Text="Baixando atualização: "+value+"%";});
                    string file=await Task.Run(()=>Distribution.Download(manifest,value=>((IProgress<int>)progress).Report(value)));
                    Process.Start(new ProcessStartInfo(file){UseShellExecute=true});
                    Session.End("Instalador aberto",file);Close();
                }
                catch(Exception ex){Session.End("Falha na atualização",ex.ToString());throw;}
            }
            catch(Exception ex){Program.LogException(ex);if(manual)MessageBox.Show(this,ex.Message,"Atualização não concluída");else updates.Text="Atualizações · offline";}
            finally{if(!IsDisposed){updates.Enabled=true;status.Text=Session.Status;}}
        }
        internal void PreviewTheme(Palette p,string path,Size size){Size=size;ChangeTheme(p,false);PerformLayout();Application.DoEvents();using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,ClientRectangle);bitmap.Save(path);}if(navigation["Aparência"].Bottom>updates.Parent.Top)throw new Exception("Navegação sobreposta ao rodapé.");}
    }
}
