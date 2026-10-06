using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ClassCommander.TestEditor.Localization;
using ClassCommander.TestEditor.Services;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestEditor;

public partial class MainWindow
{
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
        {
            switch (e.Key)
            {
                case Key.N:
                    NewMenuItem_OnClick(this, e);
                    break;
                case Key.O:
                    OpenMenuItem_OnClick(this, e);
                    break;
                case Key.S:
                    SaveMenuItem_OnClick(this, e);
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            TestSettingsMenuItem_OnClick(this, e);
            e.Handled = true;
        }
    }

    private void UpdateQuestionCommands()
    {
        var group = _definition?.Groups.FirstOrDefault(item => item.Questions.Any(question => question.Id == _selectedQuestionId));
        var index = group?.Questions.ToList().FindIndex(item => item.Id == _selectedQuestionId) ?? -1;
        var selected = index >= 0;
        DuplicateQuestionMenuItem.IsEnabled = selected;
        DeleteQuestionMenuItem.IsEnabled = selected;
        DeleteQuestionButton.IsEnabled = selected;
        ApplyQuestionMenuItem.IsEnabled = selected;
        ResetQuestionMenuItem.IsEnabled = selected;
        MoveUpMenuItem.IsEnabled = MoveUpButton.IsEnabled = index > 0;
        MoveDownMenuItem.IsEnabled = MoveDownButton.IsEnabled = selected && index < group!.Questions.Count - 1;
    }

    private void ExitMenuItem_OnClick(object? sender, RoutedEventArgs e) => Close();

