using System.Runtime.InteropServices;

namespace JonsboD34Mon;

/// <summary>
/// Tells when Windows turns the monitors off and back on (power setting GUID_CONSOLE_DISPLAY_STATE),
/// through a hidden window of its own. The callback runs on the UI thread.
/// </summary>
internal sealed unsafe class DisplayWatcher : NativeWindow, IDisposable
{
    static readonly Guid ConsoleDisplayState = new("6fe69556-704a-47a0-8f24-c28d936fda47");
    const int WM_POWERBROADCAST = 0x218, PBT_POWERSETTINGCHANGE = 0x8013;

    [StructLayout(LayoutKind.Sequential)]
    struct PowerBroadcastSetting { public Guid PowerSetting; public uint DataLength; public uint Data; }

    readonly Action<bool> _changed;
    readonly nint _registration;

    /// <param name="changed">Gets true when the monitors are on (or dimmed), false when they are off.</param>
    public DisplayWatcher(Action<bool> changed)
    {
        _changed = changed;
        CreateHandle(new CreateParams());
        var setting = ConsoleDisplayState;
        _registration = Native.RegisterPowerSettingNotification(Handle, ref setting, Native.DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_POWERBROADCAST && m.WParam == PBT_POWERSETTINGCHANGE)
        {
            var s = (PowerBroadcastSetting*)m.LParam;
            if (s->PowerSetting == ConsoleDisplayState && s->DataLength >= sizeof(uint))
                _changed(s->Data != 0); // 0 off, 1 on, 2 dimmed
            m.Result = 1;
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_registration != 0)
            Native.UnregisterPowerSettingNotification(_registration);
        DestroyHandle();
    }
}
