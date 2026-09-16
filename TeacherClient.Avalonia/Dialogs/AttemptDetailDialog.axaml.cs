using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Teacher.Common.Contracts.Testing;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class AttemptDetailDialog : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public AttemptDetailDialog()
    {
        InitializeComponent();
        Icon = AppIconLoader.Load();
        Title = CrossPlatformText.TestingResultDetailTitle;
        CloseButton.Content = CrossPlatformText.Close;
    }

    public static async Task ShowAsync(
        Window owner,
        AttemptDto attempt,
        ResultDto? result)
    {
        var dialog = new AttemptDetailDialog();
        dialog.DetailTextBlock.Text = Format(attempt, result);
        await dialog.ShowDialog(owner);
    }

    private static string Format(AttemptDto attempt, ResultDto? result)
    {
        var sb = new StringBuilder();
        var student = $"{attempt.Student.Surname} {attempt.Student.Name}".Trim();
        if (!string.IsNullOrWhiteSpace(attempt.Student.ClassName))
        {
            student += $" ({attempt.Student.ClassName})";
        }

        sb.AppendLine(student);
        sb.AppendLine($"{CrossPlatformText.TestingColumnStatus}: {attempt.Status}");
        sb.AppendLine($"{CrossPlatformText.TestingColumnStarted}: {attempt.StartedAtUtc.ToLocalTime():g}");
        if (attempt.SubmittedAtUtc is not null)
        {
            sb.AppendLine($"{CrossPlatformText.TestingColumnCompleted}: {attempt.SubmittedAtUtc.Value.ToLocalTime():g}");
        }

        sb.AppendLine();
        if (result is not null)
        {
            sb.AppendLine(CrossPlatformText.TestingScoreLine(result.ScoreEarned, result.ScoreMax, result.Percent));
            sb.AppendLine();
            sb.AppendLine(CrossPlatformText.TestingPerQuestionHeading);
            for (var i = 0; i < result.QuestionResults.Count; i++)
            {
                var q = result.QuestionResults[i];
                sb.AppendLine($"{i + 1}. {(q.IsCorrect ? "✓" : "✗")} {q.ScoreEarned:0.##}/{q.ScoreMax:0.##}  [{q.QuestionId}]");
            }
        }
        else
        {
            sb.AppendLine(CrossPlatformText.TestingNoResultYet);
        }

        sb.AppendLine();
        sb.AppendLine(CrossPlatformText.TestingAnswersHeading);
        if (attempt.Answers.Count == 0)
        {
            sb.AppendLine("—");
        }
        else
        {
            foreach (var answer in attempt.Answers)
            {
                sb.AppendLine($"[{answer.QuestionId}] {answer.Type}");
                sb.AppendLine(JsonSerializer.Serialize(answer.Value, JsonOptions));
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    private void CloseButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
