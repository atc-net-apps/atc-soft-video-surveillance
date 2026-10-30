namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// The activity of a recording drawn under its player: a bar for how much of the picture changed
/// in each stretch, an orange band where something went somewhere (a confirmed track; in the colour
/// of the watch zone it happened in, when that zone has one, see <see cref="ZoneBrushes"/>), dim where
/// nothing was analysed, and a line where the player is. A click asks to jump there through
/// <see cref="SeekCommand"/> with the place as a fraction of the recording, 0 to 1.
/// It shows the part of the recording from <see cref="VisibleStart"/> to <see cref="VisibleEnd"/>
/// (the whole of it by default); the mouse wheel asks to zoom through <see cref="ZoomCommand"/>.
/// Resting the mouse on it shows a picture of that moment from <see cref="Previews"/> (on an event, the
/// moment most moved in it; <see cref="PreviewAt"/>) above the time there, how many things moved and
/// in which watch zone (<see cref="DescribeAt"/>), the time from <see cref="RecordingStart"/> and
/// <see cref="DurationSeconds"/>.
/// A right-click offers verdicts on the stretch there through <see cref="VerdictCommand"/> (on an
/// event: boxed correctly or a false alarm; anywhere: something missed; see <see cref="VerdictsAt"/>),
/// saved in the recording's review.
/// </summary>
public sealed partial class ActivityTimeline : FrameworkElement
{
    private const double BandHeight = 4;

    // The looks when none is set: the generator takes no default from a field's initialiser.
    private static readonly Brush DefaultBarBrush = Frozen(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
    private static readonly Brush DefaultEventBrush = Frozen(Color.FromRgb(0xFF, 0x8C, 0x00));
    private static readonly Brush DefaultNotAnalysedBrush = Frozen(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));

    // The tooltip follows the mouse along the strip, its picture and words changing with the place.
    private readonly ToolTip hoverTip = new() { Placement = System.Windows.Controls.Primitives.PlacementMode.Top };
    private readonly Image hoverPicture = new() { Stretch = Stretch.None, Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed };
    private readonly TextBlock hoverWords = new();

    private IReadOnlyList<ActivityEvent>? events;

    [DependencyProperty(PropertyChangedCallback = nameof(OnBucketsChanged))]
    private IReadOnlyList<ActivityBucket>? buckets;

    // When the recording started, in local time, for the time under the mouse; the time into it when null.
    [DependencyProperty]
    private DateTime? recordingStart;

    // How long the recording is, in seconds, for the time under the mouse and to join events close together.
    [DependencyProperty(PropertyChangedCallback = nameof(OnBucketsChanged))]
    private double durationSeconds;

    // The recording's preview pictures, shown under the mouse; none while null.
    [DependencyProperty]
    private RecordingPreviewTiles? previews;

    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private double position;

    [DependencyProperty]
    private ICommand? seekCommand;

    // Where the part shown starts and ends, as fractions of the recording (ActivityTimelineZoom).
    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private double visibleStart;

    [DependencyProperty(DefaultValue = 1d, PropertyChangedCallback = nameof(OnLookChanged))]
    private double visibleEnd;

    // Takes an ActivityTimelineZoomRequest: where the mouse is in the recording, and which way.
    [DependencyProperty]
    private ICommand? zoomCommand;

    // Takes a TimelineVerdict, chosen from the menu a right-click opens; no menu while null.
    [DependencyProperty]
    private ICommand? verdictCommand;

    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private Brush? barBrush;

    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private Brush? eventBrush;

    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private Brush? notAnalysedBrush;

    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private Brush? positionBrush;

    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private IReadOnlyDictionary<string, Brush>? zoneBrushes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityTimeline"/> class.
    /// </summary>
    public ActivityTimeline()
    {
        Cursor = Cursors.Hand;
        ClipToBounds = true;
        hoverTip.PlacementTarget = this;
        hoverWords.Text = Translations.ActivityTimelineHint;
        var content = new StackPanel();
        content.Children.Add(hoverPicture);
        content.Children.Add(hoverWords);
        hoverTip.Content = content;
        ToolTip = hoverTip;
    }

    /// <summary>
    /// Gets the picture the tooltip shows for a point of the strip: on an event, the moment most
    /// moved in it; elsewhere, the nearest one of the grid.
    /// </summary>
    /// <param name="x">The point, in pixels from the left.</param>
    /// <returns>The picture, or <see langword="null"/> without previews.</returns>
    public BitmapSource? PreviewAt(double x)
        => Look(x) is { Tile: { } tile } && Previews is { } sheet
            ? sheet.Tile(tile)
            : null;

