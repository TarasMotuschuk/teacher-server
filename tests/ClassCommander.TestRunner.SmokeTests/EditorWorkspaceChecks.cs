using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassCommander.TestEditor.Localization;
using ClassCommander.TestEditor.Services;
using Teacher.Common.Contracts.Testing;
using Teacher.Common.Localization;

namespace ClassCommander.TestRunner.SmokeTests;

internal static class EditorWorkspaceChecks
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "classcommander-editor-workspace");
        Directory.CreateDirectory(directory);
        foreach (var language in new[] { UiLanguage.Ukrainian, UiLanguage.English })
        {
            var window = new ClassCommander.TestEditor.MainWindow();
            TestEditorText.Language = language;
            Call(window, "ApplyLocalization");
            var definition = Definition(window);
            var question = QuestionFactory.Create(QuestionType.SingleChoice) with
            {
                Prompt = language == UiLanguage.Ukrainian ? "Який пристрій використовується для введення тексту?" : "Which device is used to enter text?",
                Interaction = new SingleChoiceInteractionDto([new OptionDto("a", "Keyboard", 1), new OptionDto("b", "Monitor", 2)]),
                AnswerKey = new ChoiceAnswerKeyDto(["a"]),
            };
            SetDefinition(window, definition with { Groups = [definition.Groups[0] with { Questions = [question] }] });
            Call(window, "BindQuestions", false);
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var host = (QuestionEditorHost)window.GetType().GetField("_editor", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            var choices = host.Control.GetVisualDescendants().OfType<ChoiceOptionsEditor>().Single();
            choices.GetVisualDescendants().OfType<TextBox>().First().Text = "Edited answer";
            choices.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, TestEditorText.AddOption)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(choices.CollectOptions()[0].Text == "Edited answer", "Adding an answer must retain the active text edit.");
            Call(window, "DuplicateQuestionMenuItem_OnClick", null, new RoutedEventArgs());
            var questions = Definition(window).Groups[0].Questions;
            Require(questions.Count == 2 && questions[0].Id != questions[1].Id && questions[0].Prompt == questions[1].Prompt, "Duplicate must preserve content and use a new question ID.");
            var prompt = (TextBox)host.GetType().GetField("_promptBox", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host)!;
            prompt.Text = "Discard this draft";
            Call(window, "ResetQuestionMenuItem_OnClick", null, new RoutedEventArgs());
            Require(host.Collect()!.Prompt == question.Prompt, "Reset must restore the saved question.");
            Call(window, "TestSettingsMenuItem_OnClick", null, new RoutedEventArgs());
            Dispatcher.UIThread.RunJobs();
            var settings = window.OwnedWindows.Single();
            var fields = settings.GetVisualDescendants().OfType<TextBox>().ToList();
            fields[0].Text = "Edited title";
            fields[1].Text = "Test description";
            fields[2].Text = "Teacher";
            settings.GetVisualDescendants().OfType<Button>().Single(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Require(Definition(window).Title == "Edited title" && Definition(window).Author?.Name == "Teacher", "The title dialog must persist metadata.");
            Call(window, "GroupsMenuItem_OnClick", null, new RoutedEventArgs());
            Dispatcher.UIThread.RunJobs();
            var groupsDialog = window.OwnedWindows.Single();
            groupsDialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, TestEditorText.AddGroup)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            groupsDialog.GetVisualDescendants().OfType<TextBox>().First().Text = "Second group";
            groupsDialog.GetVisualDescendants().OfType<Button>().Single(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Require(Definition(window).Groups.Count == 2 && Definition(window).Groups[1].Title == "Second group", "Group editing must persist the added group.");
            var selectedId = Definition(window).Groups[0].Questions[1].Id;
            Call(window, "MoveQuestionToGroup", selectedId, Definition(window).Groups[1].Id);
            Require(Definition(window).Groups.Sum(group => group.Questions.Count) == 2 && Definition(window).Groups[1].Questions.Single().Id == selectedId, "Moving a question between groups must preserve all questions.");
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()!.Save(Path.Combine(directory, $"editor-{language}.png"));
            window.Width = 900;
            window.Height = 620;
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()!.Save(Path.Combine(directory, $"editor-small-{language}.png"));
            Call(window, "GroupsMenuItem_OnClick", null, new RoutedEventArgs());
            Dispatcher.UIThread.RunJobs();
            var deleteDialog = window.OwnedWindows.Single();
            deleteDialog.GetVisualDescendants().OfType<ListBox>().Single().SelectedIndex = 1;
            deleteDialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, TestEditorText.DeleteGroup)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            deleteDialog.GetVisualDescendants().OfType<Button>().Single(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Require(Definition(window).Groups.Count == 1 && Definition(window).Groups[0].Questions.Count == 2, "Deleting a group must transfer its questions without losing them.");
            foreach (var type in Enum.GetValues<QuestionType>())
            {
                var typedQuestion = QuestionFactory.Create(type) with { Prompt = "Example question" };
                host.Load(typedQuestion);
                Dispatcher.UIThread.RunJobs();
                Require(host.Collect() is { Prompt: "Example question" }, $"The workspace must support {type}.");
            }

            window.Close();
        }

        Console.WriteLine($"PASS: editor menus, metadata, groups, duplicate/reset, active answer edits. Screenshots: {directory}");
    }

    private static object? Call(ClassCommander.TestEditor.MainWindow window, string method, params object?[] args)
        => window.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, args);

    private static TestDefinitionDto Definition(ClassCommander.TestEditor.MainWindow window)
        => (TestDefinitionDto)window.GetType().GetField("_definition", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;

    private static void SetDefinition(ClassCommander.TestEditor.MainWindow window, TestDefinitionDto definition)
        => window.GetType().GetField("_definition", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, definition);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
