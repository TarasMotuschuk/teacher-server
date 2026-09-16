using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassCommander.TestEditor.Localization;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestEditor.Services;

internal sealed class QuestionEditorHost
{
    private readonly StackPanel _root = new() { Spacing = 10 };
    private QuestionDto? _current;
    private TextBox? _promptBox;
    private TextBox? _descriptionBox;
    private TextBox? _scoreBox;
    private CheckBox? _requiredBox;
    private Control? _typeEditor;

    public Control Control => _root;

    public void Clear()
    {
        _current = null;
        _root.Children.Clear();
        _promptBox = null;
        _descriptionBox = null;
        _scoreBox = null;
        _requiredBox = null;
        _typeEditor = null;
    }

    public void Load(QuestionDto question)
    {
        _current = question;
        _root.Children.Clear();

        _root.Children.Add(Label(TestEditorText.TypeLabel));
        _root.Children.Add(new TextBlock
        {
            Text = TestEditorText.QuestionTypeName(question.Type),
            FontWeight = FontWeight.SemiBold,
        });

        _root.Children.Add(Label(TestEditorText.PromptLabel));
        _promptBox = new TextBox
        {
            Text = question.Prompt,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 72,
        };
        _root.Children.Add(_promptBox);

        _root.Children.Add(Label(TestEditorText.DescriptionLabel));
        _descriptionBox = new TextBox
        {
            Text = question.Description ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 48,
        };
        _root.Children.Add(_descriptionBox);

        var scoreRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        _scoreBox = new TextBox
        {
            Width = 100,
            Text = question.Score.ToString("0.##", CultureInfo.InvariantCulture),
        };
        _requiredBox = new CheckBox
        {
            Content = TestEditorText.RequiredLabel,
            IsChecked = question.Required,
            VerticalAlignment = VerticalAlignment.Center,
        };
        scoreRow.Children.Add(Label(TestEditorText.ScoreLabel));
        scoreRow.Children.Add(_scoreBox);
        scoreRow.Children.Add(_requiredBox);
        _root.Children.Add(scoreRow);

        _root.Children.Add(new Separator { Margin = new Thickness(0, 8) });
        _root.Children.Add(Label(TestEditorText.OptionsLabel));

        _typeEditor = question.Type switch
        {
            QuestionType.SingleChoice => new ChoiceOptionsEditor(
                ((SingleChoiceInteractionDto)question.Interaction).Options,
                ((ChoiceAnswerKeyDto)question.AnswerKey).CorrectOptionIds,
                multiCorrect: false),
            QuestionType.MultipleChoice => new ChoiceOptionsEditor(
                ((MultipleChoiceInteractionDto)question.Interaction).Options,
                ((ChoiceAnswerKeyDto)question.AnswerKey).CorrectOptionIds,
                multiCorrect: true),
            QuestionType.Ordering => new OrderingOptionsEditor(
                ((OrderingInteractionDto)question.Interaction).Options,
                ((OrderingAnswerKeyDto)question.AnswerKey).CorrectOrder),
            QuestionType.Matching => new MatchingEditor(
                (MatchingInteractionDto)question.Interaction,
                (MatchingAnswerKeyDto)question.AnswerKey),
            QuestionType.TrueFalseGroup => new TrueFalseEditor(
                (TrueFalseGroupInteractionDto)question.Interaction,
                (TrueFalseGroupAnswerKeyDto)question.AnswerKey),
            QuestionType.NumericInputGroup => new NumericEditor(
                (NumericInputGroupInteractionDto)question.Interaction,
                (NumericInputGroupAnswerKeyDto)question.AnswerKey),
            QuestionType.TextInput => new TextInputEditor(
                (TextInputInteractionDto)question.Interaction,
                (TextInputAnswerKeyDto)question.AnswerKey),
            QuestionType.ImagePoint => new ImagePointEditor((ImagePointAnswerKeyDto)question.AnswerKey),
            QuestionType.LetterOrdering => new LetterOrderingEditor(
                question.Content as LetterOrderingContentDto,
                (LetterOrderingAnswerKeyDto)question.AnswerKey),
            _ => new TextBlock { Text = question.Type.ToString() },
        };
        _root.Children.Add(_typeEditor);
    }

    public QuestionDto? Collect()
    {
        if (_current is null || _promptBox is null || _scoreBox is null || _requiredBox is null)
        {
            return null;
        }

        if (!decimal.TryParse(
                _scoreBox.Text?.Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var score)
            && !decimal.TryParse(
                _scoreBox.Text?.Trim(),
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out score))
        {
            score = _current.Score;
        }

        var prompt = _promptBox.Text?.Trim() ?? string.Empty;
        var description = string.IsNullOrWhiteSpace(_descriptionBox?.Text)
            ? null
            : _descriptionBox.Text.Trim();

        return _typeEditor switch
        {
            ChoiceOptionsEditor choice when _current.Type == QuestionType.SingleChoice => _current with
            {
                Prompt = prompt,
                Description = description,
                Score = score,
                Required = _requiredBox.IsChecked == true,
                Interaction = new SingleChoiceInteractionDto(choice.CollectOptions()),
                AnswerKey = new ChoiceAnswerKeyDto(choice.CollectCorrectIds()),
            },
            ChoiceOptionsEditor choice when _current.Type == QuestionType.MultipleChoice => _current with
            {
                Prompt = prompt,
                Description = description,
                Score = score,
                Required = _requiredBox.IsChecked == true,
                Interaction = new MultipleChoiceInteractionDto(choice.CollectOptions()),
                AnswerKey = new ChoiceAnswerKeyDto(choice.CollectCorrectIds()),
            },
            OrderingOptionsEditor ordering => _current with
            {
                Prompt = prompt,
                Description = description,
                Score = score,
                Required = _requiredBox.IsChecked == true,
                Interaction = new OrderingInteractionDto(ordering.CollectOptions()),
                AnswerKey = new OrderingAnswerKeyDto(ordering.CollectOrderIds()),
            },
            MatchingEditor matching => _current with
            {
                Prompt = prompt,
                Description = description,
                Score = score,
                Required = _requiredBox.IsChecked == true,
                Interaction = matching.CollectInteraction(),
                AnswerKey = matching.CollectAnswerKey(),
            },
            TrueFalseEditor tf => _current with
            {
                Prompt = prompt,
                Description = description,
                Score = score,
                Required = _requiredBox.IsChecked == true,
                Interaction = tf.CollectInteraction(),
                AnswerKey = tf.CollectAnswerKey(),
            },
            NumericEditor numeric => _current with
            {
                Prompt = prompt,
                Description = description,
                Score = score,
                Required = _requiredBox.IsChecked == true,
                Interaction = numeric.CollectInteraction(),
                AnswerKey = numeric.CollectAnswerKey(),
            },
            TextInputEditor text => _current with
            {
                Prompt = prompt,
                Description = description,
                Score = score,
                Required = _requiredBox.IsChecked == true,
                Interaction = text.CollectInteraction(),
                AnswerKey = text.CollectAnswerKey(),
            },
            ImagePointEditor image => _current with
            {
                Prompt = prompt,
                Description = description,
                Score = score,
                Required = _requiredBox.IsChecked == true,
                AnswerKey = image.CollectAnswerKey(),
            },
            LetterOrderingEditor letters => _current with
            {
                Prompt = prompt,
                Description = description,
                Score = score,
                Required = _requiredBox.IsChecked == true,
                Content = letters.CollectContent(),
                AnswerKey = letters.CollectAnswerKey(),
            },
            _ => _current with
            {
                Prompt = prompt,
                Description = description,
                Score = score,
                Required = _requiredBox.IsChecked == true,
            },
        };
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 4, 0, 0),
    };
}

#pragma warning disable SA1402

internal sealed class ChoiceOptionsEditor : UserControl
{
    private readonly bool _multiCorrect;
    private readonly ObservableCollection<OptionEditRow> _rows = [];
    private readonly StackPanel _list = new() { Spacing = 6 };

    public ChoiceOptionsEditor(IReadOnlyList<OptionDto> options, IReadOnlyList<string> correctIds, bool multiCorrect)
    {
        _multiCorrect = multiCorrect;
        var correct = correctIds.ToHashSet(StringComparer.Ordinal);
        foreach (var option in options.OrderBy(o => o.Order))
        {
            _rows.Add(new OptionEditRow(option.Id, option.Text, correct.Contains(option.Id)));
        }

        var add = new Button { Content = TestEditorText.AddOption, MinWidth = 120 };
        add.Click += (_, _) =>
        {
            _rows.Add(new OptionEditRow($"opt_{Guid.NewGuid():N}"[..10], string.Empty, false));
            Rebuild();
        };

        Content = new StackPanel
        {
            Spacing = 8,
            Children = { _list, add },
        };
        Rebuild();
    }

