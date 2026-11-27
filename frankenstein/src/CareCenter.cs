using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace FrankensteinTI
{
    internal sealed class CareCenter : Form
    {
        readonly Action<string> navigate;readonly WorkspaceTabs tabs=new WorkspaceTabs{Dock=DockStyle.Fill};
        readonly DataGridView tasks=Grid("Operação","Estado","Progresso"),history=Grid("Data","Operação","Resultado"),diagnosis=Grid("Prioridade","Verificação","Evidência"),recovery=Grid("Origem","Registro","Recuperação");
        readonly TextBox details=new TextBox{Dock=DockStyle.Bottom,Height=155,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.FixedSingle};
        readonly Label summary=Style.Label("Inicie um diagnóstico ou acompanhe seus atendimentos.",10,Style.Muted);
        readonly Button scan=Style.Button("Analisar computador",true),action=Style.Button("Abrir orientação"),export=Style.Button("Exportar PDF"),refresh=Style.Button("Atualizar lista");
        List<Finding> findings=new List<Finding>();bool scanning;string diagnosedAt="";
        sealed class RecoveryEntry {public string Source,Path,Advice,Route;public bool Proxy;}
        public CareCenter(Action<string> navigate)
        {
            this.navigate=navigate;var toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=48};foreach(var b in new[]{scan,action,export,refresh}){b.AutoSize=true;b.Height=37;toolbar.Controls.Add(b);}
            AddTab("Diagnóstico geral",diagnosis);AddTab("Fila de tarefas",tasks);AddTab("Histórico",history);AddTab("Recuperação",recovery);
            summary.Dock=DockStyle.Top;summary.Height=42;Controls.Add(tabs);Controls.Add(details);Controls.Add(summary);Controls.Add(toolbar);
            tabs.SelectedIndexChanged+=delegate{RefreshDetails();RefreshActions();};
            scan.Click+=async delegate{await Scan();};refresh.Click+=delegate{LoadHistory();LoadRecovery();LoadTasks();};
            action.Click+=async delegate{try{await Act();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Operação não concluída");}};
            export.Click+=delegate{PdfReport.Export(this,tabs.SelectedTab.Text,ReportText());};
            diagnosis.SelectionChanged+=delegate{var f=Selected<Finding>(diagnosis);details.Text=f==null?"":f.ToString();RefreshActions();};
            history.SelectionChanged+=delegate{var r=Selected<RunRecord>(history);details.Text=r==null?"":r.Name+"\r\n"+r.Status+"\r\n"+r.Detail+"\r\n"+r.Folder;};
            recovery.SelectionChanged+=delegate{var r=Selected<RecoveryEntry>(recovery);details.Text=r==null?"":r.Advice+"\r\n\r\n"+r.Path;RefreshActions();};
            tasks.SelectionChanged+=delegate{var r=Selected<OperationView>(tasks);details.Text=r==null?"":r.Detail;RefreshActions();};
            Shown+=delegate{RefreshActions();};Session.Changed+=SessionChanged;Disposed+=delegate{Session.Changed-=SessionChanged;};LoadHistory();LoadRecovery();LoadTasks();RefreshActions();
        }
        internal void SelectDiagnosis(){tabs.SelectedIndex=0;RefreshActions();}
        internal void SelectTasks(){tabs.SelectedIndex=1;RefreshActions();}
        internal void ShowFindings(List<Finding> rows,string when){findings=rows;diagnosedAt=when;diagnosis.Rows.Clear();foreach(var f in rows){int i=diagnosis.Rows.Add(f.Level,f.Title,f.Evidence);diagnosis.Rows[i].Tag=f;}RefreshDetails();RefreshActions();}
        static DataGridView Grid(params string[] titles){var g=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=Style.Surface,BorderStyle=BorderStyle.None,ColumnHeadersHeight=40};g.RowTemplate.Height=42;foreach(string t in titles)g.Columns.Add(t,t);return g;}
        void AddTab(string title,Control grid){tabs.AddPage(title,grid);}
        void RefreshDetails(){details.Clear();if(tabs.SelectedIndex==0){var f=Selected<Finding>(diagnosis);if(f!=null)details.Text=f.ToString();}if(tabs.SelectedIndex==1){var r=Selected<OperationView>(tasks);if(r!=null)details.Text=r.Detail;}if(tabs.SelectedIndex==2){var r=Selected<RunRecord>(history);if(r!=null)details.Text=DescribeRun(r);}if(tabs.SelectedIndex==3){var r=Selected<RecoveryEntry>(recovery);if(r!=null)details.Text=r.Advice+"\r\n"+r.Path;}}
        static T Selected<T>(DataGridView grid) where T:class {return grid.CurrentRow==null?null:grid.CurrentRow.Tag as T;}
        void SessionChanged(){if(IsDisposed||!IsHandleCreated)return;if(InvokeRequired){BeginInvoke((Action)SessionChanged);return;}LoadTasks();if(!Session.Busy)LoadHistory();}
        void LoadTasks(){string selected=Selected<OperationView>(tasks)==null?null:Selected<OperationView>(tasks).Id;tasks.Rows.Clear();foreach(var r in Session.Snapshot()){int i=tasks.Rows.Add(r.Name,r.Status,r.Percent.HasValue?r.Percent+"%":"—");tasks.Rows[i].Tag=r;if(r.Id==selected)tasks.CurrentCell=tasks.Rows[i].Cells[0];}RefreshActions();}
        void LoadHistory(){history.Rows.Clear();foreach(var r in new Engine(AppDomain.CurrentDomain.BaseDirectory,Path.Combine(Distribution.DataRoot,"Execucoes")).History()){int i=history.Rows.Add(r.Started,r.Name,string.IsNullOrEmpty(r.Ended)?"Sem encerramento registrado":r.Status);history.Rows[i].Tag=r;}}
        internal static IEnumerable<string> RecoveryFiles(string root)
        {
            if(!Directory.Exists(root)||(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)yield break;
            var pending=new Queue<Tuple<string,int>>();pending.Enqueue(Tuple.Create(root,0));int visited=0;
            while(pending.Count>0&&visited++<2000){var item=pending.Dequeue();string[] files,dirs;try{files=Directory.GetFiles(item.Item1);dirs=item.Item2<6?Directory.GetDirectories(item.Item1):new string[0];}catch(UnauthorizedAccessException){continue;}catch(IOException){continue;}
                foreach(string f in files)if((File.GetAttributes(f)&FileAttributes.ReparsePoint)==0)yield return f;
                foreach(string d in dirs)if((File.GetAttributes(d)&FileAttributes.ReparsePoint)==0)pending.Enqueue(Tuple.Create(d,item.Item2+1));
            }
        }
        void LoadRecovery()
        {
            recovery.Rows.Clear();var entries=new List<RecoveryEntry>();
            entries.Add(new RecoveryEntry{Source="Otimizador",Path=Path.Combine(Distribution.DataRoot,"Otimizador"),Advice="Abra a recuperação do otimizador para revisar backups de configurações, drivers e quarentena. Limpeza definitiva e instalações de terceiros não têm desfazer universal.",Route="Recuperação"});
            foreach(string root in new[]{"Execucoes","Navegadores","Protecao","RecuperacaoProxy"})foreach(string file in RecoveryFiles(Path.Combine(Distribution.DataRoot,root)).Take(500)){
                string name=Path.GetFileName(file);RecoveryEntry e=null;
                if(root=="RecuperacaoProxy"&&file.EndsWith(".json")){try{var b=ProxyRecovery.Read(file);e=new RecoveryEntry{Source="Proxy · "+b.State,Proxy=b.State=="Aplicado",Advice="Restaura somente os quatro valores do proxy do usuário. Exige o mesmo computador/usuário e bloqueia se houver alteração posterior. Cache DNS não pode ser desfeito.\r\n"+string.Join("\r\n",b.Values.Select(v=>v.Name+": "+(v.Exists?v.Value:"ausente")))};}catch(Exception ex){e=new RecoveryEntry{Source="Backup inválido",Advice=ex.Message};}}
                else if(name=="nx-backup.json")e=new RecoveryEntry{Source="NX",Route="Ferramentas técnicas",Advice="Abra NX e LabX, escolha Restaurar configuração NX e selecione este arquivo. A rotina confere os valores restaurados."};
                else if(name=="COMO_RESTAURAR.txt"||name=="alteracoes.txt")e=new RecoveryEntry{Source="Navegadores",Advice="Recuperação manual: consulte este registro para reverter as alterações indicadas. Feche os navegadores. Revise cada entrada; não importe indiscriminadamente todo o Registro."};
                else if(root=="Protecao"&&file.EndsWith(".txt"))e=new RecoveryEntry{Source="Ponto de restauração",Advice="Registro de um ponto confirmado antes de uma operação. Abra a Restauração do Sistema do Windows (rstrui.exe) e confira os pontos ainda disponíveis. A restauração pode afetar outras alterações e exige reinicialização."};
                if(e!=null){e.Path=file;entries.Add(e);}
            }
            foreach(var e in entries){int i=recovery.Rows.Add(e.Source,Path.GetFileName(e.Path),e.Proxy?"Desfazer proxy":e.Route!=null?"Abrir ferramenta":"Orientação manual");recovery.Rows[i].Tag=e;}
        }
        void RefreshActions()
        {
            scan.Enabled=!scanning;action.Enabled=false;
            if(tabs.SelectedIndex==0){action.Text="Revisar recomendação";action.Enabled=Selected<Finding>(diagnosis)!=null;summary.Text=findings.Count==0?"Diagnóstico somente leitura. Nenhuma correção é aplicada automaticamente.":findings.Count+" verificações · "+diagnosedAt;}
            if(tabs.SelectedIndex==1){var r=Selected<OperationView>(tasks);action.Text="Cancelar tarefa";action.Enabled=r!=null&&r.CanCancel;summary.Text=Session.Status+" Cancelar fica disponível somente em etapas seguras.";}
            if(tabs.SelectedIndex==2){action.Text="Exportar selecionado";action.Enabled=Selected<RunRecord>(history)!=null;summary.Text=history.Rows.Count==0?"Nenhum atendimento registrado ainda.":"Selecione um atendimento para consultar o resultado. Exporte o histórico em PDF.";}
            if(tabs.SelectedIndex==3){var r=Selected<RecoveryEntry>(recovery);action.Text=r!=null&&r.Proxy?"Desfazer proxy":r!=null&&r.Route!=null?"Abrir recuperação":"Abrir pasta do registro";action.Enabled=r!=null;summary.Text="Cada backup informa como recuperar. Algumas alterações não possuem reversão direta.";}
        }
        async Task Scan()
        {
            if(scanning)return;scanning=true;RefreshActions();tabs.SelectedIndex=0;bool acquired=false;using(var cancel=new CancellationTokenSource())try{
                acquired=await Session.AcquireAsync("Diagnóstico geral");if(!acquired)return;Session.SetCancellation(cancel.Cancel);
                var result=await Task.Run(()=>Diagnostics.Scan(cancel.Token));ShowFindings(result,DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                string folder=Path.Combine(Distribution.DataRoot,"Diagnosticos");Directory.CreateDirectory(folder);string path=Path.Combine(folder,DateTime.Now.ToString("yyyyMMdd-HHmmss")+".txt");File.WriteAllText(path,DiagnosticText());Session.End("Diagnóstico concluído",path);acquired=false;
            }catch(OperationCanceledException){if(acquired){Session.End("Cancelado","Diagnóstico interrompido; nenhum reparo aplicado.");acquired=false;}}
            catch(Exception ex){if(acquired){Session.End("Falha",ex.Message);acquired=false;}MessageBox.Show(this,ex.Message,"Diagnóstico não concluído");}
            finally{if(acquired)Session.End("Interrompido","");scanning=false;RefreshActions();LoadHistory();}
        }
        async Task Act()
        {
            if(tabs.SelectedIndex==0){var f=Selected<Finding>(diagnosis);if(f!=null&&MessageBox.Show(this,f.ToString()+"\n\nAbrir a ferramenta para revisar as opções? Nenhum reparo será iniciado por este botão.","Recomendação",MessageBoxButtons.OKCancel)==DialogResult.OK)navigate(f.Route);}
            else if(tabs.SelectedIndex==1){var r=Selected<OperationView>(tasks);if(r!=null)Session.Cancel(r.Id);}
            else if(tabs.SelectedIndex==2){var r=Selected<RunRecord>(history);if(r!=null)PdfReport.Export(this,r.Name,DescribeRun(r));}
            else if(tabs.SelectedIndex==3){var r=Selected<RecoveryEntry>(recovery);if(r==null)return;if(r.Proxy){if(MessageBox.Show(this,r.Advice+"\n\nRestaurar estes valores?","Revisar restauração de proxy",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;action.Enabled=false;bool acquired=false;try{acquired=await Session.AcquireAsync("Restaurar proxy do usuário");if(!acquired)return;string result=await Task.Run(()=>ProxyRecovery.Restore(r.Path));Session.End("Restaurado",r.Path);acquired=false;MessageBox.Show(this,result);}catch(Exception ex){if(acquired){Session.End("Falha na restauração",ex.Message);acquired=false;}throw;}finally{LoadRecovery();RefreshActions();}}
                else if(r.Route!=null)navigate(r.Route);else{string directory=Directory.Exists(r.Path)?r.Path:Path.GetDirectoryName(r.Path);if(Directory.Exists(directory))Process.Start(new ProcessStartInfo("explorer.exe","\""+directory+"\""){UseShellExecute=true});}}
        }
        string DiagnosticText(){return "DIAGNÓSTICO GERAL - "+diagnosedAt+"\r\nSomente leitura; recomendações exigem revisão.\r\n\r\n"+string.Join("\r\n\r\n",findings.Select(f=>f.ToString()));}
        internal static string DescribeRun(RunRecord r){string text=r.Started+" | "+r.Name+"\r\n"+r.Status+"\r\n"+r.Detail;try{string prefix=Path.GetFullPath(Distribution.DataRoot).TrimEnd('\\')+"\\";if(!string.IsNullOrEmpty(r.Detail)&&r.Detail.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&File.Exists(r.Detail)&&new[]{".txt",".log"}.Contains(Path.GetExtension(r.Detail).ToLowerInvariant())){string path=Engine.SafePath(Distribution.DataRoot,r.Detail.Substring(prefix.Length));if(new FileInfo(path).Length<=512000)text+="\r\n\r\n"+File.ReadAllText(path);else text+="\r\nRelatório acima de 500 KB: consulte o arquivo original.";}}catch(Exception ex){text+="\r\nNão foi possível anexar o relatório: "+ex.Message;}return text;}
        string ReportText(){if(tabs.SelectedIndex==0)return DiagnosticText();if(tabs.SelectedIndex==2)return string.Join("\r\n\r\n",history.Rows.Cast<DataGridViewRow>().Select(r=>(RunRecord)r.Tag).Select(DescribeRun));if(tabs.SelectedIndex==1)return string.Join("\r\n\r\n",Session.Snapshot().Select(r=>r.Name+" | "+r.Status+"\r\n"+r.Detail));return string.Join("\r\n\r\n",recovery.Rows.Cast<DataGridViewRow>().Select(r=>(RecoveryEntry)r.Tag).Select(r=>r.Source+"\r\n"+r.Path+"\r\n"+r.Advice));}
    }
}
