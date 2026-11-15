using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace JonsboD34Mon;

/// <summary>
/// <c>JonsboD34Mon.exe --probe file.txt</c>: lists the sensors this PC offers, the defaults that would be picked,
/// and what a once-per-second update costs. No screen I/O; useful when someone reports "wrong value" or "--".
/// The report is opened before anything else and written as the run goes on: an unusable path fails at once, and
/// a sensor library that throws still leaves a file to send.
/// </summary>
internal static class SensorProbe
{
    public static int Run(string file)
    {
        StreamWriter writer;
        try
        {
            // Opened first, so a bad path costs no time and no crash dialog. Share read lets someone watch the
            // file while the run goes on, but nothing can rewrite it under us.
            writer = new StreamWriter(new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8)
            {
                AutoFlush = true,
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"--probe: cannot write \"{file}\": {e.Message}");
            return 2;
        }

        using (writer)
            return Run(writer);
    }

    static int Run(TextWriter log)
    {
        void Mem(string step)
        {
            using var me = Process.GetCurrentProcess();
            log.WriteLine($"mem {step}: private {me.PrivateMemorySize64 / 1048576.0:F1} MB, ws {me.WorkingSet64 / 1048576.0:F1} MB");
        }

        Mem("start");
        var sw = Stopwatch.StartNew();
        Computer? computer = null;
        IHardware[] cpus = [];
        try
        {
            computer = new Computer { IsCpuEnabled = true };
            computer.Open();
            cpus = computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu).ToArray();
            foreach (var cpu in cpus)
                cpu.Update();
        }
        catch (Exception e)
        {
            log.WriteLine($"open failed: {e.GetType().Name}: {e.Message}");
        }
        log.WriteLine($"open: {sw.ElapsedMilliseconds} ms, PawnIO service: {PawnIoInstalled()}");

        // Very old Windows or drivers have no adapter temperature: that is a normal answer, not a crash.
        GpuTemp? gpu = null;
        try { gpu = new GpuTemp(); }
        catch (Exception e) { log.WriteLine($"gpu enumeration failed: {e.GetType().Name}: {e.Message}"); }
        using var gpuScope = gpu;
        Mem("after open");

        try
        {
            foreach (var cpu in cpus)
            {
                log.WriteLine($"== {cpu.Name} | {cpu.Identifier}");
                foreach (var s in cpu.Sensors.OrderBy(s => s.SensorType))
                    log.WriteLine($"  {s.SensorType,-12} {s.Name,-30} {s.Value,10:F2}   {s.Identifier}");
            }
            foreach (var a in gpu?.Adapters ?? [])
                log.WriteLine($"== GPU (D3DKMT) {a.Name}: {GpuTemp.Read(a.Handle):F1} °C");
        }
        catch (Exception e)
        {
            log.WriteLine($"sensor listing failed: {e.GetType().Name}: {e.Message}");
        }

        var catalog = new SensorCatalog(cpus, gpu?.Adapters ?? []);
        for (int m = 0; m < 4; m++)
        {
            var o = catalog.Resolve((Metric)m, "");
            log.WriteLine($"{(Metric)m}: {catalog.Options[m].Length} options, default = {o?.Title} = {o?.Read():F1}");
        }

        // Thread CPU time advances in scheduler ticks (~15.6 ms), far coarser than one update: measured per tick
        // it always reads as zero, so it is summed over the whole loop.
        const int Ticks = 10;
        long cpuStart = CpuTicks(), wallStart = Stopwatch.GetTimestamp();
        for (int i = 0; i < Ticks; i++)
        {
            Thread.Sleep(1000);
            long w0 = Stopwatch.GetTimestamp();
            double cpuTemp = double.NaN, gpuTemp = double.NaN, cpuUsage = double.NaN, cpuFreq = double.NaN;
            try
            {
                foreach (var cpu in cpus)
                    cpu.Update();
                var values = Enumerable.Range(0, 4).Select(m => catalog.Resolve((Metric)m, "")?.Read() ?? double.NaN).ToArray();
                (cpuTemp, gpuTemp, cpuUsage, cpuFreq) = (values[0], values[1], values[2], values[3]);
            }
            catch (Exception e)
            {
                log.WriteLine($"tick {i}: read failed: {e.GetType().Name}: {e.Message}");
            }
            log.WriteLine($"tick {i}: wall {Stopwatch.GetElapsedTime(w0).TotalMilliseconds:F1} ms | " +
                          $"cpuT={cpuTemp:F1} gpuT={gpuTemp:F1} cpuU={cpuUsage:F1} cpuF={cpuFreq:F0}");
        }

        double seconds = Stopwatch.GetElapsedTime(wallStart).TotalSeconds;
        long cpuTicks = Math.Max(0, CpuTicks() - cpuStart);
        double cpuMs = cpuTicks / 10000.0;
        log.WriteLine(cpuTicks == 0
            ? $"cpu: below the 15.6 ms thread-timer resolution over {seconds:F0} s, so under {15.6 / seconds / 10:F2} % of one core"
            : $"cpu: {cpuMs:F0} ms over {seconds:F0} s = {cpuMs / seconds:F2} ms per second = {cpuMs / seconds / 10:F2} % of one core");

        Mem("end");
        // The settings the GC actually runs with (gen0 budget, heap limits): explains how memory grows over time.
        log.WriteLine("gc: " + string.Join(", ", GC.GetConfigurationVariables().Select(kv => $"{kv.Key}={kv.Value}")) +
                      $", gen0 collections {GC.CollectionCount(0)}");
        try { computer?.Close(); }
        catch (Exception) { } // shutting down anyway
        return 0;
    }

    static bool PawnIoInstalled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO");
        return key != null;
    }

    [DllImport("kernel32")] static extern bool GetThreadTimes(nint thread, out long creation, out long exit, out long kernel, out long user);
    [DllImport("kernel32")] static extern nint GetCurrentThread();

    static long CpuTicks()
    {
        GetThreadTimes(GetCurrentThread(), out _, out _, out long k, out long u);
        return k + u;
    }
}
