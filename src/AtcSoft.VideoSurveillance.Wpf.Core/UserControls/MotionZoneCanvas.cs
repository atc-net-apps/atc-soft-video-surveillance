namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// A camera's motion zones drawn over its picture, to edit with the pointer. A zone is picked and
/// dragged by its inside, a corner of the picked zone by its handle; a double click on an edge adds
/// a corner there, on a corner removes it, and Delete removes the picked corner. The canvas changes
/// nothing itself: it tells each change through <see cref="ZonesEdited"/> and shows what it is given
/// in <see cref="Zones"/>.
/// </summary>
public sealed partial class MotionZoneCanvas : FrameworkElement
{
    private const double HandleSize = 8;

    private static readonly Brush NoPictureBrush = Frozen(Color.FromRgb(0x3C, 0x3C, 0x3C));
    private static readonly Brush IgnoreFill = Frozen(Color.FromArgb(0x59, 0xE5, 0x39, 0x35));
    private static readonly Pen IgnorePen = FrozenPen(Color.FromRgb(0xE5, 0x39, 0x35), 1.5);
    private static readonly Brush WatchFill = Frozen(Color.FromArgb(0x59, 0x1E, 0x88, 0xE5));
    private static readonly Pen WatchPen = FrozenPen(Color.FromRgb(0x1E, 0x88, 0xE5), 1.5);
    private static readonly Pen PickedPen = FrozenPen(Colors.White, 2.5);
    private static readonly Pen HandlePen = FrozenPen(Colors.Black, 1);
    private static readonly Brush HandleBrush = Frozen(Colors.White);
    private static readonly Brush PickedHandleBrush = Frozen(Color.FromRgb(0xFF, 0x8C, 0x00));

    // The generator's backing fields: read through the generated properties only.
#pragma warning disable CS0169, CS0414
    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private IReadOnlyList<MotionZone>? zones;

    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private ImageSource? picture;

    [DependencyProperty(DefaultValue = -1, PropertyChangedCallback = nameof(OnLookChanged))]
    private int selectedZone = -1;

    [DependencyProperty(DefaultValue = -1, PropertyChangedCallback = nameof(OnLookChanged))]
    private int selectedCorner = -1;
#pragma warning restore CS0169, CS0414

