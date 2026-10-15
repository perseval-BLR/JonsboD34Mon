using Microsoft.Win32;

namespace JonsboD34Mon;

/// <summary>Tray icon + menu. The window is created on demand and disposed when closed.</summary>
internal sealed class TrayApp : ApplicationContext
{
    readonly Settings _settings = Settings.Load();
    readonly SensorMonitor _monitor;
    readonly NotifyIcon _tray;
    readonly ContextMenuStrip _menu;
    readonly ToolStripMenuItem _screenItem, _unitItem;
    readonly Icon _icon;
    readonly Control _ui = new(); // marshals calls from other threads onto the UI thread
    MainForm? _form;

    public TrayApp(EventWaitHandle showRequest)
    {
        L.En = _settings.English;
        _ui.CreateControl();
        _icon = AppIcon.Create(SystemInformation.SmallIconSize.Width);
        _monitor = new SensorMonitor(_settings);

        _menu = new ContextMenuStrip { Renderer = Theme().CreateMenuRenderer(), ShowImageMargin = false, ShowCheckMargin = true };
        var openItem = new ToolStripMenuItem("", null, (_, _) => ShowWindow());
        _screenItem = new ToolStripMenuItem("", null, (_, _) => Changed(_settings.ScreenOn = !_settings.ScreenOn));
        _unitItem = new ToolStripMenuItem("", null, (_, _) => Changed(_settings.Fahrenheit = !_settings.Fahrenheit));
        var exitItem = new ToolStripMenuItem("", null, (_, _) => Exit());
        _menu.Items.AddRange([openItem, _screenItem, _unitItem, new ToolStripSeparator(), exitItem]);
        // Texts and checks are filled in on every opening, so language changes need no extra wiring.
        _menu.Opening += (_, _) =>
        {
            openItem.Text = L.Open;
            _screenItem.Text = L.ShowOnScreen;
            _unitItem.Text = L.Fahrenheit;
            exitItem.Text = L.Exit;
            _screenItem.Checked = _settings.ScreenOn;
            _unitItem.Checked = _settings.Fahrenheit;
        };

        _tray = new NotifyIcon { Icon = _icon, Text = "Jonsbo D34 Mon", ContextMenuStrip = _menu, Visible = true };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowWindow();
        };
        // Tooltip is refreshed only when the pointer is over the icon — no periodic UI work.
        _tray.MouseMove += (_, _) => UpdateTooltip();

        _showWait = ThreadPool.RegisterWaitForSingleObject(showRequest, (_, _) =>
        {
            try { _ui.BeginInvoke(ShowWindow); }
            catch (InvalidOperationException) { } // already exiting
        }, null, -1, false);
        // SessionEnded, not SessionEnding: a shutdown can still be cancelled after SessionEnding.
        SystemEvents.SessionEnded += OnSessionEnded;
        // DateTime.Now caches the time zone; refresh it so the clock on the screen follows a zone change.
        SystemEvents.TimeChanged += OnTimeChanged;

        _monitor.Start();
        if (_settings.FirstRun)
        {
            _settings.Save();
            ShowWindow();
        }
        else if (_settings.Autostart)
        {
            // Re-point the logon task at this exe, in case the program was moved or reinstalled elsewhere.
            ThreadPool.QueueUserWorkItem(_ => Autostart.Set(true, out string _));
        }
    }

    readonly RegisteredWaitHandle _showWait;

    Theme Theme() => _settings.DarkTheme ? JonsboD34Mon.Theme.Dark : JonsboD34Mon.Theme.Light;

    void Changed(bool _)
    {
        _settings.Save();
        _monitor.Poke();
        _form?.Invalidate();
    }

    void UpdateTooltip()
    {
        var s = _monitor.Current;
        string unit = _settings.Fahrenheit ? "°F" : "°C";
        string T(double c) => double.IsNaN(c) ? "--" : ((int)Math.Round(_settings.Fahrenheit ? c * 1.8 + 32 : c)).ToString();
        string text = $"CPU {T(s.CpuTemp)}{unit} · GPU {T(s.GpuTemp)}{unit}";
        if (!s.ScreenConnected)
            text += "\n" + L.ScreenNotFound;
        _tray.Text = text;
    }

    void ShowWindow()
    {
        if (_form == null || _form.IsDisposed)
        {
            _form = new MainForm(_monitor, _settings, _icon, OnAppearanceChanged, Exit);
            _form.FormClosed += (_, _) =>
            {
                _form = null;
                // Window resources are gone; give the memory back to the OS.
                _ui.BeginInvoke(Native.TrimWorkingSet);
            };
            _form.Show();
        }
        if (_form.WindowState == FormWindowState.Minimized)
            _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    void OnAppearanceChanged()
    {
        _menu.Renderer = Theme().CreateMenuRenderer();
        _form?.ApplyAppearance();
    }

    // Logoff/shutdown, and also Restart Manager closing us (e.g. an installer updating the exe): exit cleanly.
    void OnSessionEnded(object? sender, SessionEndedEventArgs e) => Exit();

    static void OnTimeChanged(object? sender, EventArgs e) => TimeZoneInfo.ClearCachedData();

    void Exit()
    {
        _form?.Close();
        _monitor.Dispose();
        _tray.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _showWait.Unregister(null);
            SystemEvents.SessionEnded -= OnSessionEnded;
            SystemEvents.TimeChanged -= OnTimeChanged;
            _monitor.Dispose();
            _tray.Dispose();
            _menu.Dispose();
            _icon.Dispose();
            _ui.Dispose();
        }
        base.Dispose(disposing);
    }
}
