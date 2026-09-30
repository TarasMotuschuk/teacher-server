namespace TeacherClient.CrossPlatform.Localization;

internal static partial class CrossPlatformText
{
    public static string TestingLaunchAssignment => IsUk ? "Завдання для запуску" : "Assignment to launch";

    public static string TestingChooseStudents => IsUk ? "Вибрати учнів і запустити…" : "Choose students and start…";

    public static string TestingRecipientsHint => IsUk
        ? "Позначте один або декілька ПК. Учні самі введуть прізвище та ім’я перед початком тесту."
        : "Select one or more PCs. Students will enter their surname and first name before starting.";

    public static string TestingSelectAllRecipients => IsUk ? "Вибрати всі ПК" : "Select all PCs";

    public static string TestingLaunchNow => IsUk ? "Запустити тест" : "Start test";

    public static string TestingSelectedCount(int count) => IsUk ? $"Вибрано ПК: {count}" : $"Selected PCs: {count}";

    public static string TestingExitCodes => IsUk ? "Коди вчителя для виходу…" : "Teacher exit codes…";

    public static string TestingExitPin(string pin) => IsUk
        ? $"Код учителя для дострокового виходу: {pin}. Збережіть його до завершення тестування."
        : $"Teacher code for early exit: {pin}. Keep it until testing is finished.";
}
