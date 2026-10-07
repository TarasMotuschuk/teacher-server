using Teacher.Common.Contracts.Testing;
using Teacher.Common.Localization;

namespace ClassCommander.TestRunner.Localization;

internal static partial class TestRunnerText
{
    public static UiLanguage Language { get; set; } = UiLanguageExtensions.GetDefault();

    private static bool IsUk => Language == UiLanguage.Ukrainian;

    public static string ExitTest => IsUk ? "Вийти з тесту…" : "Exit test…";

    public static string TeacherCodePrompt => IsUk
        ? "Для виходу до завершення тесту покличте вчителя. Учитель має ввести шестизначний код зі свого вікна тестування."
        : "To leave before finishing, call your teacher. Your teacher must enter the six-digit code from their testing window.";

    public static string WrongTeacherCode => IsUk ? "Неправильний код учителя." : "Incorrect teacher code.";

    public static string StayInTest => IsUk ? "Повернутися до тесту" : "Return to test";

    public static string SessionNotice => IsUk
        ? "Тестування: перемикання вікон обмежено. Завершіть і надішліть відповіді або покличте вчителя для виходу."
        : "Test in progress: window switching is restricted. Submit your answers or call your teacher to exit.";

    public static string ExitSaveFailed => IsUk
        ? "Не вдалося зберегти відповіді на сервері. Вийти без збереження останніх змін?"
        : "Could not save answers to the server. Exit without saving the latest changes?";

    public static string WindowTitle => IsUk ? "ClassCommander — Тестування" : "ClassCommander Testing";

    public static string ServerUrlLabel => IsUk ? "Адреса сервера тестів" : "Test server URL";

    public static string TeacherLaunchHint => IsUk
        ? "Введіть своє прізвище та ім’я, потім натисніть «Почати тест». Відкриється завдання, яке вибрав учитель."
        : "Enter your surname and first name, then press Start test. The assignment selected by your teacher will open.";

    public static string WaitingForTeacherServer => IsUk
        ? "Немає адреси сервера. Дочекайтеся запуску тесту вчителем."
        : "No server address. Wait for the teacher to start the test.";

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

    public static string RequiredFields => IsUk
        ? "Заповніть прізвище та ім’я."
        : "Enter surname and first name.";

    public static string RequiredServer => IsUk
        ? "Немає адреси сервера. Дочекайтеся запуску тесту вчителем."
        : "No server address. Wait for the teacher to start the test.";

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

    public static string ChooseAnswer => IsUk ? "Оберіть…" : "Choose…";

    public static string AnswerHint(QuestionType type) => (type, IsUk) switch
    {
        (QuestionType.SingleChoice, true) => "Оберіть одну відповідь.",
        (QuestionType.SingleChoice, false) => "Choose one answer.",
        (QuestionType.MultipleChoice, true) => "Позначте всі правильні відповіді.",
        (QuestionType.MultipleChoice, false) => "Select all correct answers.",
        (QuestionType.Matching, true) => "Для кожного пункту оберіть відповідну пару зі списку.",
        (QuestionType.Matching, false) => "Choose a matching answer from the list for each item.",
        (QuestionType.TrueFalseGroup, true) => "Для кожного твердження оберіть «Так» або «Ні».",
        (QuestionType.TrueFalseGroup, false) => "Choose True or False for each statement.",
        (QuestionType.Ordering, true) => "Виберіть пункт і перемістіть його стрілками вгору або вниз.",
        (QuestionType.Ordering, false) => "Select an item and move it with the up or down arrow.",
        (QuestionType.NumericInputGroup, true) => "Введіть число біля кожного пункту.",
        (QuestionType.NumericInputGroup, false) => "Enter a number beside each item.",
        (QuestionType.LetterOrdering, true) => "Введіть літери в правильному порядку.",
        (QuestionType.LetterOrdering, false) => "Enter the letters in the correct order.",
        (QuestionType.ImagePoint, _) => PointHint,
        (_, true) => "Введіть відповідь у поле нижче.",
        (_, false) => "Enter your answer in the field below.",
    };

    public static string TrueLabel => IsUk ? "Так" : "True";

    public static string FalseLabel => IsUk ? "Ні" : "False";

    public static string MoveUp => IsUk ? "↑" : "↑";

    public static string MoveDown => IsUk ? "↓" : "↓";

    public static string PointHint => IsUk
        ? "Клацніть по потрібній точці на зображенні. Щоб змінити відповідь, клацніть ще раз."
        : "Click the required point on the image. Click again to change your answer.";

    public static string PointNotSelected => IsUk ? "Точку ще не вибрано." : "No point selected yet.";

    public static string PointSelected => IsUk ? "Точку вибрано. Можна перейти до наступного питання." : "Point selected. You can move to the next question.";

    public static string PointImageMissing => IsUk
        ? "У цьому питанні немає зображення. Повідомте вчителя."
        : "This question has no image. Tell your teacher.";

    public static string TextAnswerPlaceholder => IsUk ? "Ваша відповідь" : "Your answer";

    public static string DoneCommand => IsUk ? "Готово" : "Done";

    public static string Connecting => IsUk ? "Підключення…" : "Connecting…";

    public static string LoadingTest => IsUk ? "Завантаження тесту…" : "Loading test…";
}
