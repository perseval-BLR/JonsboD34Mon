using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace JonsboD34Mon;

internal static unsafe class Native
{
    // kernel32
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool WriteFile(SafeFileHandle file, byte* buffer, int count, int* written, NativeOverlapped* overlapped);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool GetOverlappedResult(SafeFileHandle file, NativeOverlapped* overlapped, out int transferred, bool wait);

    [DllImport("kernel32")]
    public static extern bool CancelIoEx(SafeFileHandle file, NativeOverlapped* overlapped);

    [DllImport("kernel32")]
    public static extern nint GetCurrentProcess();

    [DllImport("kernel32")]
    public static extern bool SetProcessWorkingSetSize(nint process, nint min, nint max);

    public const uint GENERIC_WRITE = 0x40000000;
    public const uint FILE_SHARE_READ_WRITE = 3;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
    public const int ERROR_IO_PENDING = 997;

    // hid / setupapi
    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public nint Reserved;
    }

    [DllImport("hid.dll")]
    public static extern void HidD_GetHidGuid(out Guid guid);

    // These two return BOOLEAN (1 byte), not BOOL.
    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_GetPreparsedData(SafeFileHandle device, out nint data);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_FreePreparsedData(nint data);

    [DllImport("hid.dll")]
    public static extern int HidP_GetCaps(nint data, byte* caps);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
    public static extern nint SetupDiGetClassDevsW(ref Guid classGuid, nint enumerator, nint parent, int flags);

    [DllImport("setupapi.dll")]
    public static extern bool SetupDiEnumDeviceInterfaces(nint set, nint devInfo, ref Guid classGuid, int index, ref SP_DEVICE_INTERFACE_DATA data);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
    public static extern bool SetupDiGetDeviceInterfaceDetailW(nint set, ref SP_DEVICE_INTERFACE_DATA data, byte* detail, int size, out int required, nint devInfo);

    [DllImport("setupapi.dll")]
    public static extern bool SetupDiDestroyDeviceInfoList(nint set);

    public const int DIGCF_PRESENT = 0x2;
    public const int DIGCF_DEVICEINTERFACE = 0x10;
    public const int HIDP_STATUS_SUCCESS = 0x00110000;

    // user32 / dwm
    [DllImport("user32")]
    public static extern bool ReleaseCapture();

    [DllImport("user32")]
    public static extern nint SendMessageW(nint hwnd, int msg, nint wParam, nint lParam);

    [DllImport("user32")]
    public static extern bool DestroyIcon(nint icon);

    [DllImport("dwmapi")]
    public static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    public const int WM_NCLBUTTONDOWN = 0xA1;
    public const int HTCAPTION = 2;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWCP_ROUND = 2;

    /// <summary>Hands unused pages back to the OS (used after the window closes).</summary>
    public static void TrimWorkingSet()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1);
    }
}
