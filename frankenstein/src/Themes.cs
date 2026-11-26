using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace FrankensteinTI
{
    internal sealed class Palette
    {
        public string Name, Font;
        public Color Bg, Surface, Border, Text, Muted, Accent, Amber, Sidebar, Selected, Hover;
        public Palette(string name,string font,params string[] colors)
        { Name=name;Font=font;Bg=C(colors[0]);Surface=C(colors[1]);Border=C(colors[2]);Text=C(colors[3]);Muted=C(colors[4]);Accent=C(colors[5]);Amber=C(colors[6]);Sidebar=C(colors[7]);Selected=C(colors[8]);Hover=C(colors[9]); }
        static Color C(string hex){return ColorTranslator.FromHtml(hex);}
    }
    internal static partial class Style
    {
        public static readonly Palette[] Themes={
            new Palette("Frankenstein","Segoe UI","#0D1319","#161F27","#2B3943","#EBF2F2","#9DAFB9","#99EDBE","#F3C97D","#111920","#264137","#22313C"),
            new Palette("Minecraft","Consolas","#172015","#263021","#4F6340","#F1F4E8","#BCC6AF","#91CE5D","#E8C570","#10180F","#3A4A2F","#34422A"),
            new Palette("Gamer","Segoe UI","#120E21","#211932","#48375F","#F5EFFF","#BDAED1","#CEACFF","#FFBF94","#191026","#493359","#322244"),
            new Palette("Tecnologia","Segoe UI","#071720","#0E2632","#285262","#E6F5FA","#9DC0CC","#67DCF0","#F2CA80","#0B1E28","#16485A","#173746"),
            new Palette("Claro","Segoe UI","#F0F4F7","#FFFFFF","#B4C2CB","#142835","#465D6B","#005B45","#765300","#E1EAF0","#C7DED5","#E9F1F4")
        };
        public static Palette Current=Themes[0];
        public static Color Sidebar {get{return Current.Sidebar;}}
        public static Color Selected {get{return Current.Selected;}}
        public static Color Hover {get{return Current.Hover;}}
        public static string FontName {get{return Current.Font;}}
        public static string PreferencesPath {get{return Path.Combine(Distribution.DataRoot,"interface.json");}}
        public static void LoadTheme()
        {
            string name="Frankenstein";
            try { if(File.Exists(PreferencesPath)) { var p=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(PreferencesPath)); if(p.ContainsKey("Theme"))name=p["Theme"]; } } catch {}
            foreach(Palette p in Themes)if(p.Name==name){Set(p);return;}
            Set(Themes[0]);
        }
        public static void SaveTheme(Palette palette)
        {
            Directory.CreateDirectory(Distribution.DataRoot);
            string temp=PreferencesPath+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(new Dictionary<string,string>{{"Theme",palette.Name}}));
            if(File.Exists(PreferencesPath))File.Replace(temp,PreferencesPath,null);else File.Move(temp,PreferencesPath);
        }
        static void Set(Palette p){Current=p;Bg=p.Bg;Surface=p.Surface;Border=p.Border;Text=p.Text;Muted=p.Muted;Mint=p.Accent;Amber=p.Amber;}
        public static void ApplyTheme(Control root,Palette palette)
        {
            Palette before=Current;Set(palette);ApplyControl(root,before,palette);root.Invalidate(true);
        }
        static Color Translate(Color c,Palette a,Palette b)
        {
            Color[] from={a.Bg,a.Surface,a.Border,a.Text,a.Muted,a.Accent,a.Amber,a.Sidebar,a.Selected,a.Hover};
            Color[] to={b.Bg,b.Surface,b.Border,b.Text,b.Muted,b.Accent,b.Amber,b.Sidebar,b.Selected,b.Hover};
            for(int i=0;i<from.Length;i++)if(c.ToArgb()==from[i].ToArgb())return to[i];return c;
        }
        static void ApplyControl(Control c,Palette before,Palette after)
        {
            c.BackColor=Translate(c.BackColor,before,after);c.ForeColor=Translate(c.ForeColor,before,after);
            if(c.Font.FontFamily.Name==before.Font)c.Font=new Font(after.Font,c.Font.Size,c.Font.Style);
            Button button=c as Button;if(button!=null){button.FlatAppearance.BorderColor=after.Border;button.FlatAppearance.MouseOverBackColor=button.BackColor==after.Accent?after.Accent:after.Hover;}
            DataGridView grid=c as DataGridView;if(grid!=null){grid.BackgroundColor=after.Bg;grid.GridColor=after.Border;grid.DefaultCellStyle.BackColor=after.Surface;grid.DefaultCellStyle.ForeColor=after.Text;grid.DefaultCellStyle.SelectionBackColor=after.Selected;grid.DefaultCellStyle.SelectionForeColor=after.Text;grid.ColumnHeadersDefaultCellStyle.BackColor=after.Surface;grid.ColumnHeadersDefaultCellStyle.ForeColor=after.Accent;}
            foreach(Control child in c.Controls)ApplyControl(child,before,after);
        }
    }
}
