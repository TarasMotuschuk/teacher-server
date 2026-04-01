using Avalonia.Controls;
using Avalonia.Interactivity;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class RegistryEditDialog : Window
{
    public RegistryEditDialog()
    {
        InitializeComponent();
    }

    public RegistryEditDialog(string? initialName = null, string? initialType = null, string? initialData = null)
        : this()
    {
        Title = CrossPlatformText.EditValue;
        NameTextBox.Text = initialName ?? string.Empty;
        var types = new[] { "REG_SZ", "REG_EXPAND_SZ", "REG_DWORD", "REG_QWORD", "REG_BINARY", "REG_MULTI_SZ" };
        foreach (var type in types)
            TypeComboBox.Items.Add(type);
        TypeComboBox.SelectedItem = initialType ?? "REG_SZ";
        DataTextBox.Text = initialData ?? string.Empty;
    }

    public string ValueName => NameTextBox.Text?.Trim() ?? string.Empty;
    public string ValueType => TypeComboBox.SelectedItem?.ToString() ?? "REG_SZ";
    public string ValueData => DataTextBox.Text ?? string.Empty;

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            return;
        }

        Close(true);
    }
}
