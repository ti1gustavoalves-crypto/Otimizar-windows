using System;using System.Drawing;using System.Windows.Forms;
namespace FrankensteinTI {
    internal sealed class NxDialog : Form
    {
        TextBox server; ComboBox scope;
        public string Server { get { return server.Text.Trim(); } }
        public string Scope { get { return scope.SelectedIndex==0?"User":"Machine"; } }
        public NxDialog()
        {
            Text="Configurar licença NX"; ClientSize=new Size(500,260); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false; StartPosition=FormStartPosition.CenterParent; BackColor=Style.Bg; ForeColor=Style.Text; Font=new Font(Style.FontName,10);
            var a=Style.Label("Servidor de licença (porta@servidor)",10,Style.Text); a.SetBounds(22,18,450,25);
            server=new TextBox {Text="28000@minduri.fiemg.com.br",BackColor=Style.Surface,ForeColor=Style.Text}; server.SetBounds(22,50,450,28);
            var b=Style.Label("Aplicar em",10,Style.Text); b.SetBounds(22,95,450,25);
            scope=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Style.Surface,ForeColor=Style.Text }; scope.Items.AddRange(new object[]{"Usuário atual","Computador inteiro (administrador)"}); scope.SelectedIndex=0; scope.SetBounds(22,125,450,30);
            var ok=Style.Button("Continuar",true); ok.SetBounds(302,193,170,42); ok.Click+=delegate { if(!System.Text.RegularExpressions.Regex.IsMatch(Server,@"^[0-9]{1,5}@[a-zA-Z0-9][a-zA-Z0-9.-]*$")) { MessageBox.Show(this,"Use o formato porta@servidor.");return; } int port=int.Parse(Server.Split('@')[0]); if(port<1||port>65535){MessageBox.Show(this,"Porta deve estar entre 1 e 65535.");return;} DialogResult=DialogResult.OK; };
            var cancel=Style.Button("Cancelar"); cancel.SetBounds(118,193,170,42); cancel.DialogResult=DialogResult.Cancel; CancelButton=cancel; AcceptButton=ok;
            Controls.AddRange(new Control[]{a,server,b,scope,ok,cancel});
        }
    }
}
