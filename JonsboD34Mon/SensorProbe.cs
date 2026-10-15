using System.Diagnostics;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace JonsboD34Mon;

/// <summary>
/// <c>JonsboD34Mon.exe --probe file.txt</c>: lists the sensors this PC offers, the defaults that would be picked,
/// and what a once-per-second update costs. No screen I/O; useful when someone reports "wrong value" or "--".
/// </summary>
internal static class SensorProbe
{
    public static int Run(string file)
    {
        var log = new System.Text.StringBuilder();
        void Mem(string step)
        {
            using var me = Process.GetCurrentProcess();
            log.AppendLine($"mem {step}: private {me.PrivateMemorySize64 / 1048576.0:F1} MB, ws {me.WorkingSet64 / 1048576.0:F1} MB");
        }

        Mem("start");
        var sw = Stopwatch.StartNew();
        var computer = new Computer { IsCpuEnabled = true };
        computer.Open();
        var cpus = computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu).ToArray();
        foreach (var cpu in cpus)
            cpu.Update();
        log.AppendLine($"open: {sw.ElapsedMilliseconds} ms, PawnIO service: {Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO") != null}");
        using var gpu = new GpuTemp();
        Mem("after open");

        foreach (var cpu in cpus)
        {
            log.AppendLine($"== {cpu.Name} | {cpu.Identifier}");
            foreach (var s in cpu.Sensors.OrderBy(s => s.SensorType))
                log.AppendLine($"  {s.SensorType,-12} {s.Name,-30} {s.Value,10:F2}   {s.Identifier}");
        }
        foreach (var a in gpu.Adapters)
            log.AppendLine($"== GPU (D3DKMT) {a.Name}: {GpuTemp.Read(a.Handle):F1} °C");

        var cat = new SensorCatalog(cpus, gpu.Adapters);
        for (int m = 0; m < 4; m++)
        {
            var o = cat.Resolve((Metric)m, "");
            log.AppendLine($"{(Metric)m}: {cat.Options[m].Length} options, default = {o?.Title} = {o?.Read():F1}");
        }

        for (int i = 0; i < 5; i++)
        {
            Thread.Sleep(1000);
            long c0 = CpuTicks(), w0 = Stopwatch.GetTimestamp();
            foreach (var cpu in cpus)
                cpu.Update();
            var vals = Enumerable.Range(0, 4).Select(m => cat.Resolve((Metric)m, "")?.Read() ?? double.NaN).ToArray();
            log.AppendLine($"tick {i}: wall {Stopwatch.GetElapsedTime(w0).TotalMilliseconds:F1} ms, cpu {(CpuTicks() - c0) / 10000.0:F1} ms | " +
                           $"cpuT={vals[0]:F1} gpuT={vals[1]:F1} cpuU={vals[2]:F1} cpuF={vals[3]:F0}");
        }
        Mem("end");
        computer.Close();
        File.WriteAllText(file, log.ToString());
        return 0;
    }

    [DllImport("kernel32")] static extern bool GetThreadTimes(nint thread, out long creation, out long exit, out long kernel, out long user);
    [DllImport("kernel32")] static extern nint GetCurrentThread();

    static long CpuTicks()
    {
        GetThreadTimes(GetCurrentThread(), out _, out _, out long k, out long u);
        return k + u;
    }
}
