namespace JonsboD34Mon;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--probe")
            return SensorProbe.Run(args.Length > 1 ? args[1] : "probe.txt");

        // One instance: a second launch just asks the running one to show its window.
        using var instance = new Mutex(true, @"Local\JonsboD34Mon.Instance", out bool first);
        using var showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\JonsboD34Mon.Show");
        if (!first)
        {
            showRequest.Set();
            return 0;
        }

        ApplicationConfiguration.Initialize();
        using var app = new TrayApp(showRequest);
        Application.Run(app);
        GC.KeepAlive(instance);
        return 0;
    }
}
