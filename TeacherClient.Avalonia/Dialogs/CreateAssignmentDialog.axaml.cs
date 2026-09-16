using Avalonia.Controls;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class CreateAssignmentDialog : Window
{
    private CreateAssignmentDraft? _result;

    public CreateAssignmentDialog()
    {
        InitializeComponent();
        Icon = AppIconLoader.Load();
        ApplyLocalization();
    }

    public CreateAssignmentDialog(string suggestedTitle)
        : this()
    {
        TitleTextBox.Text = suggestedTitle;
    }

    public static async Task<CreateAssignmentDraft?> ShowAsync(Window owner, string suggestedTitle)
    {
        var dialog = new CreateAssignmentDialog(suggestedTitle);
        return await dialog.ShowDialog<CreateAssignmentDraft?>(owner);
    }

    private void ApplyLocalization()
    {
        Title = CrossPlatformText.TestingCreateAssignmentTitle;
        TitleLabel.Text = CrossPlatformText.TestingAssignmentTitleLabel;
        ClassLabel.Text = CrossPlatformText.TestingClassNameLabel;
        MaxAttemptsLabel.Text = CrossPlatformText.TestingMaxAttemptsLabel;
        TimeLimitLabel.Text = CrossPlatformText.TestingTimeLimitLabel;
        ShowScoreCheckBox.Content = CrossPlatformText.TestingShowScoreLabel;
        ShowCorrectCheckBox.Content = CrossPlatformText.TestingShowCorrectLabel;
        ShowFeedbackCheckBox.Content = CrossPlatformText.TestingShowFeedbackLabel;
        CancelButton.Content = CrossPlatformText.Cancel;
        CreateButton.Content = CrossPlatformText.Ok;
    }

    private async void CreateButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var title = TitleTextBox.Text?.Trim() ?? string.Empty;
        var className = ClassTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(className))
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingRequiredAssignmentFields);
            return;
        }

        if (!int.TryParse(MaxAttemptsTextBox.Text?.Trim(), out var maxAttempts) || maxAttempts < 1)
        {
            maxAttempts = 1;
        }

        int? timeLimit = null;
        if (int.TryParse(TimeLimitTextBox.Text?.Trim(), out var parsedLimit) && parsedLimit > 0)
        {
            timeLimit = parsedLimit;
        }

        _result = new CreateAssignmentDraft(
            title,
            className,
            maxAttempts,
            timeLimit,
            ShowScoreCheckBox.IsChecked == true,
            ShowCorrectCheckBox.IsChecked == true,
            ShowFeedbackCheckBox.IsChecked == true);
        Close(_result);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}

public sealed record CreateAssignmentDraft(
    string Title,
    string ClassName,
    int MaxAttempts,
    int? TimeLimitSeconds,
    bool ShowScore,
    bool ShowCorrectAnswers,
    bool ShowPerQuestionFeedback);