    public IReadOnlyList<OptionDto> CollectOptions()
    {
        SyncFromUi();
        return _rows
            .Select((row, index) => new OptionDto(row.Id, row.Text.Trim(), index + 1))
            .Where(o => !string.IsNullOrWhiteSpace(o.Text))
            .ToList();
    }

    public IReadOnlyList<string> CollectCorrectIds()
    {
        SyncFromUi();
        return _rows.Where(r => r.IsCorrect && !string.IsNullOrWhiteSpace(r.Text)).Select(r => r.Id).ToList();
    }

    private void Rebuild()
    {
        _list.Children.Clear();
        foreach (var row in _rows)
        {
            var text = new TextBox { Text = row.Text, Width = 360 };
            text.LostFocus += (_, _) => row.Text = text.Text ?? string.Empty;

            Control marker;
            if (_multiCorrect)
            {
                var box = new CheckBox { IsChecked = row.IsCorrect, VerticalAlignment = VerticalAlignment.Center };
                box.IsCheckedChanged += (_, _) => row.IsCorrect = box.IsChecked == true;
                marker = box;
            }
            else
            {
                var radio = new RadioButton
                {
                    GroupName = "choice-correct",
                    IsChecked = row.IsCorrect,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                radio.IsCheckedChanged += (_, _) =>
                {
                    if (radio.IsChecked == true)
                    {
                        foreach (var other in _rows)
                        {
                            other.IsCorrect = ReferenceEquals(other, row);
                        }
                    }
                };
                marker = radio;
            }

            var remove = new Button { Content = "×", Width = 32 };
            var captured = row;
            remove.Click += (_, _) =>
            {
                _rows.Remove(captured);
                Rebuild();
            };

            _list.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    marker,
                    text,
                    remove,
                },
            });
        }
    }

    private void SyncFromUi()
    {
        // Text values are synced on LostFocus; ensure current edits are captured from children.
        for (var i = 0; i < _list.Children.Count && i < _rows.Count; i++)
        {
            if (_list.Children[i] is StackPanel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is TextBox box)
                    {
                        _rows[i].Text = box.Text ?? string.Empty;
                    }
                }
            }
        }
    }

    private sealed class OptionEditRow(string id, string text, bool isCorrect)
    {
        public string Id { get; } = id;

        public string Text { get; set; } = text;

        public bool IsCorrect { get; set; } = isCorrect;
    }
}

internal sealed class OrderingOptionsEditor : UserControl
{
    private readonly ObservableCollection<OptionDto> _items;

    public OrderingOptionsEditor(IReadOnlyList<OptionDto> options, IReadOnlyList<string> correctOrder)
    {
        var byId = options.ToDictionary(o => o.Id, StringComparer.Ordinal);
        _items = correctOrder.Count > 0
            ? new ObservableCollection<OptionDto>(
                correctOrder.Where(byId.ContainsKey).Select(id => byId[id])
                    .Concat(options.Where(o => !correctOrder.Contains(o.Id))))
            : new ObservableCollection<OptionDto>(options.OrderBy(o => o.Order));

        var list = new ListBox
        {
            ItemsSource = _items,
            MinHeight = 140,
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<OptionDto>((item, _) =>
            {
                var box = new TextBox { Text = item.Text, MinWidth = 280 };
                box.LostFocus += (_, _) =>
                {
                    var idx = _items.IndexOf(item);
                    if (idx >= 0)
                    {
                        _items[idx] = item with { Text = box.Text?.Trim() ?? string.Empty };
                    }
                };
                return box;
            }),
        };

        var up = new Button { Content = "↑", Width = 36 };
        var down = new Button { Content = "↓", Width = 36 };
        var add = new Button { Content = TestEditorText.AddOption, MinWidth = 100 };
        up.Click += (_, _) => Move(list, -1);
        down.Click += (_, _) => Move(list, 1);
        add.Click += (_, _) =>
        {
            _items.Add(new OptionDto($"opt_{Guid.NewGuid():N}"[..10], string.Empty, _items.Count + 1));
        };

        Content = new DockPanel
        {
            Children =
            {
                new StackPanel
                {
                    [DockPanel.DockProperty] = Dock.Right,
                    Spacing = 8,
                    Margin = new Thickness(8, 0, 0, 0),
                    Children = { up, down, add },
                },
                list,
            },
        };
    }

    public IReadOnlyList<OptionDto> CollectOptions()
        => _items.Select((item, index) => item with { Order = index + 1 }).Where(i => !string.IsNullOrWhiteSpace(i.Text)).ToList();

