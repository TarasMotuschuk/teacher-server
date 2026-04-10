using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Teacher.Common.Contracts;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Models;

namespace TeacherClient.CrossPlatform.Dialogs;

internal sealed class FrequentProgramEditWindow : Window
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
            ColumnDefinitions = new ColumnDefinitions("160,*"),
        };

        grid.Children.Add(new TextBlock { Text = CrossPlatformText.ProgramName, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        _nameTextBox = new TextBox();
        Grid.SetColumn(_nameTextBox, 1);
        grid.Children.Add(_nameTextBox);

        grid.Children.Add(new TextBlock { Text = CrossPlatformText.CommandText, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        _commandTextBox = new TextBox();
        Grid.SetRow(_commandTextBox, 1);
        Grid.SetColumn(_commandTextBox, 1);
        grid.Children.Add(_commandTextBox);

        grid.Children.Add(new TextBlock { Text = CrossPlatformText.RunAs, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
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

        Grid.SetRow(buttons, 3);
        Grid.SetColumnSpan(buttons, 2);
        grid.Children.Add(buttons);
        Content = grid;
    }

    public FrequentProgramEditWindow(FrequentProgramEntry entry)
        : this()
    {
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

        return FrequentProgramEntry.Create(
            displayName,
            commandText,
            _runAsComboBox.SelectedIndex == 1 ? RemoteCommandRunAs.Administrator : RemoteCommandRunAs.CurrentUser);
    }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ToEntry() is null)
        {
            return;
        }

        var entry = ToEntry();
        if (entry is null)
        {
            return;
        }

        Close(entry);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