    // A drag: the corners as it began, where it began, the corner dragged (-1: the whole zone),
    // and the corners now, given out when it ends.
    private IReadOnlyList<ZonePoint>? dragOrigin;
    private ZonePoint dragStart = new(0, 0);
    private int dragCorner = -1;
    private IReadOnlyList<ZonePoint>? dragged;

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionZoneCanvas"/> class.
    /// </summary>
    public MotionZoneCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
        FocusVisualStyle = null;
    }

    /// <summary>
    /// Occurs when the pointer or a key changed the zones.
    /// </summary>
    public event EventHandler<MotionZonesEditedEventArgs>? ZonesEdited;

    /// <summary>
    /// Gets the size of the picture as it is shown, in which a corner is grabbed.
    /// </summary>
    public Size PictureSize
    {
        get
        {
            var area = PictureArea();
            return new Size(area.Width, area.Height);
        }
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        // Transparent, not empty: the whole area takes the pointer.
        drawingContext.DrawRectangle(Brushes.Transparent, pen: null, new Rect(0, 0, ActualWidth, ActualHeight));

        var area = PictureArea();
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        if (Picture is { } image)
        {
            drawingContext.DrawImage(image, area);
        }
        else
        {
            drawingContext.DrawRectangle(NoPictureBrush, pen: null, area);
        }

        var shown = Zones ?? [];
        for (var i = 0; i < shown.Count; i++)
        {
            var corners = Corners(i);
            if (corners.Count < 2)
            {
                continue;
            }

            var watch = shown[i].Kind == MotionZoneKind.Watch;
            var picked = i == SelectedZone;
            var (fill, pen) = watch ? WatchLook(shown[i].Color) : (IgnoreFill, IgnorePen);
            drawingContext.DrawGeometry(
                fill,
                picked ? PickedPen : pen,
                Outline(corners, area));
            DrawName(drawingContext, shown[i].Name, corners, area);
        }

        if (SelectedZone >= 0 && SelectedZone < shown.Count)
        {
            var corners = Corners(SelectedZone);
            for (var c = 0; c < corners.Count; c++)
            {
                var at = ToPoint(corners[c], area);
                drawingContext.DrawRectangle(
                    c == SelectedCorner ? PickedHandleBrush : HandleBrush,
                    HandlePen,
                    new Rect(at.X - (HandleSize / 2), at.Y - (HandleSize / 2), HandleSize, HandleSize));
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>A plain element has no peer: without one, screen readers and UI tests cannot see the canvas.</remarks>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        => new System.Windows.Automation.Peers.FrameworkElementAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseLeftButtonDown(e);

        var area = PictureArea();
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        Focus();
        e.Handled = true;
        var at = ToFraction(e.GetPosition(this), area);
        var shown = Zones ?? [];

        if (SelectedZone >= 0 && SelectedZone < shown.Count)
        {
            var corners = shown[SelectedZone].Points;
            if (MotionZoneEditing.FindCorner(corners, at.X, at.Y, area.Width, area.Height) is { } corner)
            {
                if (e.ClickCount == 2)
                {
                    SetCurrentValue(SelectedCornerProperty, -1);
                    Edit(SelectedZone, MotionZoneEditing.RemoveCorner(corners, corner));
                    return;
                }

                SetCurrentValue(SelectedCornerProperty, corner);
                StartDrag(corners, at, corner);
                return;
            }

            if (e.ClickCount == 2 &&
                MotionZoneEditing.CanAddCorner(corners.Count) &&
                MotionZoneEditing.FindEdge(corners, at.X, at.Y, area.Width, area.Height) is { } edge)
            {
                var added = MotionZoneEditing.MoveCorner(MotionZoneEditing.AddCorner(corners, edge), edge + 1, at.X, at.Y);
                SetCurrentValue(SelectedCornerProperty, edge + 1);
                Edit(SelectedZone, added);
                return;
            }

            // On the edge, just outside the zone, it stays picked: the first click of a double
            // click there must not let it go.
            if (MotionZoneEditing.FindEdge(corners, at.X, at.Y, area.Width, area.Height) is not null)
            {
                SetCurrentValue(SelectedCornerProperty, -1);
                StartDrag(corners, at, corner: -1);
                return;
            }
        }

        var hit = MotionZoneEditing.FindZone([.. shown.Select(z => (IReadOnlyList<ZonePoint>)z.Points)], at.X, at.Y);
        SetCurrentValue(SelectedCornerProperty, -1);
        SetCurrentValue(SelectedZoneProperty, hit ?? -1);
        if (hit is { } zone)
        {
            StartDrag(shown[zone].Points, at, corner: -1);
        }
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);

        var area = PictureArea();
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        var at = ToFraction(e.GetPosition(this), area);
        if (dragOrigin is null || !IsMouseCaptured)
        {
            Cursor = PointerOver(at, area);
            return;
        }

        dragged = dragCorner >= 0
            ? MotionZoneEditing.MoveCorner(dragOrigin, dragCorner, at.X, at.Y)
            : MotionZoneEditing.MoveZone(dragOrigin, at.X - dragStart.X, at.Y - dragStart.Y);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseLeftButtonUp(e);

        var corners = dragged;
        EndDrag();
        if (corners is not null && SelectedZone >= 0)
        {
            Edit(SelectedZone, corners);
        }
    }

    /// <inheritdoc />
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        // Taken away during a drag (another window, Escape): the zone stays where it was.
        if (dragOrigin is not null)
        {
            dragOrigin = null;
            dragged = null;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);

        var shown = Zones ?? [];
        if (e.Key == Key.Delete &&
            SelectedZone >= 0 && SelectedZone < shown.Count &&
            SelectedCorner >= 0 && SelectedCorner < shown[SelectedZone].Points.Count)
        {
            var corners = shown[SelectedZone].Points;
            var corner = SelectedCorner;
            SetCurrentValue(SelectedCornerProperty, -1);
            Edit(SelectedZone, MotionZoneEditing.RemoveCorner(corners, corner));
            e.Handled = true;
        }
    }

    // A watch zone in its own colour, as on the timeline; blue without one.
    private static (Brush Fill, Pen Pen) WatchLook(string? color)
    {
        if (MotionZoneColors.Brush(color) is not { } brush)
        {
            return (WatchFill, WatchPen);
        }

        var value = brush.Color;
        return (Frozen(Color.FromArgb(0x59, value.R, value.G, value.B)), FrozenPen(value, 1.5));
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(
        Color color,
        double thickness)
    {
        var pen = new Pen(Frozen(color), thickness) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        return pen;
    }

    private static Point ToPoint(
        ZonePoint corner,
        Rect area)
        => new(area.X + (corner.X * area.Width), area.Y + (corner.Y * area.Height));

    private static ZonePoint ToFraction(
        Point point,
        Rect area)
        => new((point.X - area.X) / area.Width, (point.Y - area.Y) / area.Height);

    private static StreamGeometry Outline(
        IReadOnlyList<ZonePoint> corners,
        Rect area)
    {
        var geometry = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var context = geometry.Open())
        {
            context.BeginFigure(ToPoint(corners[0], area), isFilled: true, isClosed: true);
            context.PolyLineTo([.. corners.Skip(1).Select(c => ToPoint(c, area))], isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static void OnLookChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionZoneCanvas canvas)
        {
            canvas.InvalidateVisual();
        }
    }

    private void DrawName(
        DrawingContext drawingContext,
        string name,
        IReadOnlyList<ZonePoint> corners,
        Rect area)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var text = new FormattedText(
            name,
            CultureInfo.CurrentCulture,
            FlowDirection,
            new Typeface("Segoe UI"),
            12,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var left = corners.Min(c => c.X);
        var top = corners.Min(c => c.Y);
        var at = ToPoint(new ZonePoint(left, top), area);
        drawingContext.DrawRectangle(
            NoPictureBrush,
            pen: null,
            new Rect(at.X + 2, at.Y + 2, text.Width + 6, text.Height + 2));
        drawingContext.DrawText(text, new Point(at.X + 5, at.Y + 3));
    }

    private Rect PictureArea()
    {
        var aspectRatio = Picture is { Width: > 0, Height: > 0 } image
            ? image.Width / image.Height
            : MotionZoneEditing.DefaultAspectRatio;
        var (x, y, width, height) = MotionZoneEditing.Fit(ActualWidth, ActualHeight, aspectRatio);
        return new Rect(x, y, width, height);
    }

    private IReadOnlyList<ZonePoint> Corners(int zone)
        => zone == SelectedZone && dragged is not null
            ? dragged
            : Zones![zone].Points;

    private Cursor PointerOver(
        ZonePoint at,
        Rect area)
    {
        var shown = Zones ?? [];
        if (SelectedZone >= 0 && SelectedZone < shown.Count &&
            MotionZoneEditing.FindCorner(shown[SelectedZone].Points, at.X, at.Y, area.Width, area.Height) is not null)
        {
            return Cursors.Cross;
        }

        return MotionZoneEditing.FindZone([.. shown.Select(z => (IReadOnlyList<ZonePoint>)z.Points)], at.X, at.Y) is not null
            ? Cursors.SizeAll
            : Cursors.Arrow;
    }

    private void StartDrag(
        IReadOnlyList<ZonePoint> corners,
        ZonePoint at,
        int corner)
    {
        dragOrigin = [.. corners];
        dragStart = at;
        dragCorner = corner;
        dragged = null;
        CaptureMouse();
    }

    private void EndDrag()
    {
        dragOrigin = null;
        dragged = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void Edit(
        int zone,
        IReadOnlyList<ZonePoint> corners)
    {
        var shown = Zones ?? [];
        var edited = shown.Select(z => z.Clone()).ToList();
        edited[zone].Points = [.. corners];
        ZonesEdited?.Invoke(this, new MotionZonesEditedEventArgs(edited));
    }
}