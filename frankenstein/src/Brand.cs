using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FrankensteinTI
{
    internal static class Brand
    {
        public static void Draw(Graphics g,Rectangle r,Color accent,Color ink)
        {
            var state=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(r.Width/100f,r.Height/100f);g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var b=new SolidBrush(accent)){
                g.FillPolygon(b,new[]{new Point(18,8),new Point(76,8),new Point(88,20),new Point(88,39),new Point(57,39),new Point(57,52),new Point(77,52),new Point(77,70),new Point(43,70),new Point(43,92),new Point(18,92)});
                g.FillRectangle(b,5,33,9,18);g.FillRectangle(b,89,33,7,18);
            }
            using(var p=new Pen(ink,4)){g.DrawLine(p,25,26,73,26);g.DrawLine(p,35,20,35,32);g.DrawLine(p,48,20,48,32);g.DrawLine(p,61,20,61,32);}
            using(var b=new SolidBrush(ink)){g.FillRectangle(b,27,43,9,9);g.FillPolygon(b,new[]{new Point(47,43),new Point(37,62),new Point(46,62),new Point(33,83),new Point(58,56),new Point(47,56)});}
            g.Restore(state);
        }
    }
    internal sealed class Atmosphere : Panel
    {
        public bool Hero;
        public Palette Preview;
        public Atmosphere(){DoubleBuffered=true;ResizeRedraw=true;}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var p=Preview??Style.Current;var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            int start=Hero?Math.Max(Width*3/5,380):0;
            using(var pen=new Pen(Color.FromArgb(45,p.Accent),1))
            using(var fill=new SolidBrush(Color.FromArgb(24,p.Accent)))
            {
                if(p.Name=="Minecraft"){
                    for(int x=start;x<Width;x+=28)for(int y=0;y<Height;y+=28)if((x/28+y/28)%3==0)g.FillRectangle(fill,x,y,24,24);
                    using(var earth=new SolidBrush(Color.FromArgb(65,130,91,54)))g.FillRectangle(earth,0,Height-12,Width,12);
                    using(var grass=new SolidBrush(p.Accent))g.FillRectangle(grass,0,Height-15,Width,3);
                }else if(p.Name=="Gamer"){
                    for(int x=start-Height;x<Width;x+=48)g.DrawLine(pen,x,Height,x+Height,0);
                    using(var glow=new Pen(Color.FromArgb(90,p.Accent),3))g.DrawEllipse(glow,Width-215,-45,245,245);
                    using(var glow=new Pen(Color.FromArgb(80,255,88,178),2))g.DrawEllipse(glow,Width-180,-10,175,175);
                }else if(p.Name=="Tecnologia"){
                    for(int x=start;x<Width;x+=36)g.DrawLine(pen,x,0,x,Height);
                    for(int y=0;y<Height;y+=36)g.DrawLine(pen,start,y,Width,y);
                    for(int x=start+18;x<Width;x+=72){g.DrawLines(pen,new[]{new Point(x,20),new Point(x,65),new Point(x+32,97),new Point(x+32,Height)});g.FillEllipse(fill,x-5,15,10,10);}
                }else if(p.Name=="Claro"){
                    for(int x=start;x<Width;x+=24)for(int y=12;y<Height;y+=24)g.FillEllipse(fill,x,y,3,3);
                    g.DrawEllipse(pen,Width-170,-70,260,260);
                }else{
                    for(int y=18;y<Height;y+=32){g.DrawLine(pen,Width-100,y,Width-72,y+14);g.DrawLine(pen,Width-72,y,Width-100,y+14);}
                    g.DrawLine(pen,Width-86,0,Width-86,Height);
                    for(int x=start;x<Width-110;x+=40)g.DrawEllipse(pen,x,Height-48,5,5);
                }
            }
            if(Hero)Brand.Draw(g,new Rectangle(Width-260,45,110,110),p.Accent,p.Bg);
        }
    }
}
