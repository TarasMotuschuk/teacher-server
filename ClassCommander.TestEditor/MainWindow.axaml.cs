using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ClassCommander.TestEditor.Localization;
using ClassCommander.TestEditor.Models;
using ClassCommander.TestEditor.Services;
using ClassCommander.Testing.Core.Import;
using ClassCommander.Testing.Core.Packaging;
using Teacher.Common.Contracts.Testing;
using Teacher.Common.Localization;

namespace ClassCommander.TestEditor;

public partial class MainWindow : Window
{
    private readonly MyTestXmlImporter _importer = new();
    private readonly CctestPackageService _packageService = new();
    private readonly ObservableCollection<QuestionListItem> _questions = [];
    private readonly QuestionEditorHost _editor = new();

    private TestDefinitionDto? _definition;
    private string? _packagePath;
    private string? _workspaceDirectory;
    private string? _originalImportPath;
    private string? _selectedQuestionId;
    private bool _suppressSelectionHandler;

    public MainWindow()
    {
        InitializeComponent();
        TestEditorText.Language = ClassCommanderUiSettings.LoadLanguage();
        QuestionsListBox.ItemsSource = _questions;
        EditorHost.Child = _editor.Control;
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
        GroupsHeadingText.Text = TestEditorText.GroupsHeading;
        EditorHeadingText.Text = TestEditorText.EditorHeading;
        TestTitleLabelText.Text = TestEditorText.TestTitleLabel;
        GroupTitleLabelText.Text = TestEditorText.GroupTitleLabel;
        AddQuestionButton.Content = TestEditorText.AddQuestion;
        DeleteQuestionButton.Content = TestEditorText.DeleteQuestion;
        MoveUpButton.Content = TestEditorText.MoveUp;
        MoveDownButton.Content = TestEditorText.MoveDown;
        ApplyChangesButton.Content = TestEditorText.ApplyChanges;
        RefreshTestHeader();
        RefreshEmptyState();
        BindQuestions(preserveSelection: true);
    }

    private void NewMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => NewTest();

