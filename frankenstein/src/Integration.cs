using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FrankensteinTI
{
    internal static class IntegratedStyle
    {
        public static void Apply(Control c)
        {
            c.Font=new Font(Style.FontName,c.Font.Size,c.Font.Style);
            c.BackColor=c is TextBoxBase || c is ListControl || c is TabPage || c is CodexPerformanceOptimizer.DashboardPanel?Style.Surface:Style.Bg;
            c.ForeColor=Style.Text;
            var button=c as Button;
            if(button!=null){button.FlatStyle=FlatStyle.Flat;button.BackColor=Style.Surface;button.FlatAppearance.BorderColor=Style.Border;button.FlatAppearance.MouseOverBackColor=Style.Hover;}
            var modern=c as CodexPerformanceOptimizer.ModernButton;
            if(modern!=null){modern.BaseColor=Style.Surface;modern.Radius=Style.Current.Name=="Minecraft"?1:8;}
            var grid=c as DataGridView;
            if(grid!=null){grid.EnableHeadersVisualStyles=false;grid.BackgroundColor=Style.Bg;grid.GridColor=Style.Border;grid.DefaultCellStyle.BackColor=Style.Surface;grid.DefaultCellStyle.ForeColor=Style.Text;grid.DefaultCellStyle.SelectionBackColor=Style.Selected;grid.DefaultCellStyle.SelectionForeColor=Style.Text;grid.ColumnHeadersDefaultCellStyle.BackColor=Style.Sidebar;grid.ColumnHeadersDefaultCellStyle.ForeColor=Style.Mint;}
            foreach(Control child in c.Controls)Apply(child);
            c.Invalidate();
        }
        public static void EnginePalette()
        {
            CodexPerformanceOptimizer.Theme.Background=Style.Bg;CodexPerformanceOptimizer.Theme.Header=Style.Sidebar;CodexPerformanceOptimizer.Theme.Navigation=Style.Sidebar;
            CodexPerformanceOptimizer.Theme.Surface=Style.Surface;CodexPerformanceOptimizer.Theme.SurfaceAlt=Style.Hover;CodexPerformanceOptimizer.Theme.SurfaceDark=Style.Bg;
            CodexPerformanceOptimizer.Theme.Border=Style.Border;CodexPerformanceOptimizer.Theme.Text=Style.Text;CodexPerformanceOptimizer.Theme.Muted=Style.Muted;
            CodexPerformanceOptimizer.Theme.Primary=Style.Mint;CodexPerformanceOptimizer.Theme.Secondary=Style.Surface;CodexPerformanceOptimizer.Theme.ButtonText=Style.Text;
        }
    }
}
namespace CodexPerformanceOptimizer
{
    internal sealed partial class MainFormV2
    {
        internal void AttachWorkspace()
        {
            TopLevel=false;FormBorderStyle=FormBorderStyle.None;MinimumSize=Size.Empty;Dock=DockStyle.Fill;
            _navigationPanel.Visible=false;_trayIcon.Visible=false;
            _automaticProfiles.Enabled=false;_minimizeToTray.Enabled=false;
        }
        bool initialized;
        internal async void SelectWorkspace(int index,bool live)
        {
            _tabs.SelectedIndex=index;
            if(!live)return;
            if(!initialized){initialized=true;_activitySampler.Prime();_processSampler.Prime();_liveMetricsTimer.Start();}
            if(FrankensteinTI.Session.Busy)return;
            if(index==0)await RefreshAudit();
            else if(index==1)LoadVolumes();
        }
        internal void StopWorkspace(){_liveMetricsTimer.Stop();_liveMetricsTimer.Dispose();_trayIcon.Visible=false;_trayIcon.Dispose();_toolTip.Dispose();}
    }
}
