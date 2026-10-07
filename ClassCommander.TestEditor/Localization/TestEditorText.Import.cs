namespace ClassCommander.TestEditor.Localization;

internal static partial class TestEditorText
{
    public static string BatchMyTestCommand => IsUk ? "Пакетне перетворення MyTestX…" : "Batch convert MyTestX…";

    public static string BatchSourceTitle => IsUk ? "Виберіть файли MyTestX" : "Choose MyTestX files";

    public static string BatchOutputTitle => IsUk ? "Папка для пакетів .cctest та звіту" : "Folder for .cctest packages and report";

    public static string ImportReportTitle => IsUk ? "Звіт імпорту MyTestX" : "MyTestX import report";
    public static string ImportCancel => IsUk ? "Скасувати" : "Cancel";

    public static string ImportClose => IsUk ? "Закрити" : "Close";

    public static string ImportCancelled => IsUk ? "Перетворення скасовано. Готові пакети збережено." : "Conversion cancelled. Completed packages were kept.";

    public static string ImportLimitations => IsUk
        ? "Імпортуються питання, ключі відповідей і підтримувані зображення. Форматування, паролі, інструкції та спеціальні режими MyTestX не переносяться повністю. Перевірте тест і налаштуйте правила оцінювання та призначення перед використанням."
        : "Questions, answer keys and supported images are imported. Formatting, passwords, instructions and special MyTestX modes are not fully transferred. Review the test and configure grading and assignment rules before use.";

    public static string ImportProgress(int current, int total) => IsUk ? $"Обробка {current} із {total}…" : $"Processing {current} of {total}…";

    public static string ImportSuccess(int count, string path) => IsUk ? $"Готово: {count} питань → {path}" : $"Done: {count} questions → {path}";

    public static string ImportReportSaved(string path) => IsUk ? $"Звіт збережено: {path}" : $"Report saved: {path}";

    public static string ImportError(string? code) => (code, IsUk) switch
    {
        ("mtf-protected-or-invalid", true) => "Файл MTF захищений паролем відкриття або має невідомий формат. Експортуйте XML через MyTestX.",
        ("mtf-protected-or-invalid", false) => "The MTF file has an opening password or an unknown format. Export XML through MyTestX.",
        ("mtf-password-protected", true) => "Прямий імпорт захищених паролем тестів не підтримується. Експортуйте XML через MyTestX з потрібним паролем.",
        ("mtf-password-protected", false) => "Direct import of password-protected tests is unsupported. Export XML through MyTestX using the required password.",
        ("mtf-unsupported-version", true) => "Нативний імпорт перевірено для MyTestX 10.2.0.2. Для цієї версії файлу використайте експорт XML.",
        ("mtf-unsupported-version", false) => "Native import is verified for MyTestX 10.2.0.2. Use XML export for this file version.",
        ("mtf-invalid-data", true) => "Пошкоджена або непідтримувана структура MTF. Пакет не створено.",
        ("mtf-invalid-data", false) => "Damaged or unsupported MTF structure. No package was created.",
        ("mtf-unsupported-content", true) => "MTF містить непідтримуване мультимедіа. Використайте експорт XML та перевірте попередження.",
        ("mtf-unsupported-content", false) => "The MTF contains unsupported multimedia. Use XML export and review the warnings.",
        ("no-questions", true) => "Не знайдено питань у підтримуваній структурі MyTest XML. Пакет не створено.",
        ("no-questions", false) => "No questions found in the supported MyTest XML structure. No package was created.",
        ("unsupported-type", true) => "Непідтримуваний тип питання. Імпорт зупинено без заміни типу.",
        ("unsupported-type", false) => "Unsupported question type. Import stopped without substituting another type.",
        ("invalid-answer-key", true) => "Ключ відповіді відсутній, неповний або має непідтримуваний формат. Перевірте питання у MyTestX.",
        ("invalid-answer-key", false) => "An answer key is missing, incomplete or uses an unsupported format. Review the question in MyTestX.",
        ("review-required", true) => "Виявлено можливі втрати. Автоматичний пакет не створено. Відкрийте XML через одиночний імпорт, перегляньте попередження та виправте тест перед збереженням.",
        ("review-required", false) => "Possible data loss detected. No automatic package was created. Open the XML through single-file import, review warnings and correct the test before saving.",
        ("unsupported-file", true) => "Підтримуються файли .xml та файли MyTestX 10.2.0.2 (.mtf).",
        ("unsupported-file", false) => "Supported inputs are .xml files and MyTestX 10.2.0.2 files (.mtf).",
        (_, true) => "Не вдалося перетворити файл. Перевірте XML, зображення та доступ до папки призначення.",
        _ => "Could not convert the file. Check the XML, images and access to the destination folder.",
    };
}