    /// <summary>
    /// Gets the words the tooltip shows for a point of the strip: the time there, how many things
    /// moved and in which watch zone, then how to use the timeline. With a picture, the time is the
    /// picture's.
    /// </summary>
    /// <param name="x">The point, in pixels from the left.</param>
    public string DescribeAt(double x)
        => Look(x) is { Hover: { } hover } look
            ? string.Join(Environment.NewLine, [.. ActivityHoverText.Lines(hover with { Position = look.Position }, RecordingStart, DurationSeconds), Translations.ActivityTimelineHint])
            : Translations.ActivityTimelineHint;

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;

        // Transparent, not empty: the whole strip takes clicks.
        drawingContext.DrawRectangle(Brushes.Transparent, pen: null, new Rect(0, 0, width, height));

        if (Buckets is not { Count: > 0 } stretches || width <= 0 || height <= BandHeight)
        {
            return;
        }

        var (start, end) = Window();
        var barArea = height - BandHeight - 1;
        foreach (var bar in ActivityTimelineZoom.Bars(stretches, start, end, (int)Math.Ceiling(width)))
        {
            var x = bar.Start * width;
            var barWidth = Math.Max((bar.End - bar.Start) * width, 1);
            if (!bar.IsAnalysed)
            {
                drawingContext.DrawRectangle(NotAnalysedBrush ?? DefaultNotAnalysedBrush, pen: null, new Rect(x, 0, barWidth, barArea));
                continue;
            }

            var barHeight = ActivityTimelineLayout.BarHeight(bar.Level) * barArea;
            if (barHeight > 0)
            {
                drawingContext.DrawRectangle(BarBrush ?? DefaultBarBrush, pen: null, new Rect(x, barArea - barHeight, barWidth, barHeight));
            }
        }

        events ??= ActivityTimelineLayout.FindEvents(stretches, DurationSeconds);
        foreach (var activityEvent in ActivityTimelineZoom.Visible(events, start, end))
        {
            var (x, bandWidth) = ActivityTimelineLayout.ToPixels(activityEvent, width);
            var brush = activityEvent.Zone is { } zone && ZoneBrushes?.TryGetValue(zone, out var zoned) == true
                ? zoned
                : EventBrush ?? DefaultEventBrush;
            drawingContext.DrawRectangle(brush, pen: null, new Rect(x, height - BandHeight, bandWidth, BandHeight));
        }

        var share = ActivityTimelineZoom.ToShare(start, end, Math.Clamp(Position, 0, 1));
        if (share is < 0 or > 1)
        {
            return;
        }

