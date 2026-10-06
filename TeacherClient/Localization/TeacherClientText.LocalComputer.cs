namespace TeacherClient.Localization;

internal static partial class TeacherClientText
{
    public static string LocalComputerLockProtected => IsUk
        ? "ПК учителя захищено від блокування. Розблокування залишається доступним."
        : "The teacher PC is protected from locking. Unlocking remains available.";

    public static string LocalComputerLockSkipped(int count) => IsUk
        ? $"Пропущено власний ПК учителя: {count}."
        : $"Skipped the teacher's own PC: {count}.";
}
