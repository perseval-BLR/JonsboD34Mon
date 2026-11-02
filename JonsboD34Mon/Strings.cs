using System.Runtime.InteropServices;

namespace JonsboD34Mon;

/// <summary>UI text in Russian and English. The panel labels (TIME, CPU TEMP. …) stay English, as on the case.</summary>
internal static class L
{
    public static volatile bool En;

    static string T(string ru, string en) => En ? en : ru;

    public static string ScreenConnected => T("Экран подключён", "Screen connected");
    public static string ScreenOff => T("Экран выключен", "Screen off");
    public static string ScreenNotFound => T("Экран не найден", "Screen not found");
    public static string Autostart => T("Автозапуск", "Autostart");
    public static string Screen => T("Экран", "Screen");
    public static string Open => T("Открыть", "Open");
    public static string ShowOnScreen => T("Показ на экране", "Show on screen");
    public static string Fahrenheit => T("Фаренгейты", "Fahrenheit");
    public static string Exit => T("Выход", "Exit");
    public static string Quit => T("Выгрузить из памяти", "Quit and unload");
    public static string RefreshSensors => T("Обновить список датчиков", "Refresh sensor list");
    public static string SensorsLoading => T("Датчики ещё загружаются…", "Sensors are still loading…");
    public static string NoSensors => T("Подходящих датчиков нет", "No matching sensors");
    public static string AutostartFailed => T("Не удалось изменить автозапуск.", "Couldn't change autostart.");
    public static string LanguageButton => T("RU", "EN");
    public static string RunningForAnotherUser => T(
        "Jonsbo D34 Mon уже запущен у другого пользователя этого ПК, экран корпуса работает от него.",
        "Jonsbo D34 Mon is already running for another user of this PC; the case screen is driven from there.");

    /// <summary>Error codes from <see cref="SensorMonitor"/>; anything else is shown as is.</summary>
    public static string Error(string code) => code switch
    {
        "pawnio" => T("Нет драйвера PawnIO: без температуры CPU", "No PawnIO driver: no CPU temperature"),
        "nocputemp" => T("Температура CPU недоступна", "CPU temperature unavailable"),
        _ => code,
    };

    public static string OptionTitle(SensorOption o) =>
        o.Key.EndsWith("cores:max", StringComparison.Ordinal) ? T("Максимум по ядрам", "Highest core clock") : o.Title;

    [DllImport("kernel32")]
    static extern ushort GetUserDefaultUILanguage();

    /// <summary>Russian for Russian/Ukrainian/Belarusian Windows, English otherwise.</summary>
    public static bool SystemPrefersEnglish()
    {
        int primary = GetUserDefaultUILanguage() & 0x3FF;
        return primary is not (0x19 or 0x22 or 0x23);
    }
}
