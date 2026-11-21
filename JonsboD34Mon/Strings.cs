using System.Runtime.InteropServices;

namespace JonsboD34Mon;

/// <summary>UI text in Russian, English and French. The panel labels (TIME, CPU TEMP. …) stay English, as on the case.</summary>
internal static class L
{
    public const int Ru = 0, En = 1, Fr = 2, Count = 3;

    public static volatile int Lang;

    static string T(string ru, string en, string fr) => Lang switch { Ru => ru, Fr => fr, _ => en };

    public static string ScreenConnected => T("Экран подключён", "Screen connected", "Écran connecté");
    public static string ScreenOff => T("Экран выключен", "Screen off", "Écran éteint");
    public static string ScreenNotFound => T("Экран не найден", "Screen not found", "Écran introuvable");
    public static string Autostart => T("Автозапуск", "Autostart", "Démarrage auto");
    public static string Screen => T("Экран", "Screen", "Écran");
    public static string Open => T("Открыть", "Open", "Ouvrir");
    public static string ShowOnScreen => T("Показ на экране", "Show on screen", "Afficher sur l'écran");
    public static string Fahrenheit => T("Фаренгейты", "Fahrenheit", "Fahrenheit");
    public static string Exit => T("Выход", "Exit", "Quitter");
    public static string Quit => T("Выгрузить из памяти", "Quit and unload", "Quitter et décharger");
    public static string RefreshSensors => T("Обновить список датчиков", "Refresh sensor list", "Actualiser les capteurs");
    public static string SensorsLoading => T("Датчики ещё загружаются…", "Sensors are still loading…", "Chargement des capteurs…");
    public static string NoSensors => T("Подходящих датчиков нет", "No matching sensors", "Aucun capteur adapté");
    public static string AutostartFailed => T("Не удалось изменить автозапуск.", "Couldn't change autostart.", "Impossible de modifier le démarrage auto.");
    public static string LanguageButton => T("RU", "EN", "FR");
    public static string RunningForAnotherUser => T(
        "Jonsbo D34 Mon уже запущен у другого пользователя этого ПК, экран корпуса работает от него.",
        "Jonsbo D34 Mon is already running for another user of this PC; the case screen is driven from there.",
        "Jonsbo D34 Mon est déjà lancé par un autre utilisateur de ce PC ; l'écran du boîtier est piloté depuis sa session.");

    /// <summary>Error codes from <see cref="SensorMonitor"/>; anything else is shown as is.</summary>
    public static string Error(string code) => code switch
    {
        "pawnio" => T("Нет драйвера PawnIO: без температуры CPU", "No PawnIO driver: no CPU temperature", "Pilote PawnIO absent : pas de température CPU"),
        "nocputemp" => T("Температура CPU недоступна", "CPU temperature unavailable", "Température CPU indisponible"),
        _ => code,
    };

    public static string OptionTitle(SensorOption o) =>
        o.Key.EndsWith("cores:max", StringComparison.Ordinal)
            ? T("Максимум по ядрам", "Highest core clock", "Fréquence du cœur le plus rapide")
            : o.Title;

    /// <summary>Settings.ini value for a language.</summary>
    public static string Code(int lang) => lang switch { Ru => "ru", Fr => "fr", _ => "en" };

    public static int FromCode(string code) => code switch { "en" => En, "fr" => Fr, _ => Ru };

    [DllImport("kernel32")]
    static extern ushort GetUserDefaultUILanguage();

    /// <summary>Russian for Russian/Ukrainian/Belarusian Windows, French for French Windows, English otherwise.</summary>
    public static int SystemDefault()
    {
        int primary = GetUserDefaultUILanguage() & 0x3FF;
        return primary switch { 0x19 or 0x22 or 0x23 => Ru, 0x0C => Fr, _ => En };
    }
}
