using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace ClassCommander.TestEditor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public ObservableCollection<EditorFocusItem> FocusItems { get; } =
    [
        new("Canonical DTO Core", "Працюємо поверх Teacher.Common.Contracts.Testing, а не поверх MyTest XML напряму."),
        new("Question Editors", "Окремі редактори для single-choice, matching, image-point, letter-ordering та інших типів."),
        new("Assets Pipeline", "Усі вхідні зображення нормалізуються в webp перед збереженням у .cctest."),
        new("Package Workflow", "Open, Save, Save As, Import XML, Export .cctest."),
    ];
}
