using Teacher.Common.Localization;

namespace ClassCommander.TestRunner.Localization;

internal static class TestRunnerText
{
    public static UiLanguage Language { get; set; } = UiLanguageExtensions.GetDefault();

    private static bool IsUk => Language == UiLanguage.Ukrainian;

    public static string WindowTitle => IsUk ? "ClassCommander — Тестування" : "ClassCommander Testing";

    public static string LanguageMenu => IsUk ? "_Мова" : "_Language";

    public static string English => "English";

    public static string Ukrainian => "Українська";

    public static string ServerUrlLabel => IsUk ? "Адреса сервера тестів" : "Test server URL";

    public static string SurnameLabel => IsUk ? "Прізвище" : "Surname";

    public static string NameLabel => IsUk ? "Ім’я" : "First name";

    public static string ClassLabel => IsUk ? "Клас" : "Class";

    public static string DeviceLabel => IsUk ? "ID пристрою (необов’язково)" : "Device ID (optional)";

    public static string ContinueCommand => IsUk ? "Продовжити" : "Continue";

    public static string BackCommand => IsUk ? "Назад" : "Back";

    public static string RefreshCommand => IsUk ? "Оновити" : "Refresh";

    public static string StartCommand => IsUk ? "Почати тест" : "Start test";

    public static string PreviousCommand => IsUk ? "Попереднє" : "Previous";

    public static string NextCommand => IsUk ? "Наступне" : "Next";

    public static string SaveProgressCommand => IsUk ? "Зберегти прогрес" : "Save progress";

    public static string SubmitCommand => IsUk ? "Завершити і надіслати" : "Submit";

    public static string AssignmentsHeading => IsUk ? "Доступні завдання" : "Available assignments";

    public static string NoAssignments => IsUk ? "Немає активних завдань для цього учня." : "No active assignments for this student.";

    public static string IdentityHeading => IsUk ? "Вхід учня" : "Student sign-in";

    public static string QuestionHeading => IsUk ? "Питання" : "Question";

    public static string ResultHeading => IsUk ? "Результат" : "Result";

    public static string StatusReady => IsUk ? "Готово" : "Ready";

    public static string ErrorTitle => IsUk ? "Помилка" : "Error";

    public static string RequiredFields => IsUk ? "Заповніть прізвище, ім’я та адресу сервера." : "Enter surname, first name, and server URL.";

    public static string ProgressSaved => IsUk ? "Прогрес збережено." : "Progress saved.";

    public static string ConfirmSubmit => IsUk
        ? "Завершити тест і надіслати відповіді? Після цього змінити відповіді буде неможливо."
        : "Submit the test now? Answers cannot be changed after submission.";

    public static string Yes => IsUk ? "Так" : "Yes";

    public static string No => IsUk ? "Ні" : "No";

    public static string ScoreLine(decimal earned, decimal max, decimal percent) => IsUk
        ? $"Бали: {earned:0.##} / {max:0.##} ({percent:0.##}%)"
        : $"Score: {earned:0.##} / {max:0.##} ({percent:0.##}%)";

    public static string QuestionProgress(int index, int total) => IsUk
        ? $"Питання {index} з {total}"
        : $"Question {index} of {total}";

    public static string TrueLabel => IsUk ? "Так" : "True";

    public static string FalseLabel => IsUk ? "Ні" : "False";

    public static string MoveUp => IsUk ? "↑" : "↑";

    public static string MoveDown => IsUk ? "↓" : "↓";

    public static string PointHint => IsUk
        ? "Вкажіть координати точки на зображенні (X, Y)."
        : "Enter image point coordinates (X, Y).";

    public static string TextAnswerPlaceholder => IsUk ? "Ваша відповідь" : "Your answer";

    public static string DoneCommand => IsUk ? "Готово" : "Done";

    public static string Connecting => IsUk ? "Підключення…" : "Connecting…";

    public static string LoadingTest => IsUk ? "Завантаження тесту…" : "Loading test…";
}