    public IReadOnlyList<string> CollectOrderIds() => CollectOptions().Select(o => o.Id).ToList();

    private void Move(ListBox list, int delta)
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

internal sealed class MatchingEditor : UserControl
{
    private readonly ObservableCollection<MatchingItemDto> _left;
    private readonly ObservableCollection<MatchingItemDto> _right;
    private readonly Dictionary<string, ComboBox> _pairCombos = new(StringComparer.Ordinal);
    private readonly StackPanel _pairs = new() { Spacing = 6 };

    public MatchingEditor(MatchingInteractionDto interaction, MatchingAnswerKeyDto answerKey)
    {
        _left = new ObservableCollection<MatchingItemDto>(interaction.LeftItems.OrderBy(i => i.Order));
        _right = new ObservableCollection<MatchingItemDto>(interaction.RightItems.OrderBy(i => i.Order));
        var selected = answerKey.Pairs.ToDictionary(p => p.LeftId, p => p.RightId, StringComparer.Ordinal);

        var leftBox = new TextBox
        {
            AcceptsReturn = true,
            MinHeight = 80,
            Text = string.Join(Environment.NewLine, _left.Select(i => i.Text)),
            Watermark = TestEditorText.MatchingLeftHint,
        };
        var rightBox = new TextBox
        {
            AcceptsReturn = true,
            MinHeight = 80,
            Text = string.Join(Environment.NewLine, _right.Select(i => i.Text)),
            Watermark = TestEditorText.MatchingRightHint,
        };
        leftBox.LostFocus += (_, _) => RebuildItems(leftBox.Text, rightBox.Text, keepPairs: true);
        rightBox.LostFocus += (_, _) => RebuildItems(leftBox.Text, rightBox.Text, keepPairs: true);

        Content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = TestEditorText.MatchingLeftHint, Opacity = 0.8 },
                leftBox,
                new TextBlock { Text = TestEditorText.MatchingRightHint, Opacity = 0.8 },
                rightBox,
                new TextBlock { Text = TestEditorText.AnswerKeyLabel, FontWeight = FontWeight.SemiBold },
                _pairs,
            },
        };

        RebuildPairs(selected);
    }

    public MatchingInteractionDto CollectInteraction()
        => new(_left.ToList(), _right.ToList());

    public MatchingAnswerKeyDto CollectAnswerKey()
    {
        var pairs = new List<MatchingPairDto>();
        foreach (var (leftId, combo) in _pairCombos)
        {
            if (combo.SelectedItem is MatchingItemDto right)
            {
                pairs.Add(new MatchingPairDto(leftId, right.Id));
            }
        }

        return new MatchingAnswerKeyDto(pairs);
    }

    private void RebuildItems(string? leftText, string? rightText, bool keepPairs)
    {
        var previous = keepPairs
            ? _pairCombos.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.SelectedItem is MatchingItemDto item ? item.Id : null,
                StringComparer.Ordinal)
            : new Dictionary<string, string?>(StringComparer.Ordinal);

        _left.Clear();
        _right.Clear();
        var leftLines = SplitLines(leftText);
        var rightLines = SplitLines(rightText);
        for (var i = 0; i < leftLines.Count; i++)
        {
            _left.Add(new MatchingItemDto($"left_{i + 1}", leftLines[i], i + 1));
        }

        for (var i = 0; i < rightLines.Count; i++)
        {
            _right.Add(new MatchingItemDto($"right_{i + 1}", rightLines[i], i + 1));
        }

        var mapped = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var left in _left)
        {
            if (previous.TryGetValue(left.Id, out var rightId) && rightId is not null)
            {
                mapped[left.Id] = rightId;
            }
        }

        RebuildPairs(mapped);
    }

    private void RebuildPairs(IReadOnlyDictionary<string, string> selected)
    {
        _pairs.Children.Clear();
        _pairCombos.Clear();
        foreach (var left in _left)
        {
            var combo = new ComboBox
            {
                Width = 240,
                ItemsSource = _right.ToList(),
                ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<MatchingItemDto>((item, _) =>
                    new TextBlock { Text = item.Text }),
            };
            if (selected.TryGetValue(left.Id, out var rightId))
            {
                combo.SelectedItem = _right.FirstOrDefault(r => r.Id == rightId);
            }

            _pairCombos[left.Id] = combo;
            _pairs.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = left.Text,
                        Width = 200,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    combo,
                },
            });
        }
    }

    private static List<string> SplitLines(string? text)
        => (text ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
}

