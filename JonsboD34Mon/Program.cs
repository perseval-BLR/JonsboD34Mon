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

        // A UI error must not pop up .NET's crash dialog over the desktop: log it and keep running.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogError(e.ExceptionObject as Exception);

        ApplicationConfiguration.Initialize();
        using var app = new TrayApp(showRequest);
        Application.Run(app);
        GC.KeepAlive(instance);
        return 0;
    }

    /// <summary>Appends to error.log next to settings.ini; starts over once it passes 256 KB.</summary>
    static void LogError(Exception? e)
    {
        try
        {
            string file = Path.Combine(Settings.Dir, "error.log");
            Directory.CreateDirectory(Settings.Dir);
            if (File.Exists(file) && new FileInfo(file).Length > 256 * 1024)
                File.Delete(file);
            File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} v{Application.ProductVersion}\n{e}\n\n");
        }
        catch (Exception) { } // nowhere left to report it
    }
}
