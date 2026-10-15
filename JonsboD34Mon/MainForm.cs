using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace JonsboD34Mon;

/// <summary>
/// The window: a live replica of the D34 screen, painted by hand (no images, no designer controls).
/// It exists only while open; closing it disposes everything it allocated.
/// Layout is in logical 96-dpi units and scaled with Graphics.ScaleTransform.
/// </summary>
internal sealed class MainForm : Form
{
    const int W = 300, H = 592;

    // Hit-test ids
    const int IdTheme = 1, IdMinimize = 2, IdLanguage = 3, IdQuit = 4, IdMetric = 10, IdUnit = 20, IdAutostart = 21, IdScreen = 22, IdGear = 30;

    // Segoe Fluent Icons / MDL2 Assets glyphs
    const string GlyphSun = "", GlyphMoon = "", GlyphMinimize = "", GlyphGear = "", GlyphPower = "";

    readonly SensorMonitor _monitor;
    readonly Settings _settings;
    readonly Action _appearanceChanged, _quit;
    readonly List<(RectangleF Rect, int Id)> _hits = [];
    int _hover = -1;
    ContextMenuStrip? _menu;

    readonly Font _segTime, _segBig, _segMid, _label, _small, _unit, _glyph, _gear, _pill;
    static readonly StringFormat Tight = StringFormat.GenericTypographic;

    Theme T => _settings.DarkTheme ? Theme.Dark : Theme.Light;
    float Zoom => DeviceDpi / 96f;

    /// <param name="appearanceChanged">Called after the theme or language changes (tray menu follows).</param>
    /// <param name="quit">Exits the whole app (the window's own button only hides it to the tray).</param>
    public MainForm(SensorMonitor monitor, Settings settings, Icon icon, Action appearanceChanged, Action quit)
    {
        _monitor = monitor;
        _settings = settings;
        _appearanceChanged = appearanceChanged;
        _quit = quit;

        Text = "Jonsbo D34 Mon";
        Icon = icon;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = true;
        KeyPreview = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        var seg = SegmentFont.Family;
        _segTime = new Font(seg, 34, FontStyle.Regular, GraphicsUnit.Pixel);
        _segBig = new Font(seg, 56, FontStyle.Regular, GraphicsUnit.Pixel);
        _segMid = new Font(seg, 28, FontStyle.Regular, GraphicsUnit.Pixel);
        _label = new Font("Segoe UI Semibold", 13, FontStyle.Regular, GraphicsUnit.Pixel);
        _small = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        _unit = new Font("Segoe UI Semibold", 13, FontStyle.Regular, GraphicsUnit.Pixel);
        _pill = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        string glyphs = GlyphFamily();
        _glyph = new Font(glyphs, 12, FontStyle.Regular, GraphicsUnit.Pixel);
        _gear = new Font(glyphs, 15, FontStyle.Regular, GraphicsUnit.Pixel);

        AutoScaleMode = AutoScaleMode.None; // we scale ourselves from DeviceDpi

        _monitor.Detailed = true;
        _monitor.Updated += OnMonitorUpdated;
    }

    static string GlyphFamily()
    {
        using var test = new Font("Segoe Fluent Icons", 10);
        return test.Name == "Segoe Fluent Icons" ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
    }

    void ApplySize() => ClientSize = new Size((int)Math.Round(W * Zoom), (int)Math.Round(H * Zoom));

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplySize();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

