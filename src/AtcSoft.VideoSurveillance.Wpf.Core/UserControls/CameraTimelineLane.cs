namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// One camera's lane on a timeline of every camera: a gap where nothing was recorded, coloured by
/// its reason when <see cref="GapBands"/> are given (the pointer on it tells the reason), dim where a
/// recording was not analysed, a bar for how much changed in each stretch, an orange band where
/// something went somewhere, and a red line at now. A click asks to play from there through
/// <see cref="SeekCommand"/> with the place as a fraction of the span, 0 to 1. The same look as
/// the player's <see cref="ActivityTimeline"/>.
/// </summary>
public sealed partial class CameraTimelineLane : FrameworkElement
{
    private const double BandHeight = 4;

    private static readonly Brush GapBrush = Frozen(Color.FromArgb(0x59, 0x80, 0x80, 0x80));
    private static readonly Brush NotAnalysedBrush = Frozen(Color.FromArgb(0x26, 0x80, 0x80, 0x80));
    private static readonly Brush BarBrush = Frozen(Color.FromArgb(0xCC, 0x80, 0x80, 0x80));
    private static readonly Brush EventBrush = Frozen(Color.FromRgb(0xFF, 0x8C, 0x00));
    private static readonly Brush NowBrush = Frozen(Color.FromRgb(0xE5, 0x39, 0x35));

    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private IReadOnlyList<TimelineStretch>? stretches;

    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private IReadOnlyList<ActivityEvent>? eventBands;

    // Null: the gaps come from the stretches, without a reason.
    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private IReadOnlyList<TimelineGapBand>? gapBands;

    [DependencyProperty(DefaultValue = -1d, PropertyChangedCallback = nameof(OnLookChanged))]
    private double nowFraction;

    [DependencyProperty]
    private ICommand? seekCommand;

    private object? laneToolTip;

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraTimelineLane"/> class.
    /// </summary>
    public CameraTimelineLane()
    {
        Cursor = Cursors.Hand;
        ClipToBounds = true;
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;

        // Transparent, not empty: the whole lane takes clicks.
        drawingContext.DrawRectangle(Brushes.Transparent, pen: null, new Rect(0, 0, width, height));
        if (Stretches is not { Count: > 0 } all || width <= 0 || height <= BandHeight)
        {
            return;
        }

        var barArea = height - BandHeight - 1;
        foreach (var gap in GapBands ?? [])
        {
            var x = gap.Start * width;
            drawingContext.DrawRectangle(TimelineGapLook.BrushOf(gap.Reason), pen: null, new Rect(x, 0, Math.Max((gap.End - gap.Start) * width, 1), height));
        }

        var step = width / all.Count;
        for (var i = 0; i < all.Count; i++)
        {
            var stretch = all[i];
            var x = i * step;
            var w = Math.Max(step, 1);
            if (!stretch.IsRecorded)
            {
                if (GapBands is null)
                {
                    drawingContext.DrawRectangle(GapBrush, pen: null, new Rect(x, 0, w, height));
                }
            }
            else if (!stretch.IsAnalysed)
            {
                drawingContext.DrawRectangle(NotAnalysedBrush, pen: null, new Rect(x, 0, w, barArea));
            }
            else if (ActivityTimelineLayout.BarHeight(stretch.Level) * barArea is var bar and > 0)
            {
                drawingContext.DrawRectangle(BarBrush, pen: null, new Rect(x, barArea - bar, w, bar));
            }
        }

        foreach (var band in EventBands ?? [])
        {
            var (x, bandWidth) = ActivityTimelineLayout.ToPixels(band, width);
            drawingContext.DrawRectangle(EventBrush, pen: null, new Rect(x, height - BandHeight, bandWidth, BandHeight));
        }

        if (NowFraction is >= 0 and <= 1)
        {
            drawingContext.DrawRectangle(NowBrush, pen: null, new Rect(Math.Clamp((NowFraction * width) - 1, 0, Math.Max(width - 2, 0)), 0, 2, height));
        }
    }

    /// <inheritdoc />
    /// <remarks>A plain element has no peer: without one, screen readers and UI tests cannot see the lane.</remarks>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        => new System.Windows.Automation.Peers.FrameworkElementAutomationPeer(this);

    /// <inheritdoc />
    /// <remarks>On a gap the tooltip tells why nothing was recorded; elsewhere it is the lane's own.</remarks>
    protected override void OnToolTipOpening(ToolTipEventArgs e)
    {
        base.OnToolTipOpening(e);

        // The lane's own tooltip, kept the first time so a gap's text can stand in for it.
        laneToolTip ??= ToolTip;
        var fraction = ActualWidth > 0 ? Mouse.GetPosition(this).X / ActualWidth : -1;
        var gap = GapBands?.FirstOrDefault(g => fraction >= g.Start && fraction <= g.End);
        ToolTip = gap is null ? laneToolTip : TimelineGapLook.TextOf(gap.Reason);
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseLeftButtonDown(e);

        if (ActualWidth <= 0)
        {
            return;
        }

        var fraction = Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1);
        if (SeekCommand?.CanExecute(fraction) == true)
        {
            SeekCommand.Execute(fraction);
            e.Handled = true;
        }
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static void OnLookChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraTimelineLane lane)
        {
            lane.InvalidateVisual();
        }
    }
}