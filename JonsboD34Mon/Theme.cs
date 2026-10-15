using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace JonsboD34Mon;

internal sealed class Theme
{
    public required Color Background, Text, Label, Ghost, Dim, Rule, Border, Hover, Warm, Hot, BarOff, Ok, Bad, Accent, MenuHover;

    public static readonly Theme Dark = new()
    {
        Background = Hex(0x0D0F10), Text = Hex(0xEEF7FA), Label = Hex(0xCFD8DC), Ghost = Hex(0x1A1F22),
        Dim = Hex(0x7D8B90), Rule = Hex(0xAEB9BD), Border = Hex(0x2A3033), Hover = Hex(0x171C1F),
        Warm = Hex(0xFF9A2E), Hot = Hex(0xFF5A4A), BarOff = Hex(0x262B2E), Ok = Hex(0x3CCF7A), Bad = Hex(0xE5534B),
        Accent = Hex(0xFFB15C), MenuHover = Hex(0x23292C),
    };

    public static readonly Theme Light = new()
    {
        Background = Hex(0xF3F5F6), Text = Hex(0x0E1214), Label = Hex(0x3C474C), Ghost = Hex(0xE0E5E7),
        Dim = Hex(0x6A777C), Rule = Hex(0x3C474C), Border = Hex(0xD3D9DC), Hover = Hex(0xE7EBED),
        Warm = Hex(0xF07F12), Hot = Hex(0xE0402F), BarOff = Hex(0xD7DDE0), Ok = Hex(0x1F9D57), Bad = Hex(0xC93C33),
        Accent = Hex(0xD9730D), MenuHover = Hex(0xE3E8EA),
    };

    static Color Hex(int rgb) => Color.FromArgb(255, Color.FromArgb(rgb));

    // Two themes × a dozen colors: brushes and pens are created once and kept for the process lifetime.
    readonly Dictionary<Color, SolidBrush> _brushes = [];
    readonly Dictionary<(Color, float), Pen> _pens = [];

    public Brush Brush(Color c)
    {
        if (!_brushes.TryGetValue(c, out var b))
            _brushes[c] = b = new SolidBrush(c);
        return b;
    }

    public Pen Pen(Color c, float width = 1f)
    {
        if (!_pens.TryGetValue((c, width), out var p))
            _pens[(c, width)] = p = new Pen(c, width);
        return p;
    }

    public ToolStripRenderer CreateMenuRenderer() => new MenuRenderer(this);

    sealed class MenuRenderer(Theme t) : ToolStripProfessionalRenderer(new Colors(t))
    {
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? t.Text : t.Dim;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            var r = e.ImageRectangle;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(t.Accent, Math.Max(1.6f, r.Height / 9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float x = r.X + r.Width * 0.2f, y = r.Y + r.Height * 0.52f;
            g.DrawLines(pen, [new PointF(x, y), new PointF(r.X + r.Width * 0.42f, r.Y + r.Height * 0.74f), new PointF(r.X + r.Width * 0.82f, r.Y + r.Height * 0.3f)]);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = t.Dim;
            base.OnRenderArrow(e);
        }
    }

    sealed class Colors(Theme t) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => t.Background;
        public override Color ImageMarginGradientBegin => t.Background;
        public override Color ImageMarginGradientMiddle => t.Background;
        public override Color ImageMarginGradientEnd => t.Background;
        public override Color MenuBorder => t.Border;
        public override Color MenuItemBorder => t.MenuHover;
        public override Color MenuItemSelected => t.MenuHover;
        public override Color SeparatorDark => t.Border;
        public override Color SeparatorLight => t.Border;
        public override Color CheckBackground => Color.Transparent;
        public override Color CheckSelectedBackground => Color.Transparent;
        public override Color CheckPressedBackground => Color.Transparent;
    }
}

/// <summary>The tray/taskbar icon, drawn in code: a tiny dark panel with the orange bar and two "digit" strokes.</summary>
internal static class AppIcon
{
    public static Icon Create(int size)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float s = size / 16f;
            using (var path = RoundRect(new RectangleF(0.5f * s, 0.5f * s, 15 * s, 15 * s), 3.2f * s))
            using (var bg = new SolidBrush(Color.FromArgb(0x10, 0x14, 0x16)))
                g.FillPath(bg, path);
            using var warm = new SolidBrush(Color.FromArgb(0xFF, 0x9A, 0x2E));
            for (int i = 0; i < 4; i++)
                g.FillRectangle(warm, (3.2f + i * 2.6f) * s, 3.4f * s, 1.8f * s, 1.8f * s);
            using var white = new SolidBrush(Color.FromArgb(0xEE, 0xF7, 0xFA));
            g.FillRectangle(white, 3.2f * s, 7.4f * s, 9.6f * s, 1.9f * s);
            g.FillRectangle(white, 3.2f * s, 11f * s, 6.2f * s, 1.9f * s);
        }
        nint h = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(h).Clone();
        }
        finally
        {
            Native.DestroyIcon(h);
        }
    }

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
