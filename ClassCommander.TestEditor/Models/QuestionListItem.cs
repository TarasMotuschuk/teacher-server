using ClassCommander.TestEditor.Localization;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestEditor.Models;

internal sealed class QuestionListItem
{
    public QuestionListItem(string groupTitle, QuestionDto question)
    {
        GroupTitle = groupTitle;
        Question = question;
        Display = $"{question.Id} · {TestEditorText.QuestionTypeName(question.Type)} · {Truncate(question.Prompt, 80)}";
    }

    public string GroupTitle { get; }

    public QuestionDto Question { get; }

    public string Display { get; }

    private static string Truncate(string value, int max)
    {
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= max ? normalized : normalized[..(max - 1)] + "…";
    }
}
