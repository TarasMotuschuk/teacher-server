using System.Collections.ObjectModel;
using Avalonia.Controls;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Models;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class FrequentProgramsWindow : Window
{
    private readonly ObservableCollection<FrequentProgramEntry> _entries;

    public FrequentProgramsWindow()
        : this([])
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