    private void ResetQuestionMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedQuestionId is { } id && FindQuestion(id) is { } question)
        {
            LoadQuestion(question);
        }
    }

    private void DuplicateQuestionMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        CommitCurrentQuestion();
        if (_selectedQuestionId is not { } id || FindQuestion(id) is not { } question || _definition is null)
        {
            return;
        }

        var copy = question with { Id = QuestionFactory.Create(question.Type).Id };
        _definition = _definition with
        {
            Groups = _definition.Groups.Select(group =>
            {
                var questions = group.Questions.ToList();
                var index = questions.FindIndex(item => item.Id == id);
                if (index >= 0)
                {
                    questions.Insert(index + 1, copy);
                }

                return group with { Questions = questions };
            }).ToList(),
        };
        BindQuestions();
        SelectQuestion(copy.Id);
    }

    private void MoveQuestionToGroup(string questionId, string groupId)
    {
        CommitCurrentQuestion();
        var question = FindQuestion(questionId);
        if (_definition is null || question is null)
        {
            return;
        }

        _definition = _definition with
        {
            Groups = _definition.Groups.Select(group => group with
            {
                Questions = group.Id == groupId
                    ? group.Questions.Where(item => item.Id != questionId).Append(question).ToList()
                    : group.Questions.Where(item => item.Id != questionId).ToList(),
            }).ToList(),
        };
        BindQuestions(preserveSelection: true);
    }

    private async void EditorHelpMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = TestEditorText.HelpCommand,
            Width = 600,
            Height = 350,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBlock { Text = TestEditorText.HelpBody, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20) },
        };
        await dialog.ShowDialog(this);
    }

    private async void TestSettingsMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        CommitCurrentQuestion();
        if (_definition is null)
        {
            return;
        }

        var title = new TextBox { Text = _definition.Title };
        var description = new TextBox { Text = _definition.Description, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 150 };
        var author = new TextBox { Text = _definition.Author?.Name };
        var email = new TextBox { Text = _definition.Author?.Email };
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        foreach (var (label, control) in new (string, Control)[]
                 {
                     (TestEditorText.TestTitleLabel, title), (TestEditorText.TestDescription, description),
                     (TestEditorText.AuthorName, author), (TestEditorText.AuthorEmail, email),
                 })
        {
            panel.Children.Add(new TextBlock { Text = label });
            panel.Children.Add(control);
        }

        var dialog = CreateWorkspaceDialog(TestEditorText.TestSettings, panel, out var save);
        save.Click += (_, _) => dialog.Close(true);
        if (await dialog.ShowDialog<bool>(this))
        {
            _definition = _definition with
            {
                Title = string.IsNullOrWhiteSpace(title.Text) ? TestEditorText.UntitledTest : title.Text.Trim(),
                Description = description.Text?.Trim(),
                Author = string.IsNullOrWhiteSpace(author.Text) ? null : new AuthorDto(author.Text.Trim(), email.Text?.Trim()),
            };
            TestTitleTextBox.Text = _definition.Title;
            RefreshTestHeader();
        }
    }

    private async void GroupsMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        CommitCurrentQuestion();
        if (_definition is null)
        {
            return;
        }

        var groups = _definition.Groups.OrderBy(group => group.Order).ToList();
        var list = new ListBox
        {
            ItemsSource = groups.ToList(),
            Height = 200,
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<TestGroupDto>((group, _) => new TextBlock { Text = group?.Title }),
        };
        var title = new TextBox();
        var description = new TextBox { AcceptsReturn = true, MinHeight = 70 };
        TestGroupDto? current = null;
        void CommitGroup()
        {
            if (current is not null)
            {
                var index = groups.FindIndex(group => group.Id == current.Id);
                if (index >= 0)
                {
                    groups[index] = current with
                    {
                        Title = string.IsNullOrWhiteSpace(title.Text) ? TestEditorText.DefaultGroupTitle : title.Text.Trim(),
                        Description = description.Text?.Trim(),
                    };
                }
            }
        }

        void Rebind(string? id)
        {
            current = null;
            list.ItemsSource = null;
            list.ItemsSource = groups.ToList();
            list.SelectedItem = groups.FirstOrDefault(group => group.Id == id) ?? groups.FirstOrDefault();
        }

        list.SelectionChanged += (_, _) =>
        {
            CommitGroup();
            current = list.SelectedItem is TestGroupDto selected ? groups.FirstOrDefault(group => group.Id == selected.Id) : null;
            title.Text = current?.Title;
            description.Text = current?.Description;
        };
        var add = new Button { Content = TestEditorText.AddGroup };
        var remove = new Button { Content = TestEditorText.DeleteGroup };
        add.Click += (_, _) =>
        {
            CommitGroup();
            var group = QuestionFactory.CreateDefaultGroup() with { Id = $"group_{Guid.NewGuid():N}", Title = TestEditorText.DefaultGroupTitle, Questions = [] };
            groups.Add(group);
            Rebind(group.Id);
        };
        remove.Click += (_, _) =>
        {
            if (current is null || groups.Count <= 1)
            {
                return;
            }

            var removed = current;
            groups.RemoveAll(group => group.Id == removed.Id);
            groups[0] = groups[0] with { Questions = groups[0].Questions.Concat(removed.Questions).ToList() };
            Rebind(groups[0].Id);
        };
        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(16),
            Children =
            {
                list,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { add, remove } },
                new TextBlock { Text = TestEditorText.GroupTitleLabel }, title,
                new TextBlock { Text = TestEditorText.TestDescription }, description,
                new TextBlock { Text = TestEditorText.GroupDeleteHint, TextWrapping = TextWrapping.Wrap },
            },
        };
        Rebind(groups[0].Id);
        var dialog = CreateWorkspaceDialog(TestEditorText.ManageGroups, panel, out var save);
        dialog.Height = 580;
        save.Click += (_, _) =>
        {
            CommitGroup();
            dialog.Close(true);
        };
        if (await dialog.ShowDialog<bool>(this))
        {
            _definition = _definition with { Groups = groups.Select((group, index) => group with { Order = index }).ToList() };
            GroupTitleTextBox.Text = groups[0].Title;
            BindQuestions(preserveSelection: true);
        }
    }

    private static Window CreateWorkspaceDialog(string title, Control content, out Button save)
    {
        save = new Button { Content = TestEditorText.SaveSettings, IsDefault = true };
        var cancel = new Button { Content = TestEditorText.Cancel, IsCancel = true };
        var dialog = new Window { Title = title, Width = 620, Height = 500, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.Content = new DockPanel
        {
            Children =
            {
                new StackPanel
                {
                    [DockPanel.DockProperty] = Dock.Bottom,
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(16),
                    Spacing = 8,
                    Children = { cancel, save },
                },
                new ScrollViewer { Content = content },
            },
        };
        return dialog;
    }
}
