using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassCommander.TestRunner.Localization;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestRunner;

#pragma warning disable SA1402

internal interface IAnswerEditor
{
    Control Control { get; }

    AttemptAnswerValueDto? Collect();
}

internal static class AnswerEditorFactory
{
    public static IAnswerEditor Create(QuestionDto question, AttemptAnswerValueDto? existing) => question.Interaction switch
    {
        SingleChoiceInteractionDto single => new SingleChoiceEditor(single, existing as SingleChoiceAnswerValueDto),
        MultipleChoiceInteractionDto multi => new MultipleChoiceEditor(multi, existing as MultipleChoiceAnswerValueDto),
        OrderingInteractionDto ordering => new OrderingEditor(ordering, existing as OrderingAnswerValueDto),
        MatchingInteractionDto matching => new MatchingEditor(matching, existing as MatchingAnswerValueDto),
        TrueFalseGroupInteractionDto tf => new TrueFalseGroupEditor(tf, existing as TrueFalseGroupAnswerValueDto),
        NumericInputGroupInteractionDto numeric => new NumericInputGroupEditor(numeric, existing as NumericInputGroupAnswerValueDto),
        TextInputInteractionDto text => new TextInputEditor(text, existing as TextInputAnswerValueDto),
        ImagePointInteractionDto => new ImagePointEditor(existing as ImagePointAnswerValueDto),
        LetterOrderingInteractionDto => new LetterOrderingEditor(question, existing as LetterOrderingAnswerValueDto),
        _ => new UnsupportedEditor(question.Type.ToString()),
    };
}

internal sealed class UnsupportedEditor : IAnswerEditor
{
    public UnsupportedEditor(string typeName)
    {
        Control = new TextBlock
        {
            Text = typeName,
            Opacity = 0.7,
        };
    }

    public Control Control { get; }

    public AttemptAnswerValueDto? Collect() => null;
}

internal sealed class SingleChoiceEditor : IAnswerEditor
{
    private readonly List<(RadioButton Button, string OptionId)> _options = [];

    public SingleChoiceEditor(SingleChoiceInteractionDto interaction, SingleChoiceAnswerValueDto? existing)
    {
        var selected = existing?.SelectedOptionIds is { Count: > 0 } ids ? ids[0] : null;
        var panel = new StackPanel { Spacing = 6 };
        foreach (var option in interaction.Options.OrderBy(o => o.Order))
        {
            var radio = new RadioButton
            {
                Content = option.Text,
                GroupName = "single-choice",
                IsChecked = string.Equals(option.Id, selected, StringComparison.Ordinal),
            };
            _options.Add((radio, option.Id));
            panel.Children.Add(radio);
        }

        Control = panel;
    }

    public Control Control { get; }

    public AttemptAnswerValueDto? Collect()
    {
        var selected = _options.FirstOrDefault(o => o.Button.IsChecked == true).OptionId;
        return string.IsNullOrWhiteSpace(selected)
            ? null
            : new SingleChoiceAnswerValueDto([selected]);
    }
}

internal sealed class MultipleChoiceEditor : IAnswerEditor
{
    private readonly List<(CheckBox Box, string OptionId)> _options = [];

    public MultipleChoiceEditor(MultipleChoiceInteractionDto interaction, MultipleChoiceAnswerValueDto? existing)
    {
        var selected = existing?.SelectedOptionIds.ToHashSet(StringComparer.Ordinal) ?? [];
        var panel = new StackPanel { Spacing = 6 };
        foreach (var option in interaction.Options.OrderBy(o => o.Order))
        {
            var box = new CheckBox
            {
                Content = option.Text,
                IsChecked = selected.Contains(option.Id),
            };
            _options.Add((box, option.Id));
            panel.Children.Add(box);
        }

        Control = panel;
    }

    public Control Control { get; }

    public AttemptAnswerValueDto? Collect()
    {
        var ids = _options.Where(o => o.Box.IsChecked == true).Select(o => o.OptionId).ToList();
        return ids.Count == 0 ? null : new MultipleChoiceAnswerValueDto(ids);
    }
}

internal sealed class OrderingEditor : IAnswerEditor
{
    private readonly ObservableCollection<OptionDto> _items;

    public OrderingEditor(OrderingInteractionDto interaction, OrderingAnswerValueDto? existing)
    {
        var byId = interaction.Options.ToDictionary(o => o.Id, StringComparer.Ordinal);
        if (existing?.OrderedOptionIds is { Count: > 0 } ordered)
        {
            _items = new ObservableCollection<OptionDto>(
                ordered.Where(byId.ContainsKey).Select(id => byId[id])
                    .Concat(interaction.Options.Where(o => !ordered.Contains(o.Id))));
        }
        else
        {
            _items = new ObservableCollection<OptionDto>(interaction.Options.OrderBy(o => o.Order));
        }

        var list = new ListBox
        {
            ItemsSource = _items,
            MinHeight = 160,
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<OptionDto>((item, _) =>
                new TextBlock { Text = item.Text, Margin = new Thickness(4) }),
        };

        var up = new Button { Content = TestRunnerText.MoveUp, MinWidth = 40 };
        var down = new Button { Content = TestRunnerText.MoveDown, MinWidth = 40 };
        up.Click += (_, _) => MoveSelected(list, -1);
        down.Click += (_, _) => MoveSelected(list, 1);

        Control = new DockPanel
        {
            Children =
            {
                new StackPanel
                {
                    [DockPanel.DockProperty] = Dock.Right,
                    Spacing = 8,
                    Margin = new Thickness(8, 0, 0, 0),
                    Children = { up, down },
                },
                list,
            },
        };
    }

    public Control Control { get; }

    public AttemptAnswerValueDto? Collect() => new OrderingAnswerValueDto(_items.Select(i => i.Id).ToList());

    private void MoveSelected(ListBox list, int delta)
    {
        if (list.SelectedItem is not OptionDto selected)
        {
            return;
        }

        var index = _items.IndexOf(selected);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _items.Count)
        {
            return;
        }

        _items.Move(index, target);
        list.SelectedItem = selected;
    }
}

internal sealed class MatchingEditor : IAnswerEditor
{
    private readonly List<(MatchingItemDto Left, ComboBox Combo)> _rows = [];

    public MatchingEditor(MatchingInteractionDto interaction, MatchingAnswerValueDto? existing)
    {
        var selected = existing?.Pairs.ToDictionary(p => p.LeftId, p => p.RightId, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var rightItems = interaction.RightItems.OrderBy(i => i.Order).ToList();
        var panel = new StackPanel { Spacing = 8 };
        foreach (var left in interaction.LeftItems.OrderBy(i => i.Order))
        {
            var combo = new ComboBox
            {
                Width = 280,
                ItemsSource = rightItems,
                ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<MatchingItemDto>((item, _) =>
                    new TextBlock { Text = item.Text }),
            };

            if (selected.TryGetValue(left.Id, out var rightId))
            {
                combo.SelectedItem = rightItems.FirstOrDefault(r => r.Id == rightId);
            }

            _rows.Add((left, combo));
            panel.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = left.Text,
                        Width = 220,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    combo,
                },
            });
        }

        Control = panel;
    }

    public Control Control { get; }

    public AttemptAnswerValueDto? Collect()
    {
        var pairs = _rows
            .Where(r => r.Combo.SelectedItem is MatchingItemDto)
            .Select(r => new MatchingAnswerValuePairDto(r.Left.Id, ((MatchingItemDto)r.Combo.SelectedItem!).Id))
            .ToList();
        return pairs.Count == 0 ? null : new MatchingAnswerValueDto(pairs);
    }
}

internal sealed class TrueFalseGroupEditor : IAnswerEditor
{
    private readonly List<(StatementDto Statement, ComboBox Combo)> _rows = [];

    public TrueFalseGroupEditor(TrueFalseGroupInteractionDto interaction, TrueFalseGroupAnswerValueDto? existing)
    {
        var selected = existing?.StatementTruth.ToDictionary(s => s.StatementId, s => s.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, bool>(StringComparer.Ordinal);
        var choices = new[] { TestRunnerText.TrueLabel, TestRunnerText.FalseLabel };
        var panel = new StackPanel { Spacing = 8 };
        foreach (var statement in interaction.Statements.OrderBy(s => s.Order))
        {
            var combo = new ComboBox
            {
                Width = 120,
                ItemsSource = choices,
            };
            if (selected.TryGetValue(statement.Id, out var value))
            {
                combo.SelectedItem = value ? TestRunnerText.TrueLabel : TestRunnerText.FalseLabel;
            }

            _rows.Add((statement, combo));
            panel.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = statement.Text,
                        Width = 360,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    combo,
                },
            });
        }

        Control = panel;
    }

    public Control Control { get; }

    public AttemptAnswerValueDto? Collect()
    {
        var values = new List<StatementTruthAnswerValueDto>();
        foreach (var (statement, combo) in _rows)
        {
            if (combo.SelectedItem is not string selected)
            {
                continue;
            }

            values.Add(new StatementTruthAnswerValueDto(
                statement.Id,
                string.Equals(selected, TestRunnerText.TrueLabel, StringComparison.Ordinal)));
        }

        return values.Count == 0 ? null : new TrueFalseGroupAnswerValueDto(values);
    }
}

internal sealed class NumericInputGroupEditor : IAnswerEditor
{
    private readonly List<(NumericEntryDto Entry, TextBox Box)> _rows = [];

    public NumericInputGroupEditor(NumericInputGroupInteractionDto interaction, NumericInputGroupAnswerValueDto? existing)
    {
        var selected = existing?.Entries.ToDictionary(e => e.EntryId, e => e.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, decimal>(StringComparer.Ordinal);
        var panel = new StackPanel { Spacing = 8 };
        foreach (var entry in interaction.Entries.OrderBy(e => e.Order))
        {
            var box = new TextBox { Width = 160 };
            if (selected.TryGetValue(entry.Id, out var value))
            {
                box.Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            _rows.Add((entry, box));
            panel.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = entry.Caption,
                        Width = 280,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    box,
                },
            });
        }

        Control = panel;
    }

    public Control Control { get; }

    public AttemptAnswerValueDto? Collect()
    {
        var entries = new List<NumericEntryAnswerValueDto>();
        foreach (var (entry, box) in _rows)
        {
            if (!decimal.TryParse(
                    box.Text?.Trim(),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value)
                && !decimal.TryParse(
                    box.Text?.Trim(),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.CurrentCulture,
                    out value))
            {
                continue;
            }

            entries.Add(new NumericEntryAnswerValueDto(entry.Id, value));
        }

        return entries.Count == 0 ? null : new NumericInputGroupAnswerValueDto(entries);
    }
}

internal sealed class TextInputEditor : IAnswerEditor
{
    private readonly TextBox _box;

    public TextInputEditor(TextInputInteractionDto interaction, TextInputAnswerValueDto? existing)
    {
        _box = new TextBox
        {
            Watermark = interaction.Placeholder ?? TestRunnerText.TextAnswerPlaceholder,
            Text = existing?.Text ?? string.Empty,
            AcceptsReturn = true,
            MinHeight = 100,
            TextWrapping = TextWrapping.Wrap,
        };
        if (interaction.MaxLength is > 0)
        {
            _box.MaxLength = interaction.MaxLength.Value;
        }

        Control = _box;
    }

    public Control Control { get; }

    public AttemptAnswerValueDto? Collect()
    {
        var text = _box.Text?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(text) ? null : new TextInputAnswerValueDto(text);
    }
}

internal sealed class ImagePointEditor : IAnswerEditor
{
    private readonly TextBox _xBox;
    private readonly TextBox _yBox;

    public ImagePointEditor(ImagePointAnswerValueDto? existing)
    {
        _xBox = new TextBox { Width = 100, Text = existing?.X.ToString(CultureInfo.InvariantCulture) ?? string.Empty };
        _yBox = new TextBox { Width = 100, Text = existing?.Y.ToString(CultureInfo.InvariantCulture) ?? string.Empty };
        Control = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = TestRunnerText.PointHint, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "X", VerticalAlignment = VerticalAlignment.Center },
                        _xBox,
                        new TextBlock { Text = "Y", VerticalAlignment = VerticalAlignment.Center },
                        _yBox,
                    },
                },
            },
        };
    }

    public Control Control { get; }

    public AttemptAnswerValueDto? Collect()
    {
        if (!int.TryParse(_xBox.Text?.Trim(), out var x) || !int.TryParse(_yBox.Text?.Trim(), out var y))
        {
            return null;
        }

        return new ImagePointAnswerValueDto(x, y);
    }
}

internal sealed class LetterOrderingEditor : IAnswerEditor
{
    private readonly TextBox _box;

    public LetterOrderingEditor(QuestionDto question, LetterOrderingAnswerValueDto? existing)
    {
        var source = question.Content is LetterOrderingContentDto content
            ? content.SourceWord
            : string.Empty;
        _box = new TextBox
        {
            Text = existing?.Text ?? string.Empty,
            Watermark = TestRunnerText.TextAnswerPlaceholder,
        };
        Control = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(source) ? string.Empty : source,
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                },
                _box,
            },
        };
    }

    public Control Control { get; }

    public AttemptAnswerValueDto? Collect()
    {
        var text = _box.Text?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(text) ? null : new LetterOrderingAnswerValueDto(text);
    }
}

#pragma warning restore SA1402
