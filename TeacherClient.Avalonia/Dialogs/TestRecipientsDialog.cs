using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

internal static class TestRecipientsDialog
{
    public static async Task<IReadOnlyList<DiscoveredAgentRow>?> ShowAsync(
        Window owner,
        string assignmentTitle,
        IReadOnlyList<DiscoveredAgentRow> online,
        IReadOnlyList<DiscoveredAgentRow> selected)
    {
        var dialog = new Window
        {
            Title = CrossPlatformText.TestingChooseStudents,
            Width = 620,
            Height = 560,
            MinWidth = 460,
            MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var list = new StackPanel { Spacing = 6 };
        var selectedIds = selected.Select(agent => agent.AgentId).ToHashSet(StringComparer.Ordinal);
        var choices = online.Select(agent => (Agent: agent, Check: new CheckBox
        {
            Content = $"{agent.MachineName} — {agent.CurrentUser} ({agent.RespondingAddress})",
            IsChecked = selectedIds.Contains(agent.AgentId),
        })).ToList();
        var count = new TextBlock();
        var start = new Button { Content = CrossPlatformText.TestingLaunchNow, MinHeight = 40 };
        void UpdateCount()
        {
            var total = choices.Count(item => item.Check.IsChecked == true);
            count.Text = CrossPlatformText.TestingSelectedCount(total);
            start.IsEnabled = total > 0;
        }

        foreach (var item in choices)
        {
            item.Check.IsCheckedChanged += (_, _) => UpdateCount();
            list.Children.Add(item.Check);
        }

        var all = new CheckBox { Content = CrossPlatformText.TestingSelectAllRecipients };
        all.IsCheckedChanged += (_, _) =>
        {
            foreach (var item in choices)
            {
                item.Check.IsChecked = all.IsChecked == true;
            }
        };
        var cancel = new Button { Content = CrossPlatformText.Cancel };
        cancel.Click += (_, _) => dialog.Close();
        start.Click += (_, _) => dialog.Close(
            choices.Where(item => item.Check.IsChecked == true).Select(item => item.Agent).ToList());
        var header = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 0, 0, 12),
            Children =
            {
                new TextBlock { Text = assignmentTitle, FontSize = 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = CrossPlatformText.TestingRecipientsHint, TextWrapping = TextWrapping.Wrap },
                all,
            },
        };
        var footer = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 12, 0, 0),
            Children =
            {
                count,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancel, start },
                },
            },
        };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        dialog.Content = new DockPanel
        {
            Margin = new Thickness(20),
            Children = { header, footer, new ScrollViewer { Content = list } },
        };
        UpdateCount();
        return await dialog.ShowDialog<IReadOnlyList<DiscoveredAgentRow>?>(owner);
    }
}
