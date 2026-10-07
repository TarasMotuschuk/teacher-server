namespace TeacherClient.CrossPlatform.Localization;

internal static partial class CrossPlatformText
{
    public static string EditProgram => IsUk ? "Редагувати" : "Edit";

    public static string ClearPrograms => IsUk ? "Очистити список" : "Clear list";

    public static string ClearProgramsPrompt => IsUk
        ? "Видалити всі записи зі списку частих програм? Самі програми на учнівських ПК залишаться встановленими."
        : "Remove all frequent-program entries? Programs on student PCs will remain installed.";

    public static string ProgramFieldsRequired => IsUk ? "Вкажіть назву програми та команду запуску." : "Enter a program name and launch command.";

    public static string SelectOnlineCommandTargets => IsUk
        ? "Позначте хоча б один онлайн ПК прапорцем у колонці «Вибір». Команди виконуються лише на онлайн ПК."
        : "Check at least one online PC in the Select column. Commands run only on online PCs.";

    public static string AdministratorLaunchHint => IsUk
        ? "Адміністратор: запуск від служби LocalSystem у сесії учня без пароля та UAC. Використовується профіль SYSTEM, а не профіль учня. Потрібна оновлена служба агента."
        : "Administrator: launch through the LocalSystem service in the student's session without a password or UAC prompt. Uses the SYSTEM profile, not the student's profile. Requires the updated agent service.";
}