        // DeviceDpi is only known once the handle exists: size and place the window here, above the tray.
        ApplySize();
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
    }

    void OnMonitorUpdated()
    {
        try
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(Invalidate);
        }
        catch (InvalidOperationException) { } // closing
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _monitor.Updated -= OnMonitorUpdated;
        _monitor.Detailed = false;
        _monitor.Options = null;
        _menu?.Dispose();
        foreach (var f in new[] { _segTime, _segBig, _segMid, _label, _small, _unit, _glyph, _gear, _pill })
            f.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
            Close();
        base.OnKeyDown(e);
    }

    // ---- Painting -------------------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var t = T;
        var s = _monitor.Current;
        g.Clear(t.Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.ScaleTransform(Zoom, Zoom);
        _hits.Clear();

        // Header: status, language, theme, close
        Color dot = s.Error != null ? t.Bad : s.ScreenConnected ? t.Ok : t.Dim;
        g.FillEllipse(t.Brush(dot), 20, 20, 7, 7);
        string status = s.Error != null ? L.Error(s.Error)
                      : s.ScreenConnected ? (_settings.ScreenOn ? L.ScreenConnected : L.ScreenOff)
                      : L.ScreenNotFound;
        g.DrawString(status, _small, t.Brush(t.Dim), 32, 15);
        Button(IdLanguage, new RectangleF(W - 92, 10, 26, 26), L.LanguageButton, _pill);
        Button(IdTheme, new RectangleF(W - 64, 10, 26, 26), _settings.DarkTheme ? GlyphSun : GlyphMoon, _glyph);
        // Minimize = hide to the tray (the window is disposed; sensors and the screen keep running).
        Button(IdMinimize, new RectangleF(W - 36, 10, 26, 26), GlyphMinimize, _glyph);

        // TIME
        Label("TIME", 52);
        Digits(-1, _segTime, "88:88", s.Time.ToString("HH:mm"), null, 72);
        Rule(130);

        // CPU TEMP. with the 10-step bar
        Label("CPU TEMP.", 146);
        Bar(s.CpuTemp, 170);
        Digits((int)Metric.CpuTemp, _segBig, null, Temp(s.CpuTemp), Unit, 186);
        Rule(268);

        Label("GPU TEMP.", 282);
        Digits((int)Metric.GpuTemp, _segMid, null, Temp(s.GpuTemp), Unit, 302);
        Label("CPU USAGE", 348);
        Digits((int)Metric.CpuUsage, _segMid, null, Whole(s.CpuUsage), "%", 368);
        Label("CPU FREQ", 414);
        Digits((int)Metric.CpuFreq, _segMid, "8.8", double.IsNaN(s.CpuFreqMhz) ? "-.-" : (s.CpuFreqMhz / 1000).ToString("0.0"), "GHz", 434);

        // Footer toggles
        g.DrawLine(t.Pen(t.Border), 20, 492, W - 20, 492);
        Pills(508, (IdUnit, _settings.Fahrenheit ? "°F" : "°C", true),
                   (IdAutostart, L.Autostart, _settings.Autostart),
                   (IdScreen, L.Screen, _settings.ScreenOn));
        QuitButton(548);

        g.ResetTransform();
        g.SmoothingMode = SmoothingMode.None;
        g.DrawRectangle(t.Pen(t.Border), 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

        // --- local helpers (all in logical units) ---
        void Label(string text, float y)
        {
            float w = g.MeasureString(text, _label, PointF.Empty, Tight).Width;
            g.DrawString(text, _label, t.Brush(t.Label), (W - w) / 2, y, Tight);
        }

        void Rule(float y) => g.DrawLine(t.Pen(t.Rule, 1.5f), 46, y, W - 46, y);

        void Bar(double celsius, float y)
        {
            int lit = double.IsNaN(celsius) ? 0 : (int)Math.Round(Math.Clamp(celsius, 0, 100) / 10);
            Color on = celsius >= 85 ? t.Hot : t.Warm;
            const float size = 9, gap = 4;
            float x = (W - (10 * size + 9 * gap)) / 2;
            for (int i = 0; i < 10; i++)
                g.FillRectangle(t.Brush(i < lit ? on : t.BarOff), x + i * (size + gap), y, size, size);
        }

        // Seven-segment number with "ghost" unlit segments behind it, like the real panel.
        // metric >= 0: the number is clickable and gets a gear button at the right edge (both open the sensor picker).
        void Digits(int metric, Font font, string? ghost, string value, string? unit, float y)
        {
            ghost ??= new string('8', Math.Max(2, value.Length));
            // '!' is a blank of digit width in DSEG fonts: right-aligns the value over the ghost.
            string shown = value.Length < ghost.Length ? new string('!', ghost.Length - value.Length) + value : value;
            SizeF gs = g.MeasureString(ghost, font, PointF.Empty, Tight);
            float uw = unit == null ? 0 : g.MeasureString(unit, _unit, PointF.Empty, Tight).Width + 6;
            float x = (W - gs.Width - uw) / 2;
            if (metric >= 0)
            {
                int id = IdMetric + metric, gearId = IdGear + metric;
                var hit = new RectangleF(x - 12, y - 6, gs.Width + uw + 24, gs.Height + 12);
                _hits.Add((hit, id));
                if (_hover == id)
                {
                    using var path = AppIcon.RoundRect(hit, 8);
                    g.FillPath(t.Brush(t.Hover), path);
                }
                // Framed so it reads as a button; hovering the number also lights up its gear.
                Button(gearId, new RectangleF(W - 48, y + gs.Height / 2 - 14, 28, 28), GlyphGear, _gear, lit: _hover == id, framed: true);
            }
            g.DrawString(ghost, font, t.Brush(t.Ghost), x, y, Tight);
            g.DrawString(shown, font, t.Brush(t.Text), x, y, Tight);
            if (unit != null)
                g.DrawString(unit, _unit, t.Brush(t.Dim), x + gs.Width + 6, y + gs.Height - _unit.Height - 1, Tight);
        }

        void Button(int id, RectangleF r, string text, Font font, bool lit = false, bool framed = false)
        {
            _hits.Add((r, id));
            bool hot = _hover == id;
            if (hot || framed)
            {
                using var path = AppIcon.RoundRect(r, 7);
                if (hot)
                    g.FillPath(t.Brush(t.Hover), path);
                if (framed)
                    g.DrawPath(t.Pen(hot || lit ? t.Accent : t.Border), path);
            }
            SizeF ts = g.MeasureString(text, font, PointF.Empty, Tight);
            Color color = hot ? t.Text : lit ? t.Accent : framed ? t.Label : t.Dim;
            g.DrawString(text, font, t.Brush(color), r.X + (r.Width - ts.Width) / 2, r.Y + (r.Height - ts.Height) / 2, Tight);
        }

        // Separate from the toggles on purpose: this one stops the program (and the case screen).
        void QuitButton(float y)
        {
            const float h = 26, gap = 6;
            string text = L.Quit;
            float tw = g.MeasureString(text, _pill, PointF.Empty, Tight).Width;
            float gw = g.MeasureString(GlyphPower, _glyph, PointF.Empty, Tight).Width;
            float w = gw + gap + tw + 24;
            var r = new RectangleF((W - w) / 2, y, w, h);
            _hits.Add((r, IdQuit));
            bool hot = _hover == IdQuit;
            using (var path = AppIcon.RoundRect(r, 7))
            {
                if (hot)
                    g.FillPath(t.Brush(t.Hover), path);
                g.DrawPath(t.Pen(hot ? t.Bad : t.Border), path);
            }
            Color c = hot ? t.Bad : t.Dim;
            g.DrawString(GlyphPower, _glyph, t.Brush(c), r.X + 12, y + (h - _glyph.Height) / 2 + 1, Tight);
            g.DrawString(text, _pill, t.Brush(c), r.X + 12 + gw + gap, y + (h - _pill.Height) / 2 + 1, Tight);
        }

        void Pills(float y, params (int Id, string Text, bool On)[] pills)
        {
            const float pad = 10, gap = 6, h = 26;
            var widths = pills.Select(p => g.MeasureString(p.Text, _pill, PointF.Empty, Tight).Width + pad * 2).ToArray();
            float x = (W - widths.Sum() - gap * (pills.Length - 1)) / 2;
            for (int i = 0; i < pills.Length; i++)
            {
                var (id, text, on) = pills[i];
                var r = new RectangleF(x, y, widths[i], h);
                _hits.Add((r, id));
                using (var path = AppIcon.RoundRect(r, 7))
                {
                    if (_hover == id)
                        g.FillPath(t.Brush(t.Hover), path);
                    g.DrawPath(t.Pen(on ? t.Accent : t.Border), path);
                }
                g.DrawString(text, _pill, t.Brush(on ? t.Accent : t.Dim), x + pad, y + (h - _pill.Height) / 2 + 1, Tight);
                x += widths[i] + gap;
            }
        }
    }

    string Unit => _settings.Fahrenheit ? "°F" : "°C";

    string Temp(double c) => double.IsNaN(c) ? "--" : Whole(_settings.Fahrenheit ? c * 1.8 + 32 : c);

    static string Whole(double v) => double.IsNaN(v) ? "--" : ((int)Math.Round(v)).ToString();

    // ---- Input ----------------------------------------------------------------------------------

    int HitTest(Point p)
    {
        var pt = new PointF(p.X / Zoom, p.Y / Zoom);
        foreach (var (rect, id) in _hits)
            if (rect.Contains(pt))
                return id;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int id = HitTest(e.Location);
        if (id != _hover)
        {
            _hover = id;
            Cursor = id >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hover != -1)
        {
            _hover = -1;
            Invalidate();
        }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;

        int id = HitTest(e.Location);
        switch (id)
        {
            case -1: // drag the window by any empty spot
                Native.ReleaseCapture();
                Native.SendMessageW(Handle, Native.WM_NCLBUTTONDOWN, Native.HTCAPTION, 0);
                break;
            case IdMinimize:
                Close();
                break;
            case IdQuit:
                _quit();
                return;
            case IdTheme:
                _settings.DarkTheme = !_settings.DarkTheme;
                _settings.Save();
                _appearanceChanged();
                break;
            case IdLanguage:
                _settings.English = L.En = !_settings.English;
                _settings.Save();
                _appearanceChanged();
                break;
            case IdUnit:
                _settings.Fahrenheit = !_settings.Fahrenheit;
                _settings.Save();
                _monitor.Poke();
                break;
            case IdScreen:
                _settings.ScreenOn = !_settings.ScreenOn;
                _settings.Save();
                _monitor.Poke();
                break;
            case IdAutostart:
                ToggleAutostart();
                break;
            case >= IdMetric and < IdMetric + 4:
            {
                var r = _hits.First(h => h.Id == id).Rect;
                ShowSensorMenu((Metric)(id - IdMetric), new Point((int)(r.Left * Zoom), (int)(r.Bottom * Zoom)), ToolStripDropDownDirection.BelowRight);
                break;
            }
            case >= IdGear and < IdGear + 4:
            {
                // Opens leftwards from the gear so the (wide) list stays inside the window's screen.
                var r = _hits.First(h => h.Id == id).Rect;
                ShowSensorMenu((Metric)(id - IdGear), new Point((int)(r.Right * Zoom), (int)(r.Bottom * Zoom)), ToolStripDropDownDirection.BelowLeft);
                break;
            }
        }
        Invalidate();
    }

    void ToggleAutostart()
    {
        bool want = !_settings.Autostart;
        Cursor = Cursors.WaitCursor;
        bool ok = Autostart.Set(want, out string error);
        Cursor = Cursors.Default;
        if (ok)
        {
            _settings.Autostart = want;
            _settings.Save();
        }
        else
        {
            MessageBox.Show(this, L.AutostartFailed + "\n" + error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Theme or language changed.</summary>
    public void ApplyAppearance()
    {
        if (_menu != null)
            _menu.Renderer = T.CreateMenuRenderer();
        Invalidate();
    }

    void ShowSensorMenu(Metric metric, Point at, ToolStripDropDownDirection direction)
    {
        _menu?.Dispose();
        var menu = _menu = new ContextMenuStrip { Renderer = T.CreateMenuRenderer(), ShowImageMargin = false, ShowCheckMargin = true };

        var data = _monitor.Options;
        var options = data?.Catalog.Options[(int)metric] ?? [];
        string? current = _monitor.SelectedKey(metric);
        if (options.Length == 0)
            menu.Items.Add(new ToolStripMenuItem(data == null ? L.SensorsLoading : L.NoSensors) { Enabled = false });

        // Same label on two devices (e.g. two "CPU (Tctl/Tdie)") → add the device name.
        var duplicate = options.GroupBy(o => o.Title).Where(gr => gr.Count() > 1).Select(gr => gr.Key).ToHashSet();
        for (int i = 0; i < options.Length; i++)
        {
            var option = options[i];
            string title = L.OptionTitle(option);
            if (duplicate.Contains(option.Title) && option.Device.Length > 0)
                title += "  ·  " + option.Device;
            var item = new ToolStripMenuItem(title)
            {
                Checked = option.Key == current,
                ShortcutKeyDisplayString = FormatOption(metric, data!.Values[(int)metric][i]),
            };
            item.Click += (_, _) =>
            {
                _settings.Sensors[(int)metric] = option.Key;
                _settings.Save();
                _monitor.Poke();
            };
            menu.Items.Add(item);
        }
        menu.Items.Add(new ToolStripSeparator());
        var rescan = new ToolStripMenuItem(L.RefreshSensors);
        rescan.Click += (_, _) => _monitor.RequestRescan();
        menu.Items.Add(rescan);

        menu.Show(this, at, direction);
    }

    string FormatOption(Metric m, double v)
    {
        if (double.IsNaN(v))
            return "—";
        return m switch
        {
            Metric.CpuTemp or Metric.GpuTemp => $"{Temp(v)} {Unit}",
            Metric.CpuUsage => $"{Whole(v)} %",
            _ => $"{v / 1000:0.00} GHz",
        };
    }
}

/// <summary>DSEG7 Classic (SIL OFL 1.1), embedded; loaded on first window open and kept.</summary>
internal static class SegmentFont
{
    static PrivateFontCollection? _fonts;

    public static FontFamily Family
    {
        get
        {
            if (_fonts == null)
            {
                using var stream = typeof(SegmentFont).Assembly.GetManifestResourceStream("seg.ttf")!;
                byte[] data = new byte[stream.Length];
                stream.ReadExactly(data);
                // AddMemoryFont needs the memory to stay valid for the collection's lifetime.
                nint mem = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, mem, data.Length);
                _fonts = new PrivateFontCollection();
                _fonts.AddMemoryFont(mem, data.Length);
            }
            return _fonts.Families[0];
        }
    }
}
