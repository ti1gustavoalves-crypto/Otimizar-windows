using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace FrankensteinTI
{
    internal sealed class OperationView
    {
        public string Id,Name,Status,Detail;public int? Percent;public bool CanCancel;
    }
    internal static class Session
    {
        sealed class Work { public RunRecord Record;public bool Persist;public TaskCompletionSource<bool> Ready;public Action Cancel;public int? Percent; }
        static readonly object gate=new object();
        static Work active;static readonly List<Work> waiting=new List<Work>();static readonly List<OperationView> finished=new List<OperationView>();static bool closing;
        public static event Action Changed;
        public static bool ClosingForUpdate {get{lock(gate)return closing;}set{lock(gate)closing=value;if(value)CancelPending();}}
        public static bool Busy {get{lock(gate)return active!=null;}}
        public static int PendingCount {get{lock(gate)return waiting.Count;}}
        public static string Status {get{lock(gate)return active==null?"Tudo pronto. Sua bancada está livre.":active.Record.Name+" · "+(active.Percent.HasValue?active.Percent+"%":"em andamento")+(waiting.Count>0?" · "+waiting.Count+" na fila":"");}}
        static Engine Journal {get{return new Engine(AppDomain.CurrentDomain.BaseDirectory,Path.Combine(Distribution.DataRoot,"Execucoes"));}}
        static Work Create(string name,bool persist){string id=DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);return new Work{Persist=persist,Record=new RunRecord{Id=id,ModuleId="integrado",Name=name,Started=DateTime.Now.ToString("o"),Status="Na fila",Folder=Path.Combine(Journal.Data,id)}};}
        static void Save(Work w){if(w.Persist)Journal.Save(w.Record);}
        static void Notify(){var handler=Changed;if(handler!=null)foreach(Action callback in handler.GetInvocationList())try{callback();}catch(Exception ex){Program.LogException(ex);}}
        static void Start(Work w){w.Record.Status="Em execução";Save(w);active=w;}
        public static bool Begin(string name,bool notify=true,bool journal=true)
        {
            lock(gate){if(closing)return false;if(active!=null||waiting.Count>0){if(notify)MessageBox.Show("Há trabalho em andamento. Use a fila de tarefas.");return false;}Start(Create(name,journal));}
            Notify();return true;
        }
        public static Task<bool> AcquireAsync(string name,bool journal=true)
        {
            Task<bool> result;
            lock(gate){if(closing)return Task.FromResult(false);var work=Create(name,journal);work.Ready=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);if(active==null){Start(work);work.Ready.SetResult(true);}else{Save(work);waiting.Add(work);}result=work.Ready.Task;}
            Notify();return result;
        }
        public static void SetCancellation(Action cancel){lock(gate){if(active!=null)active.Cancel=cancel;}Notify();}
        public static void Report(string detail,int? percent=null){lock(gate){if(active==null)return;active.Record.Detail=detail;if(percent.HasValue)active.Percent=Math.Max(0,Math.Min(100,percent.Value));}Notify();}
        static OperationView View(Work w){return new OperationView{Id=w.Record.Id,Name=w.Record.Name,Status=w.Record.Status,Detail=w.Record.Detail,Percent=w.Percent,CanCancel=string.IsNullOrEmpty(w.Record.Ended)&&(w!=active||w.Cancel!=null)};}
        public static List<OperationView> Snapshot(){lock(gate){var rows=new List<OperationView>();if(active!=null)rows.Add(View(active));rows.AddRange(waiting.Select(View));rows.AddRange(finished);return rows;}}
        public static bool Cancel(string id)
        {
            Action cancel=null;
            lock(gate){var pending=waiting.FirstOrDefault(w=>w.Record.Id==id);if(pending!=null){pending.Record.Status="Cancelado antes de iniciar";pending.Record.Ended=DateTime.Now.ToString("o");try{Save(pending);}catch(Exception ex){pending.Record.Detail="Cancelamento não gravado no histórico: "+ex.Message;Program.LogException(ex);}waiting.Remove(pending);finished.Insert(0,View(pending));if(finished.Count>100)finished.RemoveAt(100);pending.Ready.SetResult(false);}else if(active!=null&&active.Record.Id==id&&active.Cancel!=null){cancel=active.Cancel;active.Record.Detail="Cancelamento solicitado; aguardando etapa segura.";active.Cancel=null;}else return false;}
            if(cancel!=null)cancel();Notify();return true;
        }
        static void CancelPending(){string[] ids;lock(gate)ids=waiting.Select(w=>w.Record.Id).ToArray();foreach(string id in ids)Cancel(id);}
        public static void End(string result,string report)
        {
            lock(gate){if(active==null)return;var work=active;work.Record.Status=result;work.Record.Detail=report;work.Record.Ended=DateTime.Now.ToString("o");try{Save(work);}catch(Exception ex){work.Record.Status="Falha ao salvar histórico";work.Record.Detail=result+"; "+ex.Message;Program.LogException(ex);}finally{work.Cancel=null;var view=View(work);view.CanCancel=false;finished.Insert(0,view);if(finished.Count>100)finished.RemoveAt(100);active=null;}
                while(!closing&&active==null&&waiting.Count>0){var next=waiting[0];waiting.RemoveAt(0);try{Start(next);next.Ready.SetResult(true);}catch(Exception ex){next.Ready.SetException(ex);}}
            }
            Notify();
        }
    }
}
