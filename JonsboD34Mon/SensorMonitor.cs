using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace JonsboD34Mon;

internal sealed record Snapshot(double CpuTemp, double GpuTemp, double CpuUsage, double CpuFreqMhz,
                                DateTime Time, bool ScreenConnected, string? Error)
{
    public static readonly Snapshot Starting = new(double.NaN, double.NaN, double.NaN, double.NaN, DateTime.Now, false, null);
}

/// <summary>Live values of every option, published only while the window is open (for the sensor picker).</summary>
internal sealed record OptionValues(SensorCatalog Catalog, double[][] Values);

/// <summary>
/// Two background threads, nothing else runs while the window is closed:
/// <list type="bullet">
/// <item>Sensors (normal priority): once per second updates the CPU through LibreHardwareMonitor
/// (one call refreshes all CPU sensors) and reads the selected GPU's temperature from Windows.</item>
/// <item>Screen (highest priority, does almost nothing): every 250 ms sends the latest values to the case screen.
/// The screen blanks after ~1 s without a packet, so sending must never wait on a slow sensor read
/// or be starved by a 100% CPU load.</item>
/// </list>
/// </summary>
internal sealed class SensorMonitor : IDisposable
{
    const int ResendMs = 250;
    const int FirstReadingTimeoutMs = 5000;

    readonly Settings _settings;
    readonly CaseDisplay _display = new();          // screen thread only
    readonly byte[] _packet = new byte[64];         // screen thread only
    readonly Thread _sensorThread, _screenThread;
    readonly AutoResetEvent _sensorWake = new(false), _screenWake = new(false);
    readonly ManualResetEventSlim _firstReading = new(false);

    // Sensor thread only
    Computer? _computer;
    IHardware[] _cpus = [];
    GpuTemp? _gpu;
    string? _error;
    readonly SensorOption?[] _selected = new SensorOption?[4];

    volatile bool _stop, _rescan, _reselect = true, _screenConnected;
    volatile SensorCatalog _catalog = SensorCatalog.Empty;

    public SensorMonitor(Settings settings)
    {
        _settings = settings;
        _sensorThread = new Thread(SensorLoop) { IsBackground = true, Name = "Sensors" };
        _screenThread = new Thread(ScreenLoop) { IsBackground = true, Name = "Screen", Priority = ThreadPriority.Highest };
    }

    /// <summary>Raised on the sensor thread after each tick.</summary>
    public event Action? Updated;

    public volatile Snapshot Current = Snapshot.Starting;

    /// <summary>While true (window open), every option's value is read too.</summary>
    public volatile bool Detailed;

    public volatile OptionValues? Options;

    public void Start()
    {
        _sensorThread.Start();
        _screenThread.Start();
    }

    /// <summary>Apply changed settings right away instead of on the next second.</summary>
    public void Poke()
    {
        _reselect = true;
        _sensorWake.Set();
        _screenWake.Set();
    }

    /// <summary>Looks for GPUs again (e.g. after a driver update or an eGPU was plugged in).</summary>
    public void RequestRescan()
    {
        _rescan = true;
        _sensorWake.Set();
    }

    public string? SelectedKey(Metric m) => _selected[(int)m]?.Key;

    // ---- Screen thread --------------------------------------------------------------------------

    void ScreenLoop()
    {
        // Don't flash zeros while the sensors are still starting.
        _firstReading.Wait(FirstReadingTimeoutMs);
        bool offSent = false;
        while (!_stop)
        {
            if (_settings.ScreenOn)
            {
                CaseDisplay.BuildPacket(_packet, Current, DateTime.Now, _settings.Fahrenheit);
                _display.Send(_packet);
                offSent = false;
            }
            else if (!offSent)
            {
                CaseDisplay.BuildOffPacket(_packet);
                offSent = _display.Send(_packet);
            }
            _screenConnected = _display.IsConnected;
            _screenWake.WaitOne(ResendMs - DateTime.Now.Millisecond % ResendMs + 5);
        }

        if (_settings.ScreenOn)
        {
            CaseDisplay.BuildOffPacket(_packet);
            _display.Send(_packet);
        }
        _display.Dispose();
    }

    // ---- Sensor thread --------------------------------------------------------------------------

    void SensorLoop()
    {
        Open();
        // Pages touched only during start-up are handed back once.
        Native.TrimWorkingSet();

        while (!_stop)
        {
            Tick();
            _firstReading.Set();
            // Read just after each second boundary, in step with the clock on the screen.
            _sensorWake.WaitOne(1000 - DateTime.Now.Millisecond + 20);
        }

        _gpu?.Dispose();
        try { _computer?.Close(); }
        catch (Exception) { } // shutting down anyway
    }

    void Open()
    {
        try
        {
            // CPU only: no motherboard/storage/GPU/controller scanning, nothing else gets loaded.
            _computer = new Computer { IsCpuEnabled = true };
            _computer.Open();
            _cpus = _computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu).ToArray();
            foreach (var cpu in _cpus)
                cpu.Update();
        }
        catch (Exception e)
        {
            _error = e.Message;
            _cpus = [];
        }
        // CPU temperatures and clocks need the PawnIO driver (installed by our setup); load works without it.
        if (_error == null && !PawnIoInstalled())
            _error = "pawnio";
        BuildCatalog();
    }

    void BuildCatalog()
    {
        _gpu?.Dispose();
        try
        {
            _gpu = new GpuTemp();
        }
        catch (Exception)
        {
            _gpu = null; // very old Windows/driver: no GPU temperature
        }
        _catalog = new SensorCatalog(_cpus, _gpu?.Adapters ?? []);
        _reselect = true;
    }

    static bool PawnIoInstalled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO");
        return key != null;
    }

    void Tick()
    {
        if (_rescan)
        {
            _rescan = false;
            BuildCatalog();
        }
        if (_reselect)
            Select();

        foreach (var cpu in _cpus)
        {
            try { cpu.Update(); }
            catch (Exception) { } // a failed read shows as "--" for this second
        }

        bool detailed = Detailed;
        if (detailed)
            ReadAllOptions();
        else if (Options != null)
            Options = null; // window closed: don't keep (or later show) stale picker values

        Current = new Snapshot(Read(_selected[0]), Read(_selected[1]), Read(_selected[2]), Read(_selected[3]),
                               DateTime.Now, _screenConnected, _error);
        Updated?.Invoke();
    }

    static double Read(SensorOption? option) => option?.Read() ?? double.NaN;

    void ReadAllOptions()
    {
        var cat = _catalog;
        var values = new double[4][];
        for (int m = 0; m < 4; m++)
            values[m] = Array.ConvertAll(cat.Options[m], o => o.Read());
        Options = new OptionValues(cat, values);
    }

    void Select()
    {
        _reselect = false;
        var cat = _catalog;
        for (int m = 0; m < 4; m++)
            _selected[m] = cat.Resolve((Metric)m, _settings.Sensors[m]);
    }

    public void Dispose()
    {
        if (_stop)
            return;
        _stop = true;
        _sensorWake.Set();
        _screenWake.Set();
        _firstReading.Set();
        // The screen thread sends the "off" packet on its way out; the sensor thread closes the library.
        if (_screenThread.IsAlive)
            _screenThread.Join(2000);
        if (_sensorThread.IsAlive)
            _sensorThread.Join(3000);
    }
}
