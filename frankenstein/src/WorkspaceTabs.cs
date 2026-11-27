using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace FrankensteinTI
{
    internal sealed class WorkspaceTabs : Panel
    {
        readonly FlowLayoutPanel bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=43,WrapContents=false};
        readonly List<Panel> pages=new List<Panel>();readonly List<Button> buttons=new List<Button>();int selected=-1;
        public event EventHandler SelectedIndexChanged;
        public int SelectedIndex {get{return selected;}set{if(value<0||value>=pages.Count)return;selected=value;for(int i=0;i<pages.Count;i++)pages[i].Visible=i==selected;Colors();var change=SelectedIndexChanged;if(change!=null)change(this,EventArgs.Empty);}}
        public Panel SelectedTab {get{return selected<0?null:pages[selected];}}
        public WorkspaceTabs(){Controls.Add(bar);}
        public void AddPage(string title,Control content){int index=pages.Count;var page=new Panel{Dock=DockStyle.Fill,Padding=new Padding(0,6,0,0),Text=title,Visible=false};page.Controls.Add(content);pages.Add(page);Controls.Add(page);bar.SendToBack();var b=Style.Button(title);b.Width=156;b.Height=35;b.Margin=new Padding(0,0,8,0);b.Click+=delegate{SelectedIndex=index;};buttons.Add(b);bar.Controls.Add(b);if(selected<0)SelectedIndex=0;}
        void Colors(){for(int i=0;i<buttons.Count;i++){buttons[i].BackColor=i==selected?Style.Selected:Style.Surface;buttons[i].ForeColor=i==selected?Style.Mint:Style.Text;buttons[i].FlatAppearance.BorderColor=i==selected?Style.Mint:Style.Border;}}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Colors();}
    }
}
