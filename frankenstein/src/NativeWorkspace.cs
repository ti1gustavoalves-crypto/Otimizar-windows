using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FrankensteinTI
{
    internal sealed class NativeWorkspace : Form
    {
        readonly Engine engine=new Engine(AppDomain.CurrentDomain.BaseDirectory,Path.Combine(Distribution.DataRoot,"Execucoes"));
        readonly ListBox choices=new ListBox{Dock=DockStyle.Left,Width=255,BorderStyle=BorderStyle.None,ItemHeight=38,DrawMode=DrawMode.OwnerDrawFixed};
        Label title;TextBox detail;Button run;Module selected;
        public NativeWorkspace(bool technical)
        {
            BackColor=Style.Bg;var all=Catalog.All().Where(m=>m.Native&&(technical?m.Group=="Softwares técnicos":m.Group!="Softwares técnicos")).ToList();
            choices.DisplayMember="Name";foreach(var item in all)choices.Items.Add(item);
            choices.DrawItem+=delegate(object sender,DrawItemEventArgs e){if(e.Index<0)return;bool active=(e.State&DrawItemState.Selected)!=0;using(var b=new SolidBrush(active?Style.Selected:Style.Bg))e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,((Module)choices.Items[e.Index]).Name,new Font(Style.FontName,9.5f),new Rectangle(e.Bounds.X+12,e.Bounds.Y+10,e.Bounds.Width-20,e.Bounds.Height-10),active?Style.Mint:Style.Text,TextFormatFlags.EndEllipsis);};
            var body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(24)};title=Style.Label("",21,Style.Text,true);title.Dock=DockStyle.Top;title.Height=82;
            detail=new TextBox{Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,BorderStyle=BorderStyle.None,ScrollBars=ScrollBars.Vertical,BackColor=Style.Bg,ForeColor=Style.Muted,Font=new Font(Style.FontName,11)};
            run=Style.Button("Iniciar",true);run.Dock=DockStyle.Bottom;run.Click+=async delegate{await Execute();};body.Controls.Add(detail);body.Controls.Add(title);body.Controls.Add(run);Controls.Add(body);Controls.Add(choices);
            choices.SelectedIndexChanged+=delegate{selected=(Module)choices.SelectedItem;title.Text=selected.Name;detail.Text=selected.Details;run.Text=selected.Changes?"Revisar e executar":"Executar diagnóstico";};if(all.Count>0)choices.SelectedIndex=0;
        }
        async System.Threading.Tasks.Task Execute()
        {
            if(selected==null||Session.Busy)return;var module=selected;var request=new Dictionary<string,object>();bool started=false;
            try{
                if(module.Mode=="NxSet")using(var dialog=new NxDialog()){if(dialog.ShowDialog(this)!=DialogResult.OK)return;request["Server"]=dialog.Server;request["Scope"]=dialog.Scope;module.Admin=dialog.Scope=="Machine";}
                if(module.Mode=="NxRestore")using(var picker=new OpenFileDialog{Title="Selecione nx-backup.json",Filter="Backup NX|nx-backup.json",InitialDirectory=engine.Data}){if(picker.ShowDialog(this)!=DialogResult.OK)return;request["Backup"]=picker.FileName;var data=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(picker.FileName));module.Admin=Convert.ToString(data["Scope"])=="Machine";}
                if(module.Mode=="LabX")using(var picker=new OpenFileDialog{Filter="Instalador LabX|LabXBundle.exe",CheckFileExists=true}){if(picker.ShowDialog(this)!=DialogResult.OK)return;request["Installer"]=picker.FileName;request["InstallerHash"]=Engine.Hash(picker.FileName);}
                if(module.Changes&&MessageBox.Show(this,module.Details+"\n\nDeseja executar?","Revisar operação",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
                if(!Session.Begin(module.Name,true,false))return;started=true;run.Enabled=false;choices.Enabled=false;
                var result=await engine.Execute(module,request,s=>detail.Text=s);detail.Text=result.Status+"\r\n\r\n"+result.Detail+"\r\n\r\nRelatórios: "+result.Folder;
                Session.End(result.Status,result.Folder);
            }catch(Exception e){detail.Text="Falha: "+e.Message;if(started)Session.End("Falha",e.Message);}
            finally{run.Enabled=true;choices.Enabled=true;}
        }
    }
}
