using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace FrankensteinSetup
{
    internal static class AppShutdown
    {
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll",SetLastError=true)] static extern bool PostMessage(IntPtr hwnd,uint message,IntPtr w,IntPtr l);
        internal const string MessageName="FrankensteinTI.CloseForUpdate.v1";
        internal static bool Matches(string executable,string expected,int session,int currentSession)
        {return session==currentSession&&string.Equals(Path.GetFullPath(executable),Path.GetFullPath(expected),StringComparison.OrdinalIgnoreCase);}
        // The caller owns this mutex on the calling thread and must release it after deployment.
        public static Mutex Acquire(string root,Action<string> status,string mutexName="Local\\FrankensteinTI-Central",int timeoutMilliseconds=60000)
        {
            var gate=new Mutex(false,mutexName);
            bool owns=false;var closing=new List<int>();
            try
            {
                string expected=Path.Combine(root,"App","FrankensteinTI.exe");
                bool requested=false;var clock=Stopwatch.StartNew();
                while(true)
                {
                    bool acquired;try{acquired=gate.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
                    if(acquired)
                    {
                        owns=true;
                        foreach(int id in closing)try{using(var process=Process.GetProcessById(id))if(!process.WaitForExit(5000))throw new IOException("O aplicativo ainda está encerrando. Tente atualizar novamente.");}catch(ArgumentException){}
                        return gate;
                    }
                    if(!requested)
                    {
                        status("Fechando o Frankenstein para atualizar. Aguardando a conclusão de operações em andamento...");
                        foreach(var process in Process.GetProcessesByName("FrankensteinTI"))using(process)
                        {
                            try
                            {
                                if(process.HasExited)continue;
                                if(!Matches(process.MainModule.FileName,expected,process.SessionId,Process.GetCurrentProcess().SessionId))continue;
                                var window=process.MainWindowHandle;
                                if(window==IntPtr.Zero)continue;
                                closing.Add(process.Id);
                                Version version;string value=process.MainModule.FileVersionInfo.FileVersion;
                                if(Version.TryParse(value,out version)&&version>=new Version(0,3,1,0))
                                {
                                    if(!PostMessage(window,RegisterWindowMessage(MessageName),IntPtr.Zero,IntPtr.Zero))throw new IOException("O Windows recusou o pedido para fechar o aplicativo. Se ele estiver elevado, execute também o instalador como administrador.");
                                }
                                else if(!process.CloseMainWindow())throw new IOException("Não foi possível solicitar o fechamento do aplicativo.");
                            }
                            catch(InvalidOperationException){/* Process exited during inspection. */}
                            catch(System.ComponentModel.Win32Exception e){throw new IOException("Não foi possível acessar o aplicativo aberto. Se ele estiver elevado, execute o instalador como administrador.",e);}
                        }
                        requested=true;
                    }
                    if(clock.ElapsedMilliseconds>=timeoutMilliseconds)throw new IOException("O aplicativo ainda está aberto ou ocupado. Conclua a operação em andamento e clique em Instalar / atualizar novamente. Nenhum arquivo foi substituído.");
                    Thread.Sleep(200);
                }
            }
            catch{if(owns)gate.ReleaseMutex();gate.Dispose();throw;}
        }
    }
}
