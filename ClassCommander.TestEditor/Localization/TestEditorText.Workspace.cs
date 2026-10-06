namespace ClassCommander.TestEditor.Localization;

internal static partial class TestEditorText
{
    public static string QuestionsMenu => IsUk ? "_Завдання" : "_Questions";

    public static string TestParametersMenu => IsUk ? "_Параметри тесту" : "_Test parameters";

    public static string HelpMenu => IsUk ? "_Довідка" : "_Help";

    public static string TestSettings => IsUk ? "Заголовок і опис…" : "Title and description…";

    public static string ManageGroups => IsUk ? "Редактор груп…" : "Group editor…";

    public static string DuplicateQuestion => IsUk ? "Дублювати завдання" : "Duplicate question";

    public static string ResetQuestion => IsUk ? "Скинути зміни" : "Reset changes";

    public static string MoveUpCommand => IsUk ? "Перемістити вище" : "Move up";

    public static string MoveDownCommand => IsUk ? "Перемістити нижче" : "Move down";

    public static string ExitCommand => IsUk ? "Вихід" : "Exit";

    public static string HelpCommand => IsUk ? "Як працювати з редактором…" : "Using the editor…";

    public static string HelpBody => IsUk
        ? "1. Створіть або відкрийте тест через меню «Файл».\n2. Додайте завдання через меню «Завдання».\n3. Заповніть умову та позначте правильні відповіді. Вкладка «Додатково» містить підказку, бал і обов’язковість.\n4. Збережіть завдання або перейдіть до іншого — зміни застосовуються автоматично.\n5. Збережіть файл .cctest та імпортуйте його у вікні тестування вчителя."
        : "1. Create or open a test using File.\n2. Add a question using Questions.\n3. Enter the prompt and mark correct answers. Additional contains the hint, score and required flag.\n4. Save the question or select another one — changes are applied automatically.\n5. Save the .cctest file and import it in the teacher’s Testing window.";

    public static string MainTab => IsUk ? "Основне" : "Main";

    public static string AdditionalTab => IsUk ? "Додатково" : "Additional";

    public static string TestDescription => IsUk ? "Опис тесту" : "Test description";

    public static string AuthorName => IsUk ? "Автор тесту" : "Test author";

    public static string AuthorEmail => IsUk ? "Електронна пошта автора" : "Author email";

    public static string Cancel => IsUk ? "Скасувати" : "Cancel";

    public static string SaveSettings => IsUk ? "Зберегти зміни" : "Save changes";

    public static string AddGroup => IsUk ? "Додати групу" : "Add group";

    public static string DeleteGroup => IsUk ? "Видалити групу" : "Delete group";

    public static string GroupDeleteHint => IsUk
        ? "Після видалення групи її завдання перейдуть до першої групи. Останню групу видалити не можна."
        : "Deleting a group moves its questions into the first group. The last group cannot be deleted.";

    public static string QuestionNumber(int number) => IsUk ? $"Завдання №{number}" : $"Question #{number}";

    public static string WorkspaceMeta(int version, int groups) => IsUk ? $"Версія: {version} · Груп: {groups}" : $"Version: {version} · Groups: {groups}";

    public static string QuestionCount(int count) => IsUk ? $"Усього завдань: {count}" : $"Questions: {count}";
}
