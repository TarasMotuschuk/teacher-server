using Teacher.Common.Contracts.Testing;
using Teacher.Common.Localization;

namespace ClassCommander.TestEditor.Localization;

internal static partial class TestEditorText
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

    public static string GroupsHeading => IsUk ? "Завдання" : "Questions";

    public static string EditorHeading => IsUk ? "Редагування питання" : "Edit question";

    public static string TestTitleLabel => IsUk ? "Назва тесту" : "Test title";

    public static string GroupTitleLabel => IsUk ? "Група" : "Group";

    public static string AddQuestion => IsUk ? "Додати питання" : "Add question";

    public static string DeleteQuestion => IsUk ? "Видалити" : "Delete";

    public static string MoveUp => IsUk ? "↑" : "↑";

    public static string MoveDown => IsUk ? "↓" : "↓";

    public static string ApplyChanges => IsUk ? "Зберегти завдання" : "Save question";

    public static string EmptyTestHint => IsUk
        ? "Новий тест створено. Додайте завдання через меню «Завдання» або панель команд."
        : "New test created. Add a question using the Questions menu or toolbar.";

    public static string SelectQuestionHint => IsUk
        ? "Оберіть питання зліва або додайте нове."
        : "Select a question on the left or add a new one.";

    public static string NoTestLoaded => IsUk
        ? "Тест ще не завантажено. Створіть новий, відкрийте .cctest або імпортуйте MyTest XML."
        : "No test loaded. Create a new test, open a .cctest package, or import MyTest XML.";

    public static string StatusReady => IsUk ? "Готово" : "Ready";

    public static string StatusNewTest => IsUk ? "Створено новий тест." : "Created a new test.";

    public static string StatusQuestionAdded => IsUk ? "Питання додано." : "Question added.";

    public static string StatusQuestionUpdated => IsUk ? "Питання оновлено." : "Question updated.";

    public static string StatusQuestionDeleted => IsUk ? "Питання видалено." : "Question deleted.";

    public static string PromptLabel => IsUk ? "Умова" : "Prompt";

    public static string DescriptionLabel => IsUk ? "Опис / підказка" : "Description / hint";

    public static string TypeLabel => IsUk ? "Тип" : "Type";

    public static string ScoreLabel => IsUk ? "Бал" : "Score";

    public static string RequiredLabel => IsUk ? "Обов’язкове" : "Required";

    public static string OptionsLabel => IsUk ? "Варіанти / відповідь" : "Options / answer";

    public static string AnswerKeyLabel => IsUk ? "Правильна відповідь" : "Answer key";

    public static string AddOption => IsUk ? "Додати варіант" : "Add option";

    public static string AddStatement => IsUk ? "Додати твердження" : "Add statement";

    public static string AddEntry => IsUk ? "Додати поле" : "Add entry";

    public static string TrueLabel => IsUk ? "Так" : "True";

    public static string FalseLabel => IsUk ? "Ні" : "False";

    public static string MatchingLeftHint => IsUk ? "Лівий стовпчик (по рядку)" : "Left column (one per line)";

    public static string MatchingRightHint => IsUk ? "Правий стовпчик (по рядку)" : "Right column (one per line)";

    public static string PlaceholderLabel => IsUk ? "Підказка в полі" : "Placeholder";

    public static string AcceptedTextsHint => IsUk ? "Прийнятні відповіді (по рядку)" : "Accepted answers (one per line)";

    public static string CaseSensitiveLabel => IsUk ? "Враховувати регістр" : "Case sensitive";

    public static string SelectImageArea => IsUk ? "Виділити область (прямокутник)" : "Select an area (rectangle)";

    public static string SelectImagePoint => IsUk ? "Точка з допустимим відхиленням" : "Point with allowed deviation";

    public static string ImageAreaHint => IsUk
        ? "Затисніть ліву кнопку миші й обведіть правильну область на зображенні, наприклад вкладку «Макет». Будь-яке клацання учня всередині синьої області буде правильним. Щоб замінити область, обведіть нову."
        : "Hold the left mouse button and drag around the correct image area, such as the Layout tab. Any student click inside the blue area will be correct. Draw a new area to replace it.";

    public static string SelectCorrectArea => IsUk ? "Обведіть правильну область на зображенні." : "Drag around the correct area on the image.";

    public static string ImageAreaSelected => IsUk
        ? "Правильну область позначено. Учень може клацнути будь-де всередині неї."
        : "The correct area is marked. The student may click anywhere inside it.";

    public static string ImagePointHint => IsUk
        ? "Виберіть зображення та клацніть по правильній точці. Синя область показує допустиме відхилення у пікселях оригінального зображення."
        : "Choose an image and click the correct point. The blue area shows the allowed deviation in original image pixels.";

    public static string ChooseImage => IsUk ? "Вибрати зображення…" : "Choose image…";

    public static string ImageFiles => IsUk ? "Зображення" : "Images";

    public static string ImageNotSelected => IsUk ? "Зображення ще не вибрано." : "No image selected yet.";

    public static string SelectCorrectPoint => IsUk ? "Клацніть по правильній точці на зображенні." : "Click the correct point on the image.";

    public static string PointTolerance => IsUk ? "Допустиме відхилення (пікселі)" : "Allowed deviation (pixels)";

    public static string PointSelected(int x, int y) => IsUk ? $"Вибрано точку: X = {x}, Y = {y}." : $"Selected point: X = {x}, Y = {y}.";

    public static string ImagePointIncomplete => IsUk
        ? "Для кожного питання «Область на зображенні» виберіть зображення та позначте правильну область або точку перед збереженням."
        : "Choose an image and mark the correct area or point for each Image area question before saving.";

    public static string SourceWordLabel => IsUk ? "Літери / джерело" : "Letters / source";

    public static string TargetWordLabel => IsUk ? "Правильне слово" : "Target word";

    public static string UntitledTest => IsUk ? "Новий тест" : "Untitled test";

    public static string DefaultGroupTitle => IsUk ? "Основна група" : "Main group";

    public static string OpenCctestTitle => IsUk ? "Відкрити пакет тесту" : "Open test package";

    public static string SaveCctestTitle => IsUk ? "Зберегти пакет тесту" : "Save test package";

    public static string ImportXmlTitle => IsUk ? "Імпортувати MyTest XML" : "Import MyTest XML";

    public static string ErrorTitle => IsUk ? "Помилка" : "Error";

    public static string NothingToSave => IsUk ? "Немає тесту для збереження." : "There is no test to save.";

    public static string ChooseTypeTitle => IsUk ? "Тип питання" : "Question type";

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
        QuestionType.ImagePoint => IsUk ? "Область на зображенні" : "Image area",
        QuestionType.LetterOrdering => IsUk ? "Слово з літер" : "Letter ordering",
        _ => type.ToString(),
    };
}
