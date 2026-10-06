using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassCommander.TestEditor.Localization;
using ClassCommander.TestEditor.Services;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestRunner.SmokeTests;

internal static class OrderingEditorChecks
{
    public static void Run()
    {
        var editor = new OrderingOptionsEditor([new OptionDto("first", "First", 1), new OptionDto("second", "Second", 2)], ["first", "second"]);
        var window = new Window { Content = editor, Width = 600, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var fields = editor.GetVisualDescendants().OfType<TextBox>().ToList();
        fields[0].Focus();
        fields[0].Text = "Edited first";
        Require(editor.CollectOptions()[0].Text == "Edited first", "Collect must retain text even before the field loses focus.");
        fields[1].Focus();
        Dispatcher.UIThread.RunJobs();
        Require(editor.CollectOptions()[0].Text == "Edited first", "Leaving an ordering field must retain its value without replacing the row.");
        fields[1].Text = "Edited second";
        var add = editor.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, TestEditorText.AddOption));
        Click(window, add);
        Dispatcher.UIThread.RunJobs();
        var list = editor.GetVisualDescendants().OfType<ListBox>().Single();
        Require(list.ItemCount == 3 && editor.CollectOptions().Count == 2, "Adding an empty row must retain both existing edited answers.");
        list.SelectedIndex = 1;
        var up = editor.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "↑"));
        Click(window, up);
        Dispatcher.UIThread.RunJobs();
        Require(editor.CollectOrderIds().SequenceEqual(new[] { "second", "first" }), "Moving an edited row must preserve answer IDs and the new order.");
        Require(editor.CollectOptions()[0].Text == "Edited second", "Moving must preserve the edited answer text.");
        window.Close();
        Console.WriteLine("PASS: ordering field focus changes, active edits, adding options and moving edited rows.");
    }

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