internal sealed class TrueFalseEditor : UserControl
{
    private readonly List<(TextBox Text, ComboBox Value)> _rows = [];

    public TrueFalseEditor(TrueFalseGroupInteractionDto interaction, TrueFalseGroupAnswerKeyDto answerKey)
    {
        var truth = answerKey.StatementTruth.ToDictionary(s => s.StatementId, s => s.Value, StringComparer.Ordinal);
        var panel = new StackPanel { Spacing = 6 };
        foreach (var statement in interaction.Statements.OrderBy(s => s.Order))
        {
            var text = new TextBox { Text = statement.Text, Width = 360 };
            var combo = new ComboBox
            {
                Width = 100,
                ItemsSource = new[] { TestEditorText.TrueLabel, TestEditorText.FalseLabel },
                SelectedItem = truth.TryGetValue(statement.Id, out var value) && value
                    ? TestEditorText.TrueLabel
                    : TestEditorText.FalseLabel,
            };
            _rows.Add((text, combo));
            panel.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { text, combo },
            });
        }

        var add = new Button { Content = TestEditorText.AddStatement, MinWidth = 120 };
        add.Click += (_, _) =>
        {
            var text = new TextBox { Width = 360 };
            var combo = new ComboBox
            {
                Width = 100,
                ItemsSource = new[] { TestEditorText.TrueLabel, TestEditorText.FalseLabel },
                SelectedItem = TestEditorText.TrueLabel,
            };
            _rows.Add((text, combo));
            panel.Children.Insert(panel.Children.Count - 1, new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { text, combo },
            });
        };
        panel.Children.Add(add);
        Content = panel;
    }

    public TrueFalseGroupInteractionDto CollectInteraction()
    {
        var statements = new List<StatementDto>();
        for (var i = 0; i < _rows.Count; i++)
        {
            var text = _rows[i].Text.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            statements.Add(new StatementDto($"st_{i + 1}", text, statements.Count + 1));
        }

        return new TrueFalseGroupInteractionDto(statements);
    }

    public TrueFalseGroupAnswerKeyDto CollectAnswerKey()
    {
        var interaction = CollectInteraction();
        var values = new List<StatementTruthDto>();
        var filled = _rows.Where(r => !string.IsNullOrWhiteSpace(r.Text.Text)).ToList();
        for (var i = 0; i < interaction.Statements.Count && i < filled.Count; i++)
        {
            values.Add(new StatementTruthDto(
                interaction.Statements[i].Id,
                string.Equals(filled[i].Value.SelectedItem as string, TestEditorText.TrueLabel, StringComparison.Ordinal)));
        }

        return new TrueFalseGroupAnswerKeyDto(values);
    }
}

internal sealed class NumericEditor : UserControl
{
    private readonly List<(TextBox Caption, TextBox Value)> _rows = [];

    public NumericEditor(NumericInputGroupInteractionDto interaction, NumericInputGroupAnswerKeyDto answerKey)
    {
        var values = answerKey.Values.ToDictionary(v => v.EntryId, v => v.AcceptedNumbers, StringComparer.Ordinal);
        var panel = new StackPanel { Spacing = 6 };
        foreach (var entry in interaction.Entries.OrderBy(e => e.Order))
        {
            var caption = new TextBox { Text = entry.Caption, Width = 220 };
            var value = new TextBox
            {
                Width = 120,
                Text = values.TryGetValue(entry.Id, out var nums) && nums.Count > 0
                    ? nums[0].ToString(CultureInfo.InvariantCulture)
                    : "0",
            };
            _rows.Add((caption, value));
            panel.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { caption, value },
            });
        }

        var add = new Button { Content = TestEditorText.AddEntry, MinWidth = 120 };
        add.Click += (_, _) =>
        {
            var caption = new TextBox { Width = 220 };
            var value = new TextBox { Width = 120, Text = "0" };
            _rows.Add((caption, value));
            panel.Children.Insert(panel.Children.Count - 1, new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { caption, value },
            });
        };
        panel.Children.Add(add);
        Content = panel;
    }

    public NumericInputGroupInteractionDto CollectInteraction()
    {
        var entries = new List<NumericEntryDto>();
        for (var i = 0; i < _rows.Count; i++)
        {
            var caption = _rows[i].Caption.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(caption))
            {
                continue;
            }

            entries.Add(new NumericEntryDto($"num_{entries.Count + 1}", caption, entries.Count + 1));
        }

        return new NumericInputGroupInteractionDto(entries);
    }

    public NumericInputGroupAnswerKeyDto CollectAnswerKey()
    {
        var interaction = CollectInteraction();
        var filled = _rows.Where(r => !string.IsNullOrWhiteSpace(r.Caption.Text)).ToList();
        var values = new List<AcceptedNumberDto>();
        for (var i = 0; i < interaction.Entries.Count && i < filled.Count; i++)
        {
            if (!decimal.TryParse(filled[i].Value.Text?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                && !decimal.TryParse(filled[i].Value.Text?.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out number))
            {
                number = 0;
            }

            values.Add(new AcceptedNumberDto(interaction.Entries[i].Id, [number]));
        }

        return new NumericInputGroupAnswerKeyDto(values);
    }
}

