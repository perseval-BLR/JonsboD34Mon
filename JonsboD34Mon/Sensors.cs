using LibreHardwareMonitor.Hardware;

namespace JonsboD34Mon;

/// <summary>The four values the D34 screen shows (besides the clock).</summary>
internal enum Metric { CpuTemp, GpuTemp, CpuUsage, CpuFreq }

/// <summary>A choice for one metric. <see cref="Read"/> returns the current value (NaN if unavailable).</summary>
internal sealed record SensorOption(string Key, string Title, string Device, Func<double> Read);

/// <summary>
/// Sensors available on this PC, grouped into options per metric:
/// CPU from LibreHardwareMonitor (needs the PawnIO driver for temperatures and clocks),
/// GPU temperature from Windows (D3DKMT). Keys are stable across runs (LHM identifiers, adapter names).
/// </summary>
internal sealed class SensorCatalog
{
    public static readonly SensorCatalog Empty = new([], []);

    public readonly SensorOption[][] Options = new SensorOption[4][];

    public SensorCatalog(IReadOnlyList<IHardware> cpus, IReadOnlyList<GpuTemp.Adapter> gpus)
    {
        var temps = new List<SensorOption>();
        var loads = new List<SensorOption>();
        var clocks = new List<SensorOption>();
        foreach (var cpu in cpus)
        {
            var sensors = cpu.Sensors;
            temps.AddRange(sensors.Where(s => s.SensorType == SensorType.Temperature).Select(s => Option(s, cpu)));
            loads.AddRange(sensors.Where(s => s.SensorType == SensorType.Load).Select(s => Option(s, cpu)));

            // Per-core nominal clocks ("Core #1"…), not the "(Effective)" ones: the highest is what the CPU runs at.
            var cores = sensors.Where(s => s.SensorType == SensorType.Clock && s.Name.StartsWith("Core #", StringComparison.Ordinal)
                                           && !s.Name.Contains("Effective", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (cores.Length > 0)
            {
                string prefix = cpus.Count > 1 ? cpu.Identifier + ":" : "";
                clocks.Add(new(prefix + "cores:max", "Highest core clock", cpu.Name,
                               () => cores.Select(c => c.Value ?? float.NaN).Where(v => !float.IsNaN(v)).DefaultIfEmpty(float.NaN).Max()));
            }
            clocks.AddRange(sensors.Where(s => s.SensorType == SensorType.Clock && !s.Name.StartsWith("Bus", StringComparison.Ordinal))
                                   .Select(s => Option(s, cpu)));
        }
        Options[(int)Metric.CpuTemp] = [.. temps];
        Options[(int)Metric.CpuUsage] = [.. loads];
        Options[(int)Metric.CpuFreq] = [.. clocks];
        Options[(int)Metric.GpuTemp] = gpus.Select((a, i) =>
            new SensorOption($"gpu:{a.Name}#{gpus.Take(i).Count(b => b.Name == a.Name)}", a.Name, "", () => GpuTemp.Read(a.Handle))).ToArray();
    }

    static SensorOption Option(ISensor s, IHardware hw) =>
        new(s.Identifier.ToString(), s.Name, hw.Name, () => s.Value is float v ? v : double.NaN);

    /// <summary>The saved choice if it still exists, otherwise a sensible default for this hardware.</summary>
    public SensorOption? Resolve(Metric metric, string savedKey)
    {
        var o = Options[(int)metric];
        if (o.Length == 0)
            return null;
        return o.FirstOrDefault(x => x.Key == savedKey) ?? metric switch
        {
            // AMD: "Core (Tctl/Tdie)"; Intel: "CPU Package".
            Metric.CpuTemp => Find(o, "Tctl/Tdie") ?? Find(o, "Package") ?? o[0],
            Metric.CpuUsage => o.FirstOrDefault(x => x.Title == "CPU Total") ?? o[0],
            _ => o[0], // GPU: the first discrete adapter; clock: highest core
        };
    }

    static SensorOption? Find(SensorOption[] o, string text) =>
        o.FirstOrDefault(x => x.Title.Contains(text, StringComparison.OrdinalIgnoreCase));
}
