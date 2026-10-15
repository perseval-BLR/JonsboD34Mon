using System.Runtime.InteropServices;

namespace JonsboD34Mon;

/// <summary>
/// GPU temperature straight from Windows (WDDM 2.4+): the same D3DKMT query Task Manager uses.
/// Works for NVIDIA, AMD and Intel discrete GPUs without vendor libraries or drivers of our own.
/// Integrated GPUs usually report no temperature and are skipped.
/// </summary>
internal sealed unsafe class GpuTemp : IDisposable
{
    public sealed record Adapter(uint Handle, string Name, long Luid);

    const int KMTQAITYPE_ADAPTERREGISTRYINFO = 8;
    const int KMTQAITYPE_ADAPTERPERFDATA = 62;

    [StructLayout(LayoutKind.Sequential)]
    struct AdapterInfo { public uint Handle; public uint LuidLow; public int LuidHigh; public uint NumSources; public int PrecisePresent; }

    [StructLayout(LayoutKind.Sequential)]
    struct EnumAdapters2 { public uint Count; public AdapterInfo* Adapters; }

    [StructLayout(LayoutKind.Sequential)]
    struct QueryAdapterInfo { public uint Adapter; public int Type; public void* Data; public uint Size; }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    struct PerfData { [FieldOffset(0)] public uint PhysicalAdapterIndex; [FieldOffset(56)] public uint Temperature; }

    [StructLayout(LayoutKind.Sequential)]
    struct CloseAdapter { public uint Adapter; }

    [DllImport("gdi32")] static extern int D3DKMTEnumAdapters2(EnumAdapters2* e);
    [DllImport("gdi32")] static extern int D3DKMTQueryAdapterInfo(QueryAdapterInfo* q);
    [DllImport("gdi32")] static extern int D3DKMTCloseAdapter(CloseAdapter* c);

    readonly List<uint> _allHandles = [];

    /// <summary>Discrete adapters that report a temperature.</summary>
    public IReadOnlyList<Adapter> Adapters { get; }

    public GpuTemp()
    {
        var found = new List<Adapter>();
        var e = new EnumAdapters2();
        if (D3DKMTEnumAdapters2(&e) == 0 && e.Count > 0)
        {
            var infos = new AdapterInfo[e.Count];
            fixed (AdapterInfo* p = infos)
            {
                e.Adapters = p;
                if (D3DKMTEnumAdapters2(&e) == 0)
                {
                    for (int i = 0; i < e.Count; i++)
                    {
                        uint h = p[i].Handle;
                        _allHandles.Add(h);
                        if (double.IsNaN(Read(h)))
                            continue;
                        long luid = ((long)p[i].LuidHigh << 32) | p[i].LuidLow;
                        found.Add(new Adapter(h, Name(h), luid));
                    }
                }
            }
        }
        Adapters = found;
    }

    /// <summary>°C, or NaN when the adapter doesn't report a temperature.</summary>
    public static double Read(uint adapter)
    {
        var perf = new PerfData();
        var q = new QueryAdapterInfo { Adapter = adapter, Type = KMTQAITYPE_ADAPTERPERFDATA, Data = &perf, Size = (uint)sizeof(PerfData) };
        if (D3DKMTQueryAdapterInfo(&q) != 0 || perf.Temperature == 0)
            return double.NaN;
        return perf.Temperature / 10.0; // deci-Celsius
    }

    static string Name(uint adapter)
    {
        const int Chars = 260;
        char* info = stackalloc char[Chars * 4]; // D3DKMT_ADAPTERREGISTRYINFO: 4 × WCHAR[260]
        var q = new QueryAdapterInfo { Adapter = adapter, Type = KMTQAITYPE_ADAPTERREGISTRYINFO, Data = info, Size = Chars * 4 * sizeof(char) };
        if (D3DKMTQueryAdapterInfo(&q) != 0)
            return "GPU";
        string s = new(info);
        return s.Length > 0 ? s : "GPU";
    }

    public void Dispose()
    {
        foreach (uint h in _allHandles)
        {
            var c = new CloseAdapter { Adapter = h };
            D3DKMTCloseAdapter(&c);
        }
        _allHandles.Clear();
    }
}
