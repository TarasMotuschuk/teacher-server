using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.Testing.UI;

internal sealed class ImagePointPicker : Control, IDisposable
{
    private Bitmap? _bitmap;
    private PointDto? _dragStart;
    private IReadOnlyList<PolygonRegionDto>? _previousRegions;

    public ImagePointPicker()
    {
        Height = 320;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    public PointDto? SelectedPoint { get; private set; }

    public IReadOnlyList<PolygonRegionDto> Regions { get; set; } = [];

    public bool SelectRegions { get; set; }

    public event Action<PointDto>? PointSelected;

    public event Action<PolygonRegionDto>? RegionSelected;

    public PixelSize ImageSize => _bitmap?.PixelSize ?? default;

    public void SetImage(Bitmap bitmap, PointDto? point)
    {
        _bitmap?.Dispose();
        _bitmap = bitmap;
        SelectedPoint = point;
        InvalidateVisual();
    }

    public void SelectPoint(PointDto point)
    {
        SelectedPoint = point;
        InvalidateVisual();
        PointSelected?.Invoke(point);
    }

    public PointDto? PointAt(Point position)
    {
        if (_bitmap is null)
        {
            return null;
        }

        var rect = ImageRect();
        if (rect.Width <= 0 || rect.Height <= 0 || !rect.Contains(position))
        {
            return null;
        }

        return new PointDto(
            Math.Clamp((int)((position.X - rect.X) * ImageSize.Width / rect.Width), 0, ImageSize.Width - 1),
            Math.Clamp((int)((position.Y - rect.Y) * ImageSize.Height / rect.Height), 0, ImageSize.Height - 1));
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.LightGray, new Rect(Bounds.Size));
        if (_bitmap is null)
        {
            return;
        }

        var rect = ImageRect();
        context.DrawImage(_bitmap, rect);
        Point Map(PointDto point) => new(rect.X + (point.X * rect.Width / ImageSize.Width), rect.Y + (point.Y * rect.Height / ImageSize.Height));
        foreach (var region in Regions.Where(region => region.Points.Count >= 3))
        {
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                path.BeginFigure(Map(region.Points[0]), true);
                foreach (var point in region.Points.Skip(1))
                {
                    path.LineTo(Map(point));
                }

                path.EndFigure(true);
            }

            context.DrawGeometry(new SolidColorBrush(Color.FromArgb(55, 0, 120, 215)), new Pen(Brushes.DodgerBlue, 2), geometry);
        }

        if (!SelectRegions && SelectedPoint is { } selected)
        {
            var center = Map(selected);
            context.DrawEllipse(Brushes.Red, new Pen(Brushes.White, 2), center, 5, 5);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && PointAt(e.GetPosition(this)) is { } point)
        {
            if (SelectRegions)
            {
                _dragStart = point;
                _previousRegions = Regions;
                e.Pointer.Capture(this);
            }
            else
            {
                SelectPoint(point);
            }

            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragStart is { } start && ClampedPointAt(e.GetPosition(this)) is { } end)
        {
            if (CreateRectangle(start, end) is { } region)
            {
                Regions = [region];
            }
            else
            {
                Regions = _previousRegions ?? [];
            }

            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragStart is not { } start || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        var end = ClampedPointAt(e.GetPosition(this));
        var region = end is null ? null : CreateRectangle(start, end);
        Regions = region is null ? _previousRegions ?? [] : [region];
        _dragStart = null;
        _previousRegions = null;
        e.Pointer.Capture(null);
        InvalidateVisual();
        if (region is not null)
        {
            RegionSelected?.Invoke(region);
        }

        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragStart is not null)
        {
            Regions = _previousRegions ?? [];
            _dragStart = null;
            _previousRegions = null;
            InvalidateVisual();
        }
    }

    private PointDto? ClampedPointAt(Point position)
    {
        if (_bitmap is null)
        {
            return null;
        }

        var rect = ImageRect();
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return null;
        }

        return new PointDto(
            Math.Clamp((int)((position.X - rect.X) * ImageSize.Width / rect.Width), 0, ImageSize.Width - 1),
            Math.Clamp((int)((position.Y - rect.Y) * ImageSize.Height / rect.Height), 0, ImageSize.Height - 1));
    }

    private static PolygonRegionDto? CreateRectangle(PointDto start, PointDto end)
    {
        var left = Math.Min(start.X, end.X);
        var right = Math.Max(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var bottom = Math.Max(start.Y, end.Y);
        return left == right || top == bottom ? null
            : new PolygonRegionDto("polygon", [new(left, top), new(right, top), new(right, bottom), new(left, bottom)]);
    }

    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
    }

    private Rect ImageRect()
    {
        var scale = Math.Min(Bounds.Width / ImageSize.Width, Bounds.Height / ImageSize.Height);
        var width = ImageSize.Width * scale;
        var height = ImageSize.Height * scale;
        return new Rect((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);
    }
}
