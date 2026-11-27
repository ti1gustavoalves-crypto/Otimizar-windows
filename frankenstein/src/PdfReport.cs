using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
namespace FrankensteinTI
{
    internal static class PdfReport
    {
        static string Literal(string s)
        {
            var b=new StringBuilder("(");foreach(byte c in Encoding.GetEncoding(1252).GetBytes(s??"")){if(c<32||c>126||c==40||c==41||c==92)b.Append("\\"+Convert.ToString(c,8).PadLeft(3,'0'));else b.Append((char)c);}return b.Append(')').ToString();
        }
        internal static void Save(string path,string title,string text)
        {
            var lines=new List<string>();foreach(string raw in (text??"").Replace("\r","").Replace("\t","    ").Split('\n')){string line=raw;while(line.Length>86){int cut=line.LastIndexOf(' ',85,85);if(cut<20)cut=86;lines.Add(line.Substring(0,cut));line=line.Substring(cut).TrimStart();}lines.Add(line);}
            int count=Math.Max(1,(lines.Count+46)/47);var objects=new List<string>();objects.Add("<< /Type /Catalog /Pages 2 0 R >>");objects.Add("");objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>");objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
            var kids=new List<string>();for(int p=0;p<count;p++){
                int page=objects.Count+1,stream=page+1;kids.Add(page+" 0 R");
                objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents "+stream+" 0 R >>");
                var body=new StringBuilder("0.08 0.19 0.17 rg 0 778 595 64 re f\nBT /F2 18 Tf 1 1 1 rg 40 805 Td (FRANKENSTEIN / TI) Tj ET\n");
                body.Append("BT /F2 12 Tf 0.08 0.19 0.17 rg 40 755 Td "+Literal(title.Length>72?title.Substring(0,72):title)+" Tj ET\nBT /F1 9 Tf 0.12 0.14 0.16 rg 14 TL 40 730 Td\n");
                foreach(string line in lines.Skip(p*47).Take(47))body.Append(Literal(line)+" Tj T*\n");body.Append("ET\nBT /F1 8 Tf 0.35 0.4 0.4 rg 40 38 Td "+Literal(DateTime.Now.ToString("dd/MM/yyyy HH:mm")+"  |  Relatório local  |  Página "+(p+1)+" de "+count)+" Tj ET\n");
                string content=body.ToString();objects.Add("<< /Length "+Encoding.ASCII.GetByteCount(content)+" >>\nstream\n"+content+"endstream");
            }
            objects[1]="<< /Type /Pages /Count "+count+" /Kids ["+string.Join(" ",kids)+"] >>";
            using(var file=new MemoryStream()){
                Action<string> write=s=>{byte[] bytes=Encoding.ASCII.GetBytes(s);file.Write(bytes,0,bytes.Length);};write("%PDF-1.4\n");var offsets=new List<long>{0};
                for(int i=0;i<objects.Count;i++){offsets.Add(file.Position);write((i+1)+" 0 obj\n"+objects[i]+"\nendobj\n");}
                long xref=file.Position;write("xref\n0 "+offsets.Count+"\n0000000000 65535 f \n");foreach(long n in offsets.Skip(1))write(n.ToString("D10")+" 00000 n \n");write("trailer\n<< /Size "+offsets.Count+" /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF\n");
                string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,file.ToArray());if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
            }
        }
        internal static void Export(IWin32Window owner,string title,string text)
        {
            using(var dialog=new SaveFileDialog{Filter="Relatório PDF|*.pdf",FileName="Frankenstein-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".pdf"}){if(dialog.ShowDialog(owner)!=DialogResult.OK)return;try{Save(dialog.FileName,title,text);MessageBox.Show(owner,"Relatório salvo em:\n"+dialog.FileName);}catch(Exception ex){MessageBox.Show(owner,ex.Message,"Não foi possível exportar");}}
        }
    }
}
