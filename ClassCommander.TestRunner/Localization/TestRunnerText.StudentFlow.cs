namespace ClassCommander.TestRunner.Localization;

internal static partial class TestRunnerText
{
    public static string TestMenuLabel => IsUk ? "Тест" : "Test";

    public static string ViewMenuLabel => IsUk ? "Вигляд" : "View";

    public static string OverviewHeading => IsUk ? "Інформація про тест" : "Test information";

    public static string ReadAndContinue => IsUk ? "Продовжити →" : "Continue →";

    public static string RetryCommand => IsUk ? "Спробувати ще раз" : "Try again";

    public static string ZoomIn => IsUk ? "Збільшити текст і зображення" : "Enlarge text and images";

    public static string ZoomOut => IsUk ? "Зменшити текст і зображення" : "Reduce text and images";

    public static string ResetZoom => IsUk ? "Звичайний розмір" : "Normal size";

    public static string SkipCommand => IsUk ? "Пропустити" : "Skip";

    public static string FinishCommand => IsUk ? "Завершити тест" : "Finish test";

    public static string ScoreHidden => IsUk ? "Результат перегляне вчитель." : "Your teacher will review the result.";

    public static string NoTimeLimit => IsUk ? "Без обмеження часу" : "No time limit";

    public static string OverviewRules => IsUk
        ? "Введіть власне прізвище та ім’я. Відповіді можна змінювати та повертатися до питань до надсилання. Пропущені питання залишаться без відповіді. Після завершення дочекайтеся підтвердження від сервера."
        : "Enter your own surname and first name. You can change answers and return to questions before submitting. Skipped questions remain unanswered. After finishing, wait for the server confirmation.";

    public static string ScoreVisibleRule => IsUk ? "Після завершення ви побачите набрані бали." : "Your score will be shown after submission.";

    public static string ScoreHiddenRule => IsUk ? "Учитель приховав бали від учнів." : "Your teacher has hidden scores from students.";

    public static string OverviewFailed => IsUk ? "Не вдалося отримати інформацію про тест. Перевірте підключення та спробуйте ще раз." : "Could not load test information. Check your connection and try again.";

    public static string SubmitFailed => IsUk ? "Результат ще не підтверджено сервером. Відповіді залишаються у цьому вікні. Перевірте підключення й повторіть надсилання." : "The server has not confirmed your result yet. Answers remain in this window. Check your connection and submit again.";

    public static string TimeExpired => IsUk ? "Час вичерпано. Надсилаємо відповіді…" : "Time is up. Submitting answers…";

    public static string ResultConfirmed => IsUk ? "✓ Результат отримано сервером учителя." : "✓ The teacher's server received your result.";

    public static string OverviewFacts(int count, decimal score, string? author, int? seconds) => IsUk
        ? $"Питань: {count} · Максимум балів: {score:0.##}\n{(seconds is > 0 ? $"Час: {Duration(TimeSpan.FromSeconds(seconds.Value))}" : NoTimeLimit)}{(string.IsNullOrWhiteSpace(author) ? string.Empty : $"\nАвтор: {author}")}"
        : $"Questions: {count} · Maximum score: {score:0.##}\n{(seconds is > 0 ? $"Time: {Duration(TimeSpan.FromSeconds(seconds.Value))}" : NoTimeLimit)}{(string.IsNullOrWhiteSpace(author) ? string.Empty : $"\nAuthor: {author}")}";

    public static string Answered(int count, int total) => IsUk ? $"Відповіді: {count} з {total}" : $"Answered: {count} of {total}";

    public static string Elapsed(TimeSpan elapsed) => IsUk ? $"Витрачено: {Duration(elapsed)}" : $"Elapsed: {Duration(elapsed)}";

    public static string Remaining(TimeSpan remaining) => IsUk ? $"Залишилось: {Duration(remaining)}" : $"Remaining: {Duration(remaining)}";

    public static string GradeLine(decimal grade) => IsUk ? $"Оцінка: {grade:0.##}" : $"Grade: {grade:0.##}";

    public static string Unanswered(int count) => IsUk ? $"Без відповіді: {count}." : $"Unanswered: {count}.";

    public static string ResultCounts(int correct, int partial, int incorrect) => IsUk
        ? $"Правильних: {correct} · Частково правильних: {partial} · Неправильних: {incorrect}"
        : $"Correct: {correct} · Partially correct: {partial} · Incorrect: {incorrect}";

    public static string Duration(TimeSpan time) => $"{(int)Math.Max(0, time.TotalHours):00}:{Math.Max(0, time.Minutes):00}:{Math.Max(0, time.Seconds):00}";
}