        var playhead = share * width;
        drawingContext.DrawRectangle(PositionBrush ?? Brushes.White, pen: null, new Rect(Math.Clamp(playhead - 1, 0, Math.Max(width - 2, 0)), 0, 2, height));
    }

    /// <inheritdoc />
    /// <remarks>A plain element has no peer: without one, screen readers and UI tests cannot see the timeline.</remarks>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        => new System.Windows.Automation.Peers.FrameworkElementAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseLeftButtonDown(e);

        if (ActualWidth <= 0)
        {
            return;
        }

        var fraction = PlaceAt(e.GetPosition(this).X);
        if (SeekCommand?.CanExecute(fraction) == true)
        {
            SeekCommand.Execute(fraction);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);

        var x = e.GetPosition(this).X;
        hoverWords.Text = DescribeAt(x);
        hoverPicture.Source = PreviewAt(x);
        hoverPicture.Visibility = hoverPicture.Source is null ? Visibility.Collapsed : Visibility.Visible;
        hoverTip.HorizontalOffset = x;
    }

    /// <inheritdoc />
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseWheel(e);

        if (ActualWidth <= 0 || e.Delta == 0)
        {
            return;
        }

        var request = new ActivityTimelineZoomRequest(PlaceAt(e.GetPosition(this).X), ZoomIn: e.Delta > 0);
        if (ZoomCommand?.CanExecute(request) == true)
        {
            ZoomCommand.Execute(request);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Gets the verdicts a right-click at a point of the strip offers: on an event's band (as wide
    /// as it is drawn), boxed correctly and false alarm over the event; anywhere, something missed
    /// around the place.
    /// </summary>
    /// <param name="x">The point, in pixels from the left.</param>
    /// <returns>The verdicts; none without activity.</returns>
    public IReadOnlyList<TimelineVerdict> VerdictsAt(double x)
    {
        if (Buckets is not { Count: > 0 } stretches || ActualWidth <= 0 || DurationSeconds <= 0)
        {
            return [];
        }

        var (start, end) = Window();
        events ??= ActivityTimelineLayout.FindEvents(stretches, DurationSeconds);
        var slack = (end - start) * (ActivityTimelineLayout.MinimumEventWidth / 2) / ActualWidth;
        return TimelineVerdicts.At(events, PlaceAt(x), DurationSeconds, slack);
    }

    /// <inheritdoc />
    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseRightButtonUp(e);

        if (VerdictCommand is not { } command || VerdictsAt(e.GetPosition(this).X).Where(command.CanExecute).ToList() is not { Count: > 0 } verdicts)
        {
            return;
        }

        var menu = new ContextMenu
        {
            PlacementTarget = this,
            Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
        };
        foreach (var verdict in verdicts)
        {
            var item = new MenuItem { Header = VerdictText(verdict.Verdict), Command = command, CommandParameter = verdict };
            System.Windows.Automation.AutomationProperties.SetAutomationId(item, "Verdict-" + verdict.Verdict);
            menu.Items.Add(item);
        }

        hoverTip.IsOpen = false;
        menu.IsOpen = true;
        e.Handled = true;
    }

    // The menu's words for a verdict.
    private static string VerdictText(string verdict)
        => verdict switch
        {
            AtcSoft.VideoSurveillance.Helpers.Review.ReviewVerdicts.Correct => Translations.VerdictCorrect,
            AtcSoft.VideoSurveillance.Helpers.Review.ReviewVerdicts.FalseBox => Translations.VerdictFalseAlarm,
            _ => Translations.VerdictMissed,
        };

    // The part shown; the whole recording when the window is not a sensible one.
    private (double Start, double End) Window()
    {
        var start = Math.Clamp(VisibleStart, 0, 1);
        var end = Math.Clamp(VisibleEnd, 0, 1);
        return end > start ? (start, end) : (0, 1);
    }

    // What happened under a point of the strip, the stretches as wide as the narrowest band so a
    // widened band tells its event; and the preview tile of it, with the moment the tile shows.
    private (ActivityHover? Hover, int? Tile, double Position)? Look(double x)
    {
        if (Buckets is not { Count: > 0 } stretches || ActualWidth <= 0)
        {
            return null;
        }

        var (start, end) = Window();
        var half = ActivityTimelineLayout.MinimumEventWidth / 2;
        var from = ActivityTimelineZoom.ToRecording(start, end, (x - half) / ActualWidth);
        var to = ActivityTimelineZoom.ToRecording(start, end, (x + half) / ActualWidth);
        events ??= ActivityTimelineLayout.FindEvents(stretches, DurationSeconds);
        var hover = ActivityTimelineHover.At(stretches, events, from, to);
        var place = hover?.Position ?? (from + to) / 2;

        if (Previews?.Layout is not { } layout || DurationSeconds <= 0)
        {
            return (hover, null, place);
        }

        var seconds = DurationSeconds;
        if (events.FirstOrDefault(e => e.End > from && e.Start < to) is { } activityEvent &&
            RecordingPreviewLayout.EventTile(layout, activityEvent.Start * seconds, activityEvent.End * seconds) is { } eventTile)
        {
            return (hover, eventTile, layout.Events.First(e => e.Tile == eventTile).Seconds / seconds);
        }

        var gridTile = RecordingPreviewLayout.GridTile(layout, place * seconds);
        return (hover, gridTile, Math.Min(gridTile * layout.IntervalSeconds / seconds, 1));
    }

    // The place in the recording under a point of the strip, 0 to 1.
    private double PlaceAt(double x)
    {
        var (start, end) = Window();
        return ActivityTimelineZoom.ToRecording(start, end, x / ActualWidth);
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static void OnBucketsChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is ActivityTimeline timeline)
        {
            timeline.events = null;
            timeline.InvalidateVisual();
        }
    }

    private static void OnLookChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is ActivityTimeline timeline)
        {
            timeline.InvalidateVisual();
        }
    }
}