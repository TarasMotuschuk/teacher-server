using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Teacher.Common.Contracts;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Models;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class FrequentProgramsWindow : Window
{
    private readonly ObservableCollection<FrequentProgramEntry> _entries;

    public FrequentProgramsWindow() : this([])
    {
    }

    public FrequentProgramsWindow(IEnumerable<FrequentProgramEntry> entries)
    {
        InitializeComponent();
        _entries = new ObservableCollection<FrequentProgramEntry>(entries.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase));
        ProgramsGrid.ItemsSource = _entries;
        Title = CrossPlatformText.FrequentProgramsTitle;
        AddButton.Content = CrossPlatformText.AddProgram;
        RemoveButton.Content = CrossPlatformText.RemoveProgram;
        CloseButton.Content = CrossPlatformText.Close;
        if (ProgramsGrid.Columns.Count >= 3)
        {
            if (ProgramsGrid.Columns[0] is DataGridTextColumn programNameColumn)
            {
                programNameColumn.Header = CrossPlatformText.ProgramName;
                programNameColumn.Binding = new Avalonia.Data.Binding(nameof(FrequentProgramEntry.DisplayName));
            }

            if (ProgramsGrid.Columns[1] is DataGridTextColumn commandTextColumn)
            {
                commandTextColumn.Header = CrossPlatformText.CommandText;
                commandTextColumn.Binding = new Avalonia.Data.Binding(nameof(FrequentProgramEntry.CommandText));
            }

            if (ProgramsGrid.Columns[2] is DataGridTextColumn runAsColumn)
            {
                runAsColumn.Header = CrossPlatformText.RunAs;
                runAsColumn.Binding = new Avalonia.Data.Binding(nameof(FrequentProgramEntry.RunAs));
            }
        }
    }

    public static async Task<IReadOnlyList<FrequentProgramEntry>?> ShowAsync(Window owner, IReadOnlyList<FrequentProgramEntry> entries)
    {
        var dialog = new FrequentProgramsWindow(entries);
        return await dialog.ShowDialog<IReadOnlyList<FrequentProgramEntry>?>(owner);
    }

    private async void AddButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new FrequentProgramEditWindow();
        var result = await dialog.ShowDialog<FrequentProgramEntry?>(this);
        if (result is null)
        {
            return;
        }

        _entries.Add(result);
        SortEntries();
    }

    private async void RemoveButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ProgramsGrid.SelectedItem is not FrequentProgramEntry entry)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.ChooseProgramFirst);
            return;
        }

        _entries.Remove(entry);
    }

    private void CloseButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(_entries.ToList());
    }

    private void SortEntries()
    {
        var items = _entries.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.CommandText, StringComparer.OrdinalIgnoreCase).ToList();
        _entries.Clear();
        foreach (var item in items)
        {
            _entries.Add(item);
        }
    }
}

file sealed class FrequentProgramEditWindow : Window
{
    private readonly TextBox _nameTextBox;
    private readonly TextBox _commandTextBox;
    private readonly ComboBox _runAsComboBox;

    public FrequentProgramEditWindow()
    {
        Width = 760;
        Height = 360;
        MinWidth = 680;
        MinHeight = 320;
        Title = CrossPlatformText.AddProgram;

        var grid = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("160,*")
        };

        grid.Children.Add(new TextBlock { Text = CrossPlatformText.ProgramName, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        _nameTextBox = new TextBox();
        Grid.SetColumn(_nameTextBox, 1);
        grid.Children.Add(_nameTextBox);

        var commandLabel = new TextBlock { Text = CrossPlatformText.CommandText, Margin = new Thickness(0, 14, 0, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        Grid.SetRow(commandLabel, 1);
        grid.Children.Add(commandLabel);
        _commandTextBox = new TextBox { AcceptsReturn = true, Height = 120, Margin = new Thickness(0, 14, 0, 0) };
        Grid.SetRow(_commandTextBox, 1);
        Grid.SetColumn(_commandTextBox, 1);
        grid.Children.Add(_commandTextBox);

        var runAsLabel = new TextBlock { Text = CrossPlatformText.RunAs, Margin = new Thickness(0, 14, 0, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        Grid.SetRow(runAsLabel, 2);
        grid.Children.Add(runAsLabel);
        _runAsComboBox = new ComboBox { Margin = new Thickness(0, 14, 0, 0), ItemsSource = new[] { CrossPlatformText.RunAsCurrentUser, CrossPlatformText.RunAsAdministrator }, SelectedIndex = 0 };
        Grid.SetRow(_runAsComboBox, 2);
        Grid.SetColumn(_runAsComboBox, 1);
        grid.Children.Add(_runAsComboBox);

        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 12, Margin = new Thickness(0, 20, 0, 0) };
        var cancelButton = new Button { Content = CrossPlatformText.Cancel, MinWidth = 120 };
        cancelButton.Click += (_, _) => Close(null);
        var okButton = new Button { Content = CrossPlatformText.Ok, MinWidth = 120 };
        okButton.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text) || string.IsNullOrWhiteSpace(_commandTextBox.Text))
            {
                await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.CommandScriptRequired);
                return;
            }

            Close(FrequentProgramEntry.Create(
                _nameTextBox.Text.Trim(),
                _commandTextBox.Text.Trim(),
                _runAsComboBox.SelectedIndex == 1 ? RemoteCommandRunAs.Administrator : RemoteCommandRunAs.CurrentUser));
        };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(okButton);
        Grid.SetRow(buttons, 3);
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);

        Content = grid;
    }
}
