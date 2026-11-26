using System;
using System.IO;
using System.Windows.Forms;

namespace FrankensteinTI
{
    internal static class Session
    {
        static readonly object gate=new object();
        static RunRecord active; static bool persist;
        public static bool ClosingForUpdate;
        public static event Action Changed;
        public static bool Busy { get { lock(gate)return active!=null; } }
        public static string Status { get { lock(gate)return active==null?"Tudo pronto. Sua bancada está livre.":active.Name+" · em andamento"; } }
        static Engine Journal {get{return new Engine(AppDomain.CurrentDomain.BaseDirectory,Path.Combine(Distribution.DataRoot,"Execucoes"));}}
        public static bool Begin(string name,bool notify=true,bool journal=true)
        {
            lock(gate)
            {
                if(ClosingForUpdate)return false;
                if(active!=null){if(notify)MessageBox.Show("Conclua a operação atual antes de iniciar outra: "+active.Name,"Operação em andamento");return false;}
                string id=DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
                var record=new RunRecord{Id=id,ModuleId="integrado",Name=name,Started=DateTime.Now.ToString("o"),Status="Em execução",Folder=Path.Combine(Journal.Data,id)};
                if(journal)Journal.Save(record);active=record;persist=journal;
            }
            if(Changed!=null)Changed();return true;
        }
        public static void End(string result,string report)
        {
            lock(gate)
            {
                if(active==null)return;
                try{active.Status=result;active.Detail=report;active.Ended=DateTime.Now.ToString("o");if(persist)Journal.Save(active);}
                finally{active=null;}
            }
            if(Changed!=null)Changed();
        }
    }
}
