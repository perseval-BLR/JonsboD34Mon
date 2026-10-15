using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace JonsboD34Mon;

/// <summary>
/// A handful of key=value lines in %APPDATA%\JonsboD34Mon\settings.ini — or, for the portable version
/// (a "JonsboD34Mon.portable" file next to the exe), in settings.ini next to the exe.
/// </summary>
internal sealed class Settings
{
    public static readonly string Dir =File.Exists(Path.Combine(AppContext.BaseDirectory, "JonsboD34Mon.portable"))
        ? AppContext.BaseDirectory
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JonsboD34Mon");
    static readonly string FilePath = Path.Combine(Dir, "settings.ini");

    /// <summary>Selected option key per <see cref="Metric"/>; empty = automatic.</summary>
    public readonly string[] Sensors = ["", "", "", ""];
    public volatile bool Fahrenheit;
    public volatile bool ScreenOn = true;
    public bool Autostart;
    public bool DarkTheme = true;
    public bool English = L.SystemPrefersEnglish();
    public bool FirstRun { get; private set; }

    public static Settings Load()
    {
        var s = new Settings();
        try
        {
            if (!File.Exists(FilePath))
            {
                s.FirstRun = true;
                return s;
            }
            foreach (var line in File.ReadAllLines(FilePath))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;
                string key = line[..eq].Trim(), value = line[(eq + 1)..].Trim();
                switch (key)
                {
                    case "CpuTemp": s.Sensors[0] = value; break;
                    case "GpuTemp": s.Sensors[1] = value; break;
                    case "CpuUsage": s.Sensors[2] = value; break;
                    case "CpuFreq": s.Sensors[3] = value; break;
                    case "Fahrenheit": s.Fahrenheit = value == "1"; break;
                    case "ScreenOn": s.ScreenOn = value != "0"; break;
                    case "Autostart": s.Autostart = value == "1"; break;
                    case "Theme": s.DarkTheme = value != "light"; break;
                    case "Language": s.English = value == "en"; break;
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            // Write a temp file and swap it in, so a crash or power loss mid-write can't leave an empty ini.
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp,
                $"CpuTemp={Sensors[0]}\nGpuTemp={Sensors[1]}\nCpuUsage={Sensors[2]}\nCpuFreq={Sensors[3]}\n" +
                $"Fahrenheit={(Fahrenheit ? 1 : 0)}\nScreenOn={(ScreenOn ? 1 : 0)}\nAutostart={(Autostart ? 1 : 0)}\n" +
                $"Theme={(DarkTheme ? "dark" : "light")}\nLanguage={(English ? "en" : "ru")}\n");
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

/// <summary>
/// Autostart through Task Scheduler: the app needs admin rights (PawnIO driver), and a task with
/// "highest privileges" starts it at logon without a UAC prompt. No time limit, runs on battery.
/// </summary>
internal static class Autostart
{
    const string TaskName = "JonsboD34Mon";

    public static bool Set(bool enable, out string error)
    {
        error = "";
        if (!enable)
            return RunSchtasks($"/Delete /TN {TaskName} /F", out error) || !Exists();

        string user = WindowsIdentity.GetCurrent().Name;
        string exe = Environment.ProcessPath ?? Application.ExecutablePath;
        string xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <Triggers>
                <LogonTrigger><Enabled>true</Enabled><UserId>{SecurityElement.Escape(user)}</UserId><Delay>PT5S</Delay></LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{SecurityElement.Escape(user)}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>6</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec><Command>{SecurityElement.Escape(exe)}</Command></Exec>
              </Actions>
            </Task>
            """;
        string file = Path.Combine(Path.GetTempPath(), "JonsboD34Mon-task.xml");
        try
        {
            File.WriteAllText(file, xml, Encoding.Unicode);
            return RunSchtasks($"/Create /TN {TaskName} /XML \"{file}\" /F", out error);
        }
        finally
        {
            try { File.Delete(file); } catch (IOException) { }
        }
    }

    public static bool Exists() => RunSchtasks($"/Query /TN {TaskName}", out _);

    static bool RunSchtasks(string args, out string error)
    {
        error = "";
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            })!;
            error = p.StandardError.ReadToEnd().Trim();
            p.WaitForExit(10000);
            return p.ExitCode == 0;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            error = e.Message;
            return false;
        }
    }
}
