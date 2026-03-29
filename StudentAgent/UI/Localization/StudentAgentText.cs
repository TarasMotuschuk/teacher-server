using Teacher.Common.Localization;

namespace StudentAgent.UI.Localization;

internal static class StudentAgentText
{
    public static UiLanguage CurrentLanguage { get; private set; } = UiLanguageExtensions.GetDefault();

    public static void SetLanguage(UiLanguage language)
    {
        CurrentLanguage = language.Normalize();
    }

    public static bool IsUk => CurrentLanguage == UiLanguage.Ukrainian;

    public static string AgentName => "StudentAgent";
    public static string About => IsUk ? "Про програму" : "About";
    public static string Settings => IsUk ? "Налаштування" : "Settings";
    public static string Logs => IsUk ? "Логи" : "Logs";
    public static string Exit => IsUk ? "Вийти" : "Exit";
    public static string TrayBalloon => IsUk ? "StudentAgent працює в системному треї." : "StudentAgent is running in the system tray.";
    public static string EnterPasswordTitle => IsUk ? "Введіть пароль" : "Enter password";
    public static string EnterPasswordPrompt => IsUk ? "Введіть пароль адміністратора StudentAgent:" : "Enter the StudentAgent admin password:";
    public static string Ok => "OK";
    public static string Cancel => IsUk ? "Скасувати" : "Cancel";
    public static string AboutTitle => IsUk ? "Про StudentAgent" : "About StudentAgent";
    public static string AboutDescription => IsUk ? "StudentAgent — це учнівський сервіс для класу." +
        "Він працює в системному треї Windows із захищеним доступом до налаштувань та логів." : "StudentAgent is the student-side classroom service. It exposes a visible, authorized management API and runs in the Windows system tray with protected settings and logs access.";
    public static string Version => IsUk ? "Версія:" : "Version:";
    public static string Close => IsUk ? "Закрити" : "Close";
    public static string LogsTitle => IsUk ? "Логи StudentAgent" : "StudentAgent Logs";
    public static string Refresh => IsUk ? "Оновити" : "Refresh";
    public static string OpenLogFolder => IsUk ? "Відкрити папку логів" : "Open log folder";
    public static string SettingsTitle => IsUk ? "Налаштування StudentAgent" : "StudentAgent Settings";
    public static string SharedSecret => IsUk ? "Спільний секрет" : "Shared secret";
    public static string NewPassword => IsUk ? "Новий пароль" : "New password";
    public static string ConfirmPassword => IsUk ? "Підтвердіть пароль" : "Confirm password";
    public static string ClearLogs => IsUk ? "Очистити логи" : "Clear logs";
    public static string Save => IsUk ? "Зберегти" : "Save";
    public static string Language => IsUk ? "Мова" : "Language";
    public static string StudentAgentUiError => IsUk ? "Помилка інтерфейсу StudentAgent" : "StudentAgent UI Error";
    public static string StudentAgentFatalError => IsUk ? "Критична помилка StudentAgent" : "StudentAgent Fatal Error";
    public static string StartupError => IsUk ? "Помилка запуску StudentAgent" : "StudentAgent Startup Error";
    public static string StartupFailed => IsUk ? "StudentAgent не вдалося запустити." : "StudentAgent failed to start.";
    public static string StartupDetailsWritten => IsUk ? "Деталі записано до:" : "Details were written to:";
    public static string ExitDenied => IsUk ? "Вихід заборонено" : "Exit denied";
    public static string OnlyAdminCanClose => IsUk ? "Лише адміністратор Windows може закрити StudentAgent." : "Only a Windows administrator can close StudentAgent.";
    public static string AccessDenied => IsUk ? "Доступ заборонено" : "Access denied";
    public static string InvalidPassword => IsUk ? "Неправильний пароль." : "Invalid password.";
    public static string Confirm => IsUk ? "Підтвердження" : "Confirm";
    public static string ClearAllLogsPrompt => IsUk ? "Очистити всі логи StudentAgent?" : "Clear all StudentAgent logs?";
    public static string LogsCleared => IsUk ? "Логи очищено." : "Logs cleared.";
    public static string SharedSecretRequired => IsUk ? "Спільний секрет є обов'язковим." : "Shared secret is required.";
    public static string PasswordsMismatch => IsUk ? "Паролі не збігаються." : "Passwords do not match.";
    public static string Validation => IsUk ? "Перевірка" : "Validation";
    public static string SettingsSaved => IsUk ? "Налаштування збережено." : "Settings saved.";
    public static string ProtectedMenuAccessDeniedLog => IsUk ? "У доступі до захищеного меню відмовлено через неправильний пароль." : "Protected menu access denied because of an invalid password.";
    public static string ExitDeniedBecauseNotAdminLog => IsUk ? "Вихід заборонено, тому що поточний користувач не адміністратор." : "Exit denied because current user is not an administrator.";
    public static string BrowserUsageForbiddenTitle => IsUk ? "Браузер заблоковано" : "Browser blocked";
    public static string BrowserUsageForbiddenMessage => IsUk ? "Використання браузера заборонене вчителем до кінця уроку." : "Browser usage is forbidden by the teacher until the end of the lesson.";
    public static string BrowserWillCloseIn(int seconds) => IsUk ? $"Браузер буде закрито через {seconds} с." : $"The browser will be closed in {seconds} s.";
    public static string BrowserLockEnabledLog => IsUk ? "Викладач увімкнув блокування браузера." : "Teacher enabled browser lock.";
    public static string BrowserLockDisabledLog => IsUk ? "Викладач вимкнув блокування браузера." : "Teacher disabled browser lock.";
    public static string BrowserLockKilledBrowsersLog(int count) => IsUk ? $"Завершено браузерів: {count}." : $"Closed browser processes: {count}.";
    public static string InputLockEnabledLog => IsUk ? "Викладач увімкнув блокування клавіатури і миші." : "Teacher enabled keyboard and mouse lock.";
    public static string InputLockDisabledLog => IsUk ? "Викладач вимкнув блокування клавіатури і миші." : "Teacher disabled keyboard and mouse lock.";
    public static string InputLockTitle => IsUk ? "Ввід заблоковано" : "Input blocked";
    public static string InputLockMessage => IsUk ? "Клавіатуру і мишу тимчасово заблоковано викладачем до кінця уроку." : "Keyboard and mouse access has been temporarily blocked by the teacher until the end of the lesson.";
    public static string InputLockFooter => IsUk ? "Тільки викладач може зняти це блокування." : "Only the teacher can remove this lock.";
}
