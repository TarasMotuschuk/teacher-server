using Teacher.Common.Contracts.Testing;
using Teacher.Common.Localization;

namespace ClassCommander.TestEditor.Localization;

internal static class TestEditorText
{
    public static UiLanguage Language { get; set; } = UiLanguageExtensions.GetDefault();

    private static bool IsUk => Language == UiLanguage.Ukrainian;

    public static string WindowTitle => IsUk ? "ClassCommander — Редактор тестів" : "ClassCommander Test Editor";

    public static string FileMenu => IsUk ? "_Файл" : "_File";

    public static string NewCommand => IsUk ? "Новий тест" : "New test";

    public static string OpenCommand => IsUk ? "Відкрити .cctest…" : "Open .cctest…";

    public static string SaveCommand => IsUk ? "Зберегти" : "Save";

    public static string SaveAsCommand => IsUk ? "Зберегти як…" : "Save As…";

    public static string ImportMyTestCommand => IsUk ? "Імпортувати MyTest XML…" : "Import MyTest XML…";

    public static string LanguageMenu => IsUk ? "_Мова" : "_Language";

    public static string English => "English";

    public static string Ukrainian => "Українська";

    public static string GroupsHeading => IsUk ? "Групи та питання" : "Groups and questions";

    public static string PreviewHeading => IsUk ? "Перегляд питання" : "Question preview";

    public static string NoTestLoaded => IsUk ? "Тест ще не завантажено. Відкрийте .cctest або імпортуйте MyTest XML." : "No test loaded. Open a .cctest package or import a MyTest XML file.";

    public static string SelectQuestionHint => IsUk ? "Оберіть питання зліва, щоб переглянути його." : "Select a question on the left to preview it.";

    public static string StatusReady => IsUk ? "Готово" : "Ready";

    public static string PromptLabel => IsUk ? "Умова" : "Prompt";

    public static string TypeLabel => IsUk ? "Тип" : "Type";

    public static string ScoreLabel => IsUk ? "Бал" : "Score";

    public static string OptionsLabel => IsUk ? "Варіанти / взаємодія" : "Options / interaction";

    public static string AnswerKeyLabel => IsUk ? "Правильна відповідь" : "Answer key";

    public static string AssetsLabel => IsUk ? "Ресурси" : "Assets";

    public static string WarningsLabel => IsUk ? "Попередження імпорту" : "Import warnings";

    public static string UntitledTest => IsUk ? "Новий тест" : "Untitled test";

    public static string OpenCctestTitle => IsUk ? "Відкрити пакет тесту" : "Open test package";

    public static string SaveCctestTitle => IsUk ? "Зберегти пакет тесту" : "Save test package";

    public static string ImportXmlTitle => IsUk ? "Імпортувати MyTest XML" : "Import MyTest XML";

    public static string CctestFilter => IsUk ? "Пакети ClassCommander (*.cctest)|*.cctest" : "ClassCommander packages (*.cctest)|*.cctest";

    public static string XmlFilter => IsUk ? "MyTest XML (*.xml)|*.xml" : "MyTest XML (*.xml)|*.xml";

    public static string ErrorTitle => IsUk ? "Помилка" : "Error";

    public static string NothingToSave => IsUk ? "Немає тесту для збереження." : "There is no test to save.";

    public static string StatusLoaded(string title, int questionCount) => IsUk
        ? $"Завантажено «{title}» ({questionCount} питань)"
        : $"Loaded “{title}” ({questionCount} questions)";

    public static string StatusSaved(string path) => IsUk
        ? $"Збережено: {path}"
        : $"Saved: {path}";

    public static string StatusImported(string title, int warningCount) => IsUk
        ? $"Імпортовано «{title}»" + (warningCount > 0 ? $" (попереджень: {warningCount})" : string.Empty)
        : $"Imported “{title}”" + (warningCount > 0 ? $" ({warningCount} warnings)" : string.Empty);

    public static string GroupTitle(string title, int count) => IsUk
        ? $"{title} ({count})"
        : $"{title} ({count})";

    public static string TestMeta(string publicId, int version, int questionCount, int groupCount) => IsUk
        ? $"ID: {publicId} · версія {version} · груп: {groupCount} · питань: {questionCount}"
        : $"ID: {publicId} · version {version} · groups: {groupCount} · questions: {questionCount}";

    public static string QuestionTypeName(QuestionType type) => type switch
    {
        QuestionType.SingleChoice => IsUk ? "Один варіант" : "Single choice",
        QuestionType.MultipleChoice => IsUk ? "Кілька варіантів" : "Multiple choice",
        QuestionType.Ordering => IsUk ? "Упорядкування" : "Ordering",
        QuestionType.Matching => IsUk ? "Відповідність" : "Matching",
        QuestionType.TrueFalseGroup => IsUk ? "Так / Ні (група)" : "True / false group",
        QuestionType.NumericInputGroup => IsUk ? "Числове введення" : "Numeric input group",
        QuestionType.TextInput => IsUk ? "Текстове введення" : "Text input",
        QuestionType.ImagePoint => IsUk ? "Точка на зображенні" : "Image point",
        QuestionType.LetterOrdering => IsUk ? "Слово з літер" : "Letter ordering",
        _ => type.ToString(),
    };
}