    private async void OpenMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CommitCurrentQuestion();
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = TestEditorText.OpenCctestTitle,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("cctest") { Patterns = ["*.cctest"] },
            ],
        });

        if (files.Count == 0)
        {
            return;
        }

        try
        {
            await LoadPackageAsync(files[0].Path.LocalPath);
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
            CommitCurrentQuestion();
            CommitTestMetadata();
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
            CommitCurrentQuestion();
            CommitTestMetadata();
            await SaveAsync(saveAs: true);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void ImportMyTestMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CommitCurrentQuestion();
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = TestEditorText.ImportXmlTitle,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("xml") { Patterns = ["*.xml"] },
            ],
        });

        if (files.Count == 0)
        {
            return;
        }

        try
        {
            await ImportMyTestAsync(files[0].Path.LocalPath);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private async void AddQuestionButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_definition is null)
        {
            NewTest();
        }

        CommitCurrentQuestion();
        var type = await PickQuestionTypeAsync();
        if (type is null)
        {
            return;
        }

        EnsureDefaultGroup();
        var question = QuestionFactory.Create(type.Value);
        var groups = _definition!.Groups.ToList();
        var group = groups[0];
        var questions = group.Questions.ToList();
        questions.Add(question);
        groups[0] = group with { Questions = questions };
        _definition = _definition with { Groups = groups };
        BindQuestions();
        SelectQuestion(question.Id);
        StatusTextBlock.Text = TestEditorText.StatusQuestionAdded;
    }

    private void DeleteQuestionButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_definition is null || string.IsNullOrWhiteSpace(_selectedQuestionId))
        {
            return;
        }

        var groups = _definition.Groups.Select(group => group with
        {
            Questions = group.Questions.Where(q => q.Id != _selectedQuestionId).ToList(),
        }).ToList();
        _definition = _definition with { Groups = groups };
        _selectedQuestionId = null;
        _editor.Clear();
        BindQuestions();
        StatusTextBlock.Text = TestEditorText.StatusQuestionDeleted;
    }

    private void MoveUpButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => MoveSelectedQuestion(-1);

    private void MoveDownButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => MoveSelectedQuestion(1);

    private void ApplyChangesButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CommitCurrentQuestion())
        {
            StatusTextBlock.Text = TestEditorText.StatusQuestionUpdated;
            BindQuestions(preserveSelection: true);
        }
    }

    private void TestTitleTextBox_OnLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CommitTestMetadata();

    private void GroupTitleTextBox_OnLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CommitTestMetadata();

    private void QuestionsListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionHandler)
        {
            return;
        }

        CommitCurrentQuestion();
        if (QuestionsListBox.SelectedItem is QuestionListItem item)
        {
            LoadQuestion(item.Question);
        }
        else
        {
            _selectedQuestionId = null;
            _editor.Clear();
            RefreshEmptyState();
        }
    }

    private void NewTest()
    {
        ResetWorkspace();
        var group = QuestionFactory.CreateDefaultGroup() with { Title = TestEditorText.DefaultGroupTitle };
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
            Groups: [group]);
        _packagePath = null;
        _selectedQuestionId = null;
        _editor.Clear();
        TestTitleTextBox.Text = _definition.Title;
        GroupTitleTextBox.Text = group.Title;
        BindQuestions();
        StatusTextBlock.Text = TestEditorText.StatusNewTest;
        RefreshTestHeader();
        RefreshEmptyState();
        TestTitleTextBox.Focus();
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
        TestTitleTextBox.Text = definition.Title;
        GroupTitleTextBox.Text = definition.Groups.OrderBy(g => g.Order).FirstOrDefault()?.Title
            ?? TestEditorText.DefaultGroupTitle;
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
        TestTitleTextBox.Text = definition.Title;
        GroupTitleTextBox.Text = definition.Groups.OrderBy(g => g.Order).FirstOrDefault()?.Title
            ?? TestEditorText.DefaultGroupTitle;
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
                    new FilePickerFileType("cctest") { Patterns = ["*.cctest"] },
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

    private void BindQuestions(bool preserveSelection = false)
    {
        var selectedId = preserveSelection ? _selectedQuestionId : null;
        _suppressSelectionHandler = true;
        _questions.Clear();
        if (_definition is null)
        {
            _suppressSelectionHandler = false;
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

        _suppressSelectionHandler = false;
        RefreshTestHeader();

        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            SelectQuestion(selectedId);
        }
        else if (_questions.Count > 0)
        {
            QuestionsListBox.SelectedIndex = 0;
        }
        else
        {
            _editor.Clear();
            RefreshEmptyState();
        }
    }

    private void SelectQuestion(string questionId)
    {
        var item = _questions.FirstOrDefault(q => q.Question.Id == questionId);
        if (item is null)
        {
            return;
        }

        QuestionsListBox.SelectedItem = item;
    }

    private void LoadQuestion(QuestionDto question)
    {
        var real = FindQuestion(question.Id) ?? question;
        _selectedQuestionId = real.Id;
        _editor.Load(real);
        EmptyStateText.IsVisible = false;
        ApplyChangesButton.IsVisible = true;
    }

    private QuestionDto? FindQuestion(string questionId)
        => _definition?.Groups.SelectMany(g => g.Questions).FirstOrDefault(q => q.Id == questionId);

    private bool CommitCurrentQuestion()
    {
        if (_definition is null || string.IsNullOrWhiteSpace(_selectedQuestionId))
        {
            return false;
        }

        var updated = _editor.Collect();
        if (updated is null)
        {
            return false;
        }

        var groups = _definition.Groups.Select(group => group with
        {
            Questions = group.Questions.Select(q => q.Id == updated.Id ? updated : q).ToList(),
        }).ToList();
        _definition = _definition with { Groups = groups };
        return true;
    }

    private void CommitTestMetadata()
    {
        if (_definition is null)
        {
            return;
        }

        var title = string.IsNullOrWhiteSpace(TestTitleTextBox.Text)
            ? TestEditorText.UntitledTest
            : TestTitleTextBox.Text.Trim();
        var groupTitle = string.IsNullOrWhiteSpace(GroupTitleTextBox.Text)
            ? TestEditorText.DefaultGroupTitle
            : GroupTitleTextBox.Text.Trim();

        var groups = _definition.Groups.ToList();
        if (groups.Count == 0)
        {
            groups.Add(QuestionFactory.CreateDefaultGroup() with { Title = groupTitle });
        }
        else
        {
            groups[0] = groups[0] with { Title = groupTitle };
        }

        _definition = _definition with { Title = title, Groups = groups };
        RefreshTestHeader();
    }

    private void EnsureDefaultGroup()
    {
        if (_definition is null)
        {
            return;
        }

        if (_definition.Groups.Count > 0)
        {
            return;
        }

        _definition = _definition with
        {
            Groups = [QuestionFactory.CreateDefaultGroup() with { Title = TestEditorText.DefaultGroupTitle }],
        };
    }

    private void MoveSelectedQuestion(int delta)
    {
        if (_definition is null || string.IsNullOrWhiteSpace(_selectedQuestionId))
        {
            return;
        }

        CommitCurrentQuestion();
        var groups = _definition.Groups.ToList();
        for (var gi = 0; gi < groups.Count; gi++)
        {
            var questions = groups[gi].Questions.ToList();
            var index = questions.FindIndex(q => q.Id == _selectedQuestionId);
            if (index < 0)
            {
                continue;
            }

            var target = index + delta;
            if (target < 0 || target >= questions.Count)
            {
                return;
            }

            (questions[index], questions[target]) = (questions[target], questions[index]);
            groups[gi] = groups[gi] with { Questions = questions };
            _definition = _definition with { Groups = groups };
            BindQuestions(preserveSelection: true);
            return;
        }
    }

    private void RefreshEmptyState()
    {
        if (_definition is null)
        {
            EmptyStateText.Text = TestEditorText.NoTestLoaded;
            EmptyStateText.IsVisible = true;
            ApplyChangesButton.IsVisible = false;
            return;
        }

        if (_questions.Count == 0)
        {
            EmptyStateText.Text = TestEditorText.EmptyTestHint;
            EmptyStateText.IsVisible = true;
            ApplyChangesButton.IsVisible = false;
            return;
        }

        if (QuestionsListBox.SelectedItem is null)
        {
            EmptyStateText.Text = TestEditorText.SelectQuestionHint;
            EmptyStateText.IsVisible = true;
            ApplyChangesButton.IsVisible = false;
            return;
        }

        EmptyStateText.IsVisible = false;
        ApplyChangesButton.IsVisible = true;
    }

    private void RefreshTestHeader()
    {
        if (_definition is null)
        {
            Title = TestEditorText.WindowTitle;
            TestMetaText.Text = string.Empty;
            return;
        }

        Title = $"{TestEditorText.WindowTitle} — {_definition.Title}";
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
        _selectedQuestionId = null;
        _questions.Clear();
        _editor.Clear();
    }

    private async Task<QuestionType?> PickQuestionTypeAsync()
    {
        QuestionType? selected = QuestionType.SingleChoice;
        var list = new ListBox
        {
            ItemsSource = Enum.GetValues<QuestionType>()
                .Select(t => new TypePickItem(t, TestEditorText.QuestionTypeName(t)))
                .ToList(),
            SelectedIndex = 0,
            Height = 280,
            Margin = new Avalonia.Thickness(16),
        };
        list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<TypePickItem>((item, _) =>
            new TextBlock
            {
                Text = item?.Display ?? string.Empty,
                Margin = new Avalonia.Thickness(4),
            });

        var ok = new Button { Content = TestEditorText.ApplyChanges, MinWidth = 100, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 100, IsCancel = true };
        var dialog = new Window
        {
            Title = TestEditorText.ChooseTypeTitle,
            Width = 420,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Children =
                {
                    new StackPanel
                    {
                        [DockPanel.DockProperty] = Dock.Bottom,
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Avalonia.Thickness(16),
                        Children = { cancel, ok },
                    },
                    list,
                },
            },
        };

        ok.Click += (_, _) =>
        {
            if (list.SelectedItem is TypePickItem item)
            {
                selected = item.Type;
            }

            dialog.Close(true);
        };
        cancel.Click += (_, _) =>
        {
            selected = null;
            dialog.Close(false);
        };

        // Localize cancel for bilingual UI.
        cancel.Content = TestEditorText.Language == UiLanguage.Ukrainian ? "Скасувати" : "Cancel";

        var accepted = await dialog.ShowDialog<bool>(this);
        return accepted ? selected : null;
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

    private sealed record TypePickItem(QuestionType Type, string Display);
}
