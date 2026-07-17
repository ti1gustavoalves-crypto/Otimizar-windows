using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodexPerformanceOptimizer
{
    internal static class UiGeometry
    {
        public static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
        {
            var path = new GraphicsPath();
            if (rectangle.Width <= 2 || rectangle.Height <= 2)
            {
                path.AddRectangle(rectangle);
                return path;
            }
            int effectiveRadius = Math.Max(1, Math.Min(radius, Math.Min(rectangle.Width, rectangle.Height) / 2));
            int diameter = effectiveRadius * 2;
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ModernButton : Button
    {
        public Color BaseColor { get; set; }
        public int Radius { get; set; }

        public ModernButton()
        {
            Radius = 8;
            UseVisualStyleBackColor = false;
            Font = new Font("Segoe UI Semibold", 9f);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width <= 1 || Height <= 1) return;
            using (GraphicsPath path = UiGeometry.RoundedRectangle(new Rectangle(0, 0, Width, Height), Radius))
            {
                Region oldRegion = Region;
                Region = new Region(path);
                if (oldRegion != null) oldRegion.Dispose();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (Enabled) BackColor = ControlPaint.Light(BaseColor, 0.08f);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            BackColor = BaseColor;
        }
    }

    internal sealed class DashboardPanel : Panel
    {
        public int Radius { get; set; }
        public Color BorderColor { get; set; }

        public DashboardPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Radius = 10;
            BorderColor = Color.Transparent;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (Width <= 1 || Height <= 1) return;
            using (GraphicsPath path = UiGeometry.RoundedRectangle(new Rectangle(0, 0, Width, Height), Radius))
            {
                Region oldRegion = Region;
                Region = new Region(path);
                if (oldRegion != null) oldRegion.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (BorderColor == Color.Transparent || Width <= 1 || Height <= 1) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = UiGeometry.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (var pen = new Pen(BorderColor))
                e.Graphics.DrawPath(pen, path);
        }
    }

    internal sealed class SparklineChart : Control
    {
        private readonly List<float> _values = new List<float>();
        private Color _lineColor = Theme.Primary;

        public Color LineColor
        {
            get { return _lineColor; }
            set { _lineColor = value; Invalidate(); }
        }

        public SparklineChart()
        {
            DoubleBuffered = true;
            AccessibleRole = AccessibleRole.Graphic;
            TabStop = false;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }

        public void AddValue(double value)
        {
            _values.Add((float)Math.Max(0, Math.Min(100, value)));
            if (_values.Count > 60) _values.RemoveAt(0);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 2 || Height < 2) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var gridPen = new Pen(Color.FromArgb(32, Theme.Muted)))
            {
                e.Graphics.DrawLine(gridPen, 0, Height / 2, Width, Height / 2);
                e.Graphics.DrawLine(gridPen, 0, Height - 1, Width, Height - 1);
            }
            if (_values.Count == 0) return;

            var points = new PointF[_values.Count];
            float step = _values.Count > 1 ? (Width - 1f) / (_values.Count - 1) : 0;
            for (int i = 0; i < _values.Count; i++)
                points[i] = new PointF(i * step, (Height - 2f) * (1f - (_values[i] / 100f)) + 1f);

            if (points.Length == 1)
            {
                using (var dot = new SolidBrush(LineColor)) e.Graphics.FillEllipse(dot, 0, points[0].Y - 2, 4, 4);
                return;
            }

            var fillPoints = new PointF[points.Length + 2];
            fillPoints[0] = new PointF(points[0].X, Height);
            Array.Copy(points, 0, fillPoints, 1, points.Length);
            fillPoints[fillPoints.Length - 1] = new PointF(points[points.Length - 1].X, Height);
            using (var fill = new SolidBrush(Color.FromArgb(34, LineColor))) e.Graphics.FillPolygon(fill, fillPoints);
            using (var line = new Pen(LineColor, 1.8f)) e.Graphics.DrawLines(line, points);
        }
    }

    internal sealed class ModernProgressBar : Control
    {
        private int _value;
        private Color _barColor = Theme.Primary;
        private Color _trackColor = Theme.SurfaceAlt;

        public int Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public Color BarColor
        {
            get { return _barColor; }
            set { _barColor = value; Invalidate(); }
        }

        public Color TrackColor
        {
            get { return _trackColor; }
            set { _trackColor = value; Invalidate(); }
        }

        public ModernProgressBar()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AccessibleRole = AccessibleRole.ProgressBar;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath trackPath = UiGeometry.RoundedRectangle(track, 4))
            using (var trackBrush = new SolidBrush(TrackColor))
                e.Graphics.FillPath(trackBrush, trackPath);

            int fillWidth = (int)Math.Round(track.Width * (Value / 100.0));
            if (fillWidth <= 0) return;
            Rectangle fill = new Rectangle(0, 0, Math.Max(2, fillWidth), track.Height);
            using (GraphicsPath fillPath = UiGeometry.RoundedRectangle(fill, 4))
            using (var fillBrush = new SolidBrush(BarColor))
                e.Graphics.FillPath(fillBrush, fillPath);
        }
    }

    internal sealed class SafeCleanupForm : Form
    {
        private readonly CheckedListBox _items;
        private readonly List<CleanupTarget> _targets;

        public List<CleanupTarget> SelectedTargets
        {
            get
            {
                var selected = new List<CleanupTarget>();
                for (int i = 0; i < _items.Items.Count; i++) if (_items.GetItemChecked(i)) selected.Add(_targets[i]);
                return selected;
            }
        }

        public SafeCleanupForm(List<CleanupTarget> targets)
        {
            _targets = targets;
            Text = "Limpeza segura";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(620, 430);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            NativeWindowTheme.Apply(this);
            Controls.Add(new Label { Text = "Arquivos temporários e caches", Location = new Point(24, 20), AutoSize = true, Font = new Font("Segoe UI Semibold", 14f) });
            Controls.Add(new Label { Text = "Cookies, senhas, documentos e arquivos pessoais não são incluídos.", Location = new Point(27, 54), AutoSize = true, ForeColor = Theme.Muted });
            _items = new CheckedListBox { Location = new Point(26, 88), Size = new Size(566, 260), BackColor = Theme.SurfaceDark, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, CheckOnClick = true };
            foreach (CleanupTarget target in targets) _items.Items.Add(target.Name + "   " + V2Engine.FormatBytes(target.SizeBytes), target.DefaultSelected);
            var clean = DialogButton("Limpar selecionados", DialogResult.OK, 332, 150, Theme.Primary);
            var cancel = DialogButton("Cancelar", DialogResult.Cancel, 492, 100, Theme.Secondary);
            Controls.Add(_items);
            Controls.Add(clean);
            Controls.Add(cancel);
            AcceptButton = clean;
            CancelButton = cancel;
        }

        private static Button DialogButton(string text, DialogResult result, int x, int width, Color color)
        {
            var button = new ModernButton { Text = text, DialogResult = result, Location = new Point(x, 370), Size = new Size(width, 38), BackColor = color, BaseColor = color, ForeColor = Theme.ButtonText, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }

    internal static class Theme
    {
        private static readonly bool HighContrast = SystemInformation.HighContrast;
        public static readonly Color Background = HighContrast ? SystemColors.Window : Color.FromArgb(14, 18, 24);
        public static readonly Color Header = HighContrast ? SystemColors.Control : Color.FromArgb(10, 14, 20);
        public static readonly Color Navigation = HighContrast ? SystemColors.Control : Color.FromArgb(10, 14, 20);
        public static readonly Color Surface = HighContrast ? SystemColors.Control : Color.FromArgb(23, 29, 38);
        public static readonly Color SurfaceAlt = HighContrast ? SystemColors.ControlDark : Color.FromArgb(32, 41, 53);
        public static readonly Color SurfaceDark = HighContrast ? SystemColors.Window : Color.FromArgb(12, 16, 22);
        public static readonly Color Border = HighContrast ? SystemColors.WindowText : Color.FromArgb(41, 51, 65);
        public static readonly Color Text = HighContrast ? SystemColors.WindowText : Color.FromArgb(241, 245, 249);
        public static readonly Color Muted = HighContrast ? SystemColors.GrayText : Color.FromArgb(148, 163, 184);
        public static readonly Color Primary = HighContrast ? SystemColors.Highlight : Color.FromArgb(18, 137, 190);
        public static readonly Color Secondary = HighContrast ? SystemColors.ControlDark : Color.FromArgb(39, 49, 63);
        public static readonly Color Success = HighContrast ? SystemColors.Highlight : Color.FromArgb(47, 203, 145);
        public static readonly Color Warning = HighContrast ? SystemColors.HotTrack : Color.FromArgb(224, 151, 55);
        public static readonly Color Danger = HighContrast ? SystemColors.HotTrack : Color.FromArgb(226, 84, 96);
        public static readonly Color ButtonText = HighContrast ? SystemColors.HighlightText : Color.White;
    }
}
