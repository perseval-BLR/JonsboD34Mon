using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace JonsboD34Mon;

/// <summary>
/// The JONSBO case screen: a plain HID device (VID 0x5131, PID 0x2007) that takes 64-byte output reports.
/// Talks to it through the standard Windows HID stack, no vendor USB library needed.
/// </summary>
internal sealed unsafe class CaseDisplay : IDisposable
{
    const string DeviceId = "vid_5131&pid_2007";
    const int PayloadSize = 64;
    const int RetryMs = 3000;

    sealed class Device(SafeFileHandle handle, int reportLength) : IDisposable
    {
        public readonly SafeFileHandle Handle = handle;
        public readonly byte[] Report = GC.AllocateArray<byte>(reportLength, pinned: true);
        public readonly ManualResetEvent Done = new(false);

        public void Dispose()
        {
            Handle.Dispose();
            Done.Dispose();
        }
    }

    readonly List<Device> _devices = [];
    long _nextAttempt;

    public bool IsConnected => _devices.Count > 0;

    /// <summary>Sends a 64-byte payload to every connected screen; reconnects at most every 3 s when none is open.</summary>
    public bool Send(ReadOnlySpan<byte> payload)
    {
        if (_devices.Count == 0)
        {
            long now = Environment.TickCount64;
            if (now < _nextAttempt)
                return false;
            _nextAttempt = now + RetryMs;
            Open();
        }

        bool sent = false;
        for (int i = _devices.Count - 1; i >= 0; i--)
        {
            if (Write(_devices[i], payload))
            {
                sent = true;
            }
            else
            {
                _devices[i].Dispose();
                _devices.RemoveAt(i);
                // A screen that just worked is usually back at once (USB hiccup, resume): don't wait 3 s.
                _nextAttempt = 0;
            }
        }
        return sent;
    }

    static bool Write(Device d, ReadOnlySpan<byte> payload)
    {
        byte[] report = d.Report;
        Array.Clear(report);
        // report[0] is the report ID (0: the device doesn't use IDs).
        payload[..Math.Min(payload.Length, report.Length - 1)].CopyTo(report.AsSpan(1));

        d.Done.Reset();
        NativeOverlapped ov = default;
        ov.EventHandle = d.Done.SafeWaitHandle.DangerousGetHandle();
        fixed (byte* p = report)
        {
            if (Native.WriteFile(d.Handle, p, report.Length, null, &ov))
                return true;
            if (Marshal.GetLastPInvokeError() != Native.ERROR_IO_PENDING)
                return false;
            if (!d.Done.WaitOne(500))
            {
                Native.CancelIoEx(d.Handle, &ov);
                Native.GetOverlappedResult(d.Handle, &ov, out _, true);
                return false;
            }
            return Native.GetOverlappedResult(d.Handle, &ov, out _, false);
        }
    }

    void Open()
    {
        Native.HidD_GetHidGuid(out Guid hid);
        nint set = Native.SetupDiGetClassDevsW(ref hid, 0, 0, Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);
        if (set == -1)
            return;
        try
        {
            var data = new Native.SP_DEVICE_INTERFACE_DATA { cbSize = sizeof(Native.SP_DEVICE_INTERFACE_DATA) };
            byte* detail = stackalloc byte[2048];
            for (int i = 0; Native.SetupDiEnumDeviceInterfaces(set, 0, ref hid, i, ref data); i++)
            {
                *(int*)detail = IntPtr.Size == 8 ? 8 : 6; // sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA_W)
                if (!Native.SetupDiGetDeviceInterfaceDetailW(set, ref data, detail, 2048, out _, 0))
                    continue;
                string path = new((char*)(detail + 4));
                if (!path.Contains(DeviceId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var handle = Native.CreateFileW(path, Native.GENERIC_WRITE, Native.FILE_SHARE_READ_WRITE, 0,
                                                Native.OPEN_EXISTING, Native.FILE_FLAG_OVERLAPPED, 0);
                if (handle.IsInvalid)
                {
                    handle.Dispose();
                    continue;
                }
                int length = OutputReportLength(handle);
                if (length < 2)
                {
                    handle.Dispose();
                    continue;
                }
                _devices.Add(new Device(handle, length));
            }
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(set);
        }
    }

    static int OutputReportLength(SafeFileHandle handle)
    {
        if (!Native.HidD_GetPreparsedData(handle, out nint data))
            return 0;
        try
        {
            byte* caps = stackalloc byte[64]; // HIDP_CAPS
            return Native.HidP_GetCaps(data, caps) == Native.HIDP_STATUS_SUCCESS ? *(ushort*)(caps + 6) : 0;
        }
        finally
        {
            Native.HidD_FreePreparsedData(data);
        }
    }

    public void Dispose()
    {
        foreach (var d in _devices)
            d.Dispose();
        _devices.Clear();
    }

    // ---- Protocol -------------------------------------------------------------------------------

    /// <summary>
    /// Data packet: [0x00, 0x01, 0x02, v0..v32]. The D34 shows the clock (v22..v29), CPU temp (v0, v1, unit v2),
    /// CPU usage (v3), CPU clock in MHz split into hundreds/remainder (v6, v7) and GPU temp (v10, v11, unit v12).
    /// Fields it doesn't show (power, voltage, fans, RAM) are left at zero.
    /// </summary>
    public static void BuildPacket(Span<byte> d, Snapshot s, DateTime t, bool fahrenheit)
    {
        d.Clear();
        d[1] = 1;
        d[2] = 2;
        Span<byte> v = d[3..];

        PutTemp(v, 0, s.CpuTemp, fahrenheit);
        v[3] = Byte(Math.Round(s.CpuUsage));
        if (!double.IsNaN(s.CpuFreqMhz))
        {
            int mhz = (int)s.CpuFreqMhz;
            v[6] = Byte(mhz / 100);
            v[7] = Byte(mhz % 100);
        }
        PutTemp(v, 10, s.GpuTemp, fahrenheit);

        v[22] = Byte(t.Year / 100);
        v[23] = Byte(t.Year % 100);
        v[24] = Byte(t.Month);
        v[25] = Byte(t.Day);
        v[26] = Byte(t.Hour);
        v[27] = Byte(t.Minute);
        v[28] = Byte(t.Second);
        v[29] = Byte((int)t.DayOfWeek);
    }

    /// <summary>The command the vendor app sends on exit (screen goes to its idle state).</summary>
    public static void BuildOffPacket(Span<byte> d)
    {
        d.Clear();
        d[1] = 0x0F;
    }

    static void PutTemp(Span<byte> v, int at, double celsius, bool fahrenheit)
    {
        v[at + 2] = (byte)(fahrenheit ? 1 : 0);
        if (double.IsNaN(celsius))
            return;
        double t = fahrenheit ? celsius * 1.8 + 32 : celsius;
        double whole = Math.Floor(t);
        v[at] = Byte(whole);
        v[at + 1] = Byte(Math.Min(99, Math.Round((t - whole) * 100)));
    }

    static byte Byte(double x) => double.IsNaN(x) ? (byte)0 : (byte)Math.Clamp(x, 0, 255);
}
