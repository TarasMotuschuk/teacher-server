using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ClassCommander.TestEditor.Localization;
using ClassCommander.TestEditor.Models;
using ClassCommander.Testing.Core.Import;
using ClassCommander.Testing.Core.Packaging;
using ClassCommander.Testing.Core.Serialization;
using Teacher.Common.Contracts.Testing;
using Teacher.Common.Localization;

namespace ClassCommander.TestEditor;

public partial class MainWindow : Window
{
    private readonly MyTestXmlImporter _importer = new();
    private readonly CctestPackageService _packageService = new();
    private readonly ObservableCollection<QuestionListItem> _questions = [];

    private TestDefinitionDto? _definition;
    private string? _packagePath;
    private string? _workspaceDirectory;
    private string? _originalImportPath;

    public MainWindow()
    {
        InitializeComponent();
        QuestionsListBox.ItemsSource = _questions;
        ApplyLocalization();
        NewTest();
    }

    private void ApplyLocalization()
    {
        Title = TestEditorText.WindowTitle;
        FileMenuItem.Header = TestEditorText.FileMenu;
        NewMenuItem.Header = TestEditorText.NewCommand;
        OpenMenuItem.Header = TestEditorText.OpenCommand;
        SaveMenuItem.Header = TestEditorText.SaveCommand;
        SaveAsMenuItem.Header = TestEditorText.SaveAsCommand;
        ImportMyTestMenuItem.Header = TestEditorText.ImportMyTestCommand;
        LanguageMenuItem.Header = TestEditorText.LanguageMenu;
        EnglishMenuItem.Header = TestEditorText.English;
        UkrainianMenuItem.Header = TestEditorText.Ukrainian;
        GroupsHeadingText.Text = TestEditorText.GroupsHeading;
        PreviewHeadingText.Text = TestEditorText.PreviewHeading;
        TypeLabelText.Text = TestEditorText.TypeLabel;
        ScoreLabelText.Text = TestEditorText.ScoreLabel;
        PromptLabelText.Text = TestEditorText.PromptLabel;
        OptionsLabelText.Text = TestEditorText.OptionsLabel;
        AnswerKeyLabelText.Text = TestEditorText.AnswerKeyLabel;
        AssetsLabelText.Text = TestEditorText.AssetsLabel;
        WarningsLabelText.Text = TestEditorText.WarningsLabel;
        RefreshTestHeader();
        RefreshEmptyState();
    }

