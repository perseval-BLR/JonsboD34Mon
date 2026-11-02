namespace JonsboD34Mon;

internal static class Program
{
    const string InstanceName = @"Global\JonsboD34Mon.Instance", ShowName = @"Local\JonsboD34Mon.Show";

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--probe")
            return SensorProbe.Run(args.Length > 1 ? args[1] : "probe.txt");

        ApplicationConfiguration.Initialize();

        // One instance per PC, not per session: two users' copies would take turns on the one case screen.
        // A second launch in the same session just asks the running one to show its window.
        using var instance = OpenInstance(out bool first);
        if (!first)
        {
            if (EventWaitHandle.TryOpenExisting(ShowName, out var show))
            {
                using (show)
                    show.Set();
            }
            else
            {
                L.En = Settings.Load().English;
                MessageBox.Show(L.RunningForAnotherUser, "Jonsbo D34 Mon", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return 0;
        }
        using var showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, ShowName);

        // A UI error must not pop up .NET's crash dialog over the desktop: log it and keep running.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogError(e.ExceptionObject as Exception);

        using var app = new TrayApp(showRequest);
        Application.Run(app);
        GC.KeepAlive(instance);
        return 0;
    }

    static Mutex? OpenInstance(out bool first)
    {
        try
        {
            return new Mutex(true, InstanceName, out first);
        }
        catch (UnauthorizedAccessException)
        {
            first = false; // created by another user's copy, whose security doesn't let us open it
            return null;
        }
    }

    /// <summary>Appends to error.log next to settings.ini; starts over once it passes 256 KB.</summary>
    internal static void LogError(Exception? e)
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
