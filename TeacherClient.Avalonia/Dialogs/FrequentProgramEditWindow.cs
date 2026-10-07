using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Teacher.Common.Contracts;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Models;

namespace TeacherClient.CrossPlatform.Dialogs;

internal sealed class FrequentProgramEditWindow : Window
{
    private string? _entryId;
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
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("160,*"),
        };

        grid.Children.Add(new TextBlock { Text = CrossPlatformText.ProgramName, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        _nameTextBox = new TextBox();
        Grid.SetColumn(_nameTextBox, 1);
        grid.Children.Add(_nameTextBox);

        var commandLabel = new TextBlock { Text = CrossPlatformText.CommandText, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(commandLabel, 1);
        grid.Children.Add(commandLabel);
        _commandTextBox = new TextBox();
        Grid.SetRow(_commandTextBox, 1);
        Grid.SetColumn(_commandTextBox, 1);
        grid.Children.Add(_commandTextBox);

        var runAsLabel = new TextBlock { Text = CrossPlatformText.RunAs, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(runAsLabel, 2);
        grid.Children.Add(runAsLabel);
        _runAsComboBox = new ComboBox
        {
            ItemsSource = new[]
            {
                CrossPlatformText.RunAsCurrentUser,
                CrossPlatformText.RunAsAdministrator,
            },
            SelectedIndex = 0,
        };
        Grid.SetRow(_runAsComboBox, 2);
        Grid.SetColumn(_runAsComboBox, 1);
        grid.Children.Add(_runAsComboBox);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        var okButton = new Button { Content = CrossPlatformText.Ok, IsDefault = true };
        okButton.Click += OkButton_OnClick;
        buttons.Children.Add(okButton);

        var cancelButton = new Button { Content = CrossPlatformText.Cancel, IsCancel = true };
        cancelButton.Click += CancelButton_OnClick;
        buttons.Children.Add(cancelButton);

        var hint = new TextBlock { Text = CrossPlatformText.AdministratorLaunchHint, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, 12) };
        Grid.SetRow(hint, 3);
        Grid.SetColumnSpan(hint, 2);
        grid.Children.Add(hint);
        Grid.SetRow(buttons, 4);
        Grid.SetColumnSpan(buttons, 2);
        grid.Children.Add(buttons);
        Content = grid;
    }

    public FrequentProgramEditWindow(FrequentProgramEntry entry)
        : this()
    {
        Title = CrossPlatformText.EditProgram;
        _entryId = entry.Id;
        _nameTextBox.Text = entry.DisplayName;
        _commandTextBox.Text = entry.CommandText;
        _runAsComboBox.SelectedIndex = entry.RunAs == RemoteCommandRunAs.Administrator ? 1 : 0;
    }

    public FrequentProgramEntry? ToEntry(string? id = null)
    {
        var displayName = _nameTextBox.Text?.Trim() ?? string.Empty;
        var commandText = _commandTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(commandText))
        {
            return null;
        }

        return new FrequentProgramEntry(
            id ?? _entryId ?? Guid.NewGuid().ToString("N"),
            displayName,
            commandText,
            _runAsComboBox.SelectedIndex == 1 ? RemoteCommandRunAs.Administrator : RemoteCommandRunAs.CurrentUser);
    }

    private async void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var entry = ToEntry();
        if (entry is null)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.ProgramFieldsRequired);
            return;
        }
        Close(entry);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
