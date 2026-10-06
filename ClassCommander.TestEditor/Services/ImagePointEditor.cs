using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ClassCommander.TestEditor.Localization;
using ClassCommander.Testing.UI;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestEditor.Services;

internal sealed class ImagePointEditor : UserControl, IDisposable
{
    private readonly ImagePointPicker _picker = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly NumericUpDown _radius = new() { Minimum = 1, Maximum = 1000, Value = 10, Width = 120 };
    private ImagePointAnswerKeyDto _key;
    private readonly ComboBox _mode = new() { ItemsSource = new[] { TestEditorText.SelectImageArea, TestEditorText.SelectImagePoint }, SelectedIndex = 0 };
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap };

    public ImagePointEditor(ImagePointAnswerKeyDto key, QuestionAssetDto? asset, string? path, Func<Task<(QuestionAssetDto Asset, string Path)?>>? chooseImage)
    {
        _key = key;
        _picker.SelectRegions = true;
        Asset = asset;
        var choose = new Button { Content = TestEditorText.ChooseImage };
        choose.Click += async (_, _) =>
        {
            if (chooseImage is null)
            {
                return;
            }

            var result = await chooseImage();
            if (result is { } image)
            {
                Asset = image.Asset;
                _key = new ImagePointAnswerKeyDto([]);
                LoadImage(image.Path, null);
            }
        };
        _picker.PointSelected += point =>
        {
            if (_mode.SelectedIndex == 1)
            {
                UpdateRegions(point);
            }
        };
        _radius.ValueChanged += (_, _) =>
        {
            if (_mode.SelectedIndex == 1 && _picker.SelectedPoint is { } point)
            {
                UpdateRegions(point);
            }
        };
        _picker.RegionSelected += region =>
        {
            _key = new ImagePointAnswerKeyDto([region]);
            _picker.SelectPoint(new PointDto((region.Points[0].X + region.Points[2].X) / 2, (region.Points[0].Y + region.Points[2].Y) / 2));
            _status.Text = TestEditorText.ImageAreaSelected;
        };
        var toleranceRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            IsVisible = false,
            Children = { new TextBlock { Text = TestEditorText.PointTolerance, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, _radius },
        };
        _hint.Text = TestEditorText.ImageAreaHint;
        _mode.SelectionChanged += (_, _) =>
        {
            _picker.SelectRegions = _mode.SelectedIndex == 0;
            toleranceRow.IsVisible = !_picker.SelectRegions;
            _hint.Text = _picker.SelectRegions ? TestEditorText.ImageAreaHint : TestEditorText.ImagePointHint;
            _picker.InvalidateVisual();
        };
        Content = new StackPanel
        {
            Spacing = 8,
            Children = { choose, _mode, _hint, _picker, toleranceRow, _status },
        };
        if (path is not null && File.Exists(path))
        {
            var region = key.Regions.FirstOrDefault();
            PointDto? point = region is { Points.Count: > 0 }
                ? new PointDto((int)region.Points.Average(p => p.X), (int)region.Points.Average(p => p.Y))
                : null;
            LoadImage(path, point);
            _picker.Regions = key.Regions;
            if (key.Regions.Count > 0)
            {
                _status.Text = TestEditorText.ImageAreaSelected;
            }
        }
        else
        {
            _picker.IsVisible = false;
            _status.Text = TestEditorText.ImageNotSelected;
        }
    }

    public QuestionAssetDto? Asset { get; private set; }

    public ImagePointAnswerKeyDto CollectAnswerKey() => _key;

    public void Dispose() => _picker.Dispose();

    private void LoadImage(string path, PointDto? point)
    {
        _picker.SetImage(new Bitmap(path), point);
        _picker.IsVisible = true;
        _picker.Regions = [];
        _status.Text = point is null
            ? _picker.SelectRegions ? TestEditorText.SelectCorrectArea : TestEditorText.SelectCorrectPoint
            : TestEditorText.PointSelected(point.X, point.Y);
    }

    private void UpdateRegions(PointDto point)
    {
        var radius = (int)(_radius.Value ?? 10);
        var size = _picker.ImageSize;
        var left = Math.Max(0, point.X - radius);
        var right = Math.Min(size.Width - 1, point.X + radius);
        var top = Math.Max(0, point.Y - radius);
        var bottom = Math.Min(size.Height - 1, point.Y + radius);
        _key = new ImagePointAnswerKeyDto([new PolygonRegionDto("polygon", [new(left, top), new(right, top), new(right, bottom), new(left, bottom)])]);
        _picker.Regions = _key.Regions;
        _picker.InvalidateVisual();
        _status.Text = TestEditorText.PointSelected(point.X, point.Y);
    }
}