internal sealed class TextInputEditor : UserControl
{
    private readonly TextBox _placeholder;
    private readonly TextBox _accepted;
    private readonly CheckBox _caseSensitive;

    public TextInputEditor(TextInputInteractionDto interaction, TextInputAnswerKeyDto answerKey)
    {
        _placeholder = new TextBox { Text = interaction.Placeholder ?? string.Empty };
        _accepted = new TextBox
        {
            AcceptsReturn = true,
            MinHeight = 80,
            Text = string.Join(Environment.NewLine, answerKey.AcceptedTexts),
            Watermark = TestEditorText.AcceptedTextsHint,
        };
        _caseSensitive = new CheckBox
        {
            Content = TestEditorText.CaseSensitiveLabel,
            IsChecked = answerKey.CaseSensitive,
        };
        Content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = TestEditorText.PlaceholderLabel },
                _placeholder,
                new TextBlock { Text = TestEditorText.AcceptedTextsHint },
                _accepted,
                _caseSensitive,
            },
        };
    }

    public TextInputInteractionDto CollectInteraction()
        => new(string.IsNullOrWhiteSpace(_placeholder.Text) ? null : _placeholder.Text.Trim(), null);

    public TextInputAnswerKeyDto CollectAnswerKey()
    {
        var texts = (_accepted.Text ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        return new TextInputAnswerKeyDto(texts, _caseSensitive.IsChecked == true, TrimWhitespace: true);
    }
}

internal sealed class ImagePointEditor : UserControl
{
    private readonly TextBox _xBox;
    private readonly TextBox _yBox;

    public ImagePointEditor(ImagePointAnswerKeyDto answerKey)
    {
        var point = answerKey.Regions.FirstOrDefault()?.Points.FirstOrDefault() ?? new PointDto(0, 0);
        _xBox = new TextBox { Width = 100, Text = point.X.ToString(CultureInfo.InvariantCulture) };
        _yBox = new TextBox { Width = 100, Text = point.Y.ToString(CultureInfo.InvariantCulture) };
        Content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = TestEditorText.ImagePointHint, TextWrapping = TextWrapping.Wrap },
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

    public ImagePointAnswerKeyDto CollectAnswerKey()
    {
        if (!int.TryParse(_xBox.Text?.Trim(), out var x))
        {
            x = 0;
        }

        if (!int.TryParse(_yBox.Text?.Trim(), out var y))
        {
            y = 0;
        }

        return new ImagePointAnswerKeyDto([new PolygonRegionDto("point", [new PointDto(x, y)])]);
    }
}

internal sealed class LetterOrderingEditor : UserControl
{
    private readonly TextBox _source;
    private readonly TextBox _target;
    private readonly CheckBox _caseSensitive;

    public LetterOrderingEditor(LetterOrderingContentDto? content, LetterOrderingAnswerKeyDto answerKey)
    {
        _source = new TextBox { Text = content?.SourceWord ?? string.Empty };
        _target = new TextBox { Text = answerKey.TargetWord };
        _caseSensitive = new CheckBox
        {
            Content = TestEditorText.CaseSensitiveLabel,
            IsChecked = answerKey.CaseSensitive,
        };
        Content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = TestEditorText.SourceWordLabel },
                _source,
                new TextBlock { Text = TestEditorText.TargetWordLabel },
                _target,
                _caseSensitive,
            },
        };
    }

    public LetterOrderingContentDto CollectContent()
        => new(_source.Text?.Trim() ?? string.Empty);

    public LetterOrderingAnswerKeyDto CollectAnswerKey()
        => new(_target.Text?.Trim() ?? string.Empty, _caseSensitive.IsChecked == true);
}

#pragma warning restore SA1402