    private void EnglishMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TestEditorText.Language = UiLanguage.English;
        ApplyLocalization();
        BindQuestions();
    }

    private void UkrainianMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TestEditorText.Language = UiLanguage.Ukrainian;
        ApplyLocalization();
        BindQuestions();
    }

    private void NewMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => NewTest();

    private async void OpenMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = TestEditorText.OpenCctestTitle,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("cctest")
                {
                    Patterns = ["*.cctest"],
                },
            ],
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        try
        {
            await LoadPackageAsync(file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void SaveMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            await SaveAsync(saveAs: false);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void SaveAsMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            await SaveAsync(saveAs: true);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void ImportMyTestMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = TestEditorText.ImportXmlTitle,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("xml")
                {
                    Patterns = ["*.xml"],
                },
            ],
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        try
        {
            await ImportMyTestAsync(file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private void QuestionsListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (QuestionsListBox.SelectedItem is QuestionListItem item)
        {
            ShowPreview(item.Question);
        }
        else
        {
            PreviewPanel.IsVisible = false;
            RefreshEmptyState();
        }
    }

    private void NewTest()
    {
        ResetWorkspace();
        _definition = new TestDefinitionDto(
            SchemaVersion: 1,
            Type: "test-definition",
            PublicId: $"test_{Guid.NewGuid():N}"[..18],
            Version: 1,
            Title: TestEditorText.UntitledTest,
            Description: null,
            Language: TestEditorText.Language == UiLanguage.Ukrainian ? "uk" : "en",
            Grade: null,
            Subjects: [],
            Tags: [],
            Author: null,
            Source: null,
            Settings: new TestSettingsDto(false, false, false, null, 1, "score-only"),
            Assets: [],
            Groups: []);
        _packagePath = null;
        BindQuestions();
        StatusTextBlock.Text = TestEditorText.StatusReady;
        RefreshTestHeader();
    }

    private async Task LoadPackageAsync(string path)
    {
        ResetWorkspace();
        _workspaceDirectory = Path.Combine(Path.GetTempPath(), "ClassCommander", "TestEditor", Guid.NewGuid().ToString("N"));
        var (_, definition, extracted) = await _packageService.OpenAsync(path, _workspaceDirectory);
        _workspaceDirectory = extracted;
        _definition = definition;
        _packagePath = path;
        _originalImportPath = Directory.Exists(Path.Combine(extracted, "imports"))
            ? Directory.EnumerateFiles(Path.Combine(extracted, "imports")).FirstOrDefault()
            : null;
        BindQuestions();
        var questionCount = definition.Groups.Sum(g => g.Questions.Count);
        StatusTextBlock.Text = TestEditorText.StatusLoaded(definition.Title, questionCount);
        RefreshTestHeader();
    }

    private async Task ImportMyTestAsync(string xmlPath)
    {
        ResetWorkspace();
        _workspaceDirectory = Path.Combine(Path.GetTempPath(), "ClassCommander", "TestEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceDirectory);
        await using var stream = File.OpenRead(xmlPath);
        var (definition, warnings) = _importer.Import(stream, Path.GetFileName(xmlPath), _workspaceDirectory);

        var importsDir = Path.Combine(_workspaceDirectory, "imports");
        Directory.CreateDirectory(importsDir);
        _originalImportPath = Path.Combine(importsDir, Path.GetFileName(xmlPath));
        File.Copy(xmlPath, _originalImportPath, overwrite: true);

        _definition = definition;
        _packagePath = null;
        BindQuestions();
        StatusTextBlock.Text = TestEditorText.StatusImported(definition.Title, warnings.Count);
        RefreshTestHeader();
    }

    private async Task SaveAsync(bool saveAs)
    {
        if (_definition is null)
        {
            await ShowErrorAsync(TestEditorText.NothingToSave);
            return;
        }

        var targetPath = _packagePath;
        if (saveAs || string.IsNullOrWhiteSpace(targetPath))
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = TestEditorText.SaveCctestTitle,
                SuggestedFileName = $"{SanitizeFileName(_definition.Title)}.cctest",
                FileTypeChoices =
                [
                    new FilePickerFileType("cctest")
                    {
                        Patterns = ["*.cctest"],
                    },
                ],
            });

            if (file is null)
            {
                return;
            }

            targetPath = file.Path.LocalPath;
            if (!targetPath.EndsWith(".cctest", StringComparison.OrdinalIgnoreCase))
            {
                targetPath += ".cctest";
            }
        }

        var assetsDirectory = _workspaceDirectory is null
            ? null
            : Path.Combine(_workspaceDirectory, "assets");
        await _packageService.SaveAsync(targetPath, _definition, assetsDirectory, _originalImportPath);
        _packagePath = targetPath;
        StatusTextBlock.Text = TestEditorText.StatusSaved(targetPath);
    }

    private void BindQuestions()
    {
        _questions.Clear();
        if (_definition is null)
        {
            PreviewPanel.IsVisible = false;
            RefreshEmptyState();
            return;
        }

        foreach (var group in _definition.Groups.OrderBy(g => g.Order))
        {
            var groupTitle = TestEditorText.GroupTitle(group.Title, group.Questions.Count);
            foreach (var question in group.Questions)
            {
                _questions.Add(new QuestionListItem(groupTitle, question));
            }
        }

        if (_questions.Count > 0)
        {
            QuestionsListBox.SelectedIndex = 0;
        }
        else
        {
            PreviewPanel.IsVisible = false;
            RefreshEmptyState();
        }
    }

    private void ShowPreview(QuestionDto question)
    {
        EmptyStateText.IsVisible = false;
        PreviewPanel.IsVisible = true;
        TypeValueText.Text = TestEditorText.QuestionTypeName(question.Type);
        ScoreValueText.Text = question.Score.ToString("0.##");
        PromptValueText.Text = question.Prompt;
        OptionsValueText.Text = FormatInteraction(question.Interaction);
        AnswerKeyValueText.Text = FormatAnswerKey(question.AnswerKey);
        AssetsValueText.Text = question.Assets.Count == 0
            ? "—"
            : string.Join(", ", question.Assets.Select(a => $"{a.AssetId} ({a.Role})"));

        var warnings = question.Source?.ImportWarnings ?? [];
        WarningsLabelText.IsVisible = warnings.Count > 0;
        WarningsValueText.IsVisible = warnings.Count > 0;
        WarningsValueText.Text = warnings.Count == 0 ? string.Empty : string.Join(Environment.NewLine, warnings);
    }

    private void RefreshEmptyState()
    {
        if (_definition is null || _questions.Count == 0)
        {
            EmptyStateText.Text = _definition is null ? TestEditorText.NoTestLoaded : TestEditorText.SelectQuestionHint;
            EmptyStateText.IsVisible = !PreviewPanel.IsVisible;
        }
        else if (QuestionsListBox.SelectedItem is null)
        {
            EmptyStateText.Text = TestEditorText.SelectQuestionHint;
            EmptyStateText.IsVisible = true;
        }
        else
        {
            EmptyStateText.IsVisible = false;
        }
    }

    private void RefreshTestHeader()
    {
        if (_definition is null)
        {
            Title = TestEditorText.WindowTitle;
            TestTitleText.Text = "—";
            TestMetaText.Text = string.Empty;
            return;
        }

        Title = $"{TestEditorText.WindowTitle} — {_definition.Title}";
        TestTitleText.Text = _definition.Title;
        var questionCount = _definition.Groups.Sum(g => g.Questions.Count);
        TestMetaText.Text = TestEditorText.TestMeta(
            _definition.PublicId,
            _definition.Version,
            questionCount,
            _definition.Groups.Count);
    }

    private void ResetWorkspace()
    {
        if (!string.IsNullOrWhiteSpace(_workspaceDirectory) && Directory.Exists(_workspaceDirectory))
        {
            try
            {
                Directory.Delete(_workspaceDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        _workspaceDirectory = null;
        _originalImportPath = null;
        _packagePath = null;
        _definition = null;
        _questions.Clear();
        PreviewPanel.IsVisible = false;
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new Window
        {
            Title = TestEditorText.ErrorTitle,
            Width = 480,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBlock
            {
                Text = message,
                Margin = new Avalonia.Thickness(20),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            },
        };
        await dialog.ShowDialog(this);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "test" : sanitized;
    }

    private static string FormatInteraction(QuestionInteractionDto interaction) => interaction switch
    {
        SingleChoiceInteractionDto single => FormatOptions(single.Options),
        MultipleChoiceInteractionDto multi => FormatOptions(multi.Options),
        OrderingInteractionDto ordering => FormatOptions(ordering.Options),
        MatchingInteractionDto matching =>
            $"Left: {string.Join("; ", matching.LeftItems.Select(i => i.Text))}{Environment.NewLine}Right: {string.Join("; ", matching.RightItems.Select(i => i.Text))}",
        TrueFalseGroupInteractionDto tf => string.Join(Environment.NewLine, tf.Statements.Select(s => s.Text)),
        NumericInputGroupInteractionDto numeric => string.Join(Environment.NewLine, numeric.Entries.Select(e => e.Caption)),
        TextInputInteractionDto text => text.Placeholder ?? "—",
        ImagePointInteractionDto image => image.SelectionMode,
        LetterOrderingInteractionDto letters => letters.Mode,
        _ => interaction.GetType().Name,
    };

    private static string FormatOptions(IReadOnlyList<OptionDto> options) =>
        string.Join(Environment.NewLine, options.Select(o => $"{o.Order}. [{o.Id}] {o.Text}"));

    private static string FormatAnswerKey(QuestionAnswerKeyDto answerKey)
    {
        var json = TestPlatformJson.Serialize(answerKey);
        return json.Length > 2000 ? json[..2000] + "…" : json;
    }
}
