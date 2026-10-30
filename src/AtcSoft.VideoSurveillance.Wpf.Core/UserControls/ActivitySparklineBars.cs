namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// A recording's activity as a small bar chart for a row of a list: one bar per stretch, its
/// height the most that changed in it (rising by the square root, as on the timelines), orange
/// where something clearly moved. Draws nothing for a recording without an index.
/// </summary>
public sealed partial class ActivitySparklineBars : FrameworkElement
{
    private static readonly Brush QuietBrush = Frozen(Color.FromArgb(0xB0, 0x80, 0x80, 0x80));
    private static readonly Brush BusyBrush = Frozen(Color.FromRgb(0xFF, 0x8C, 0x00));

    [DependencyProperty(PropertyChangedCallback = nameof(OnLevelsChanged))]
    private IReadOnlyList<int>? levels;

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        if (Levels is not { Count: > 0 } all || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var step = ActualWidth / all.Count;
        for (var i = 0; i < all.Count; i++)
        {
            var height = ActivityTimelineLayout.BarHeight(all[i]) * ActualHeight;
            if (height <= 0)
            {
                continue;
            }

            var brush = all[i] >= ActivitySparkline.BusyLevel ? BusyBrush : QuietBrush;
            drawingContext.DrawRectangle(brush, pen: null, new Rect(i * step, ActualHeight - height, Math.Max(step - 1, 1), height));
        }
    }

    /// <inheritdoc />
    /// <remarks>A plain element has no peer: without one, screen readers and UI tests cannot see it.</remarks>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        => new System.Windows.Automation.Peers.FrameworkElementAutomationPeer(this);

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static void OnLevelsChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is ActivitySparklineBars bars)
        {
            bars.InvalidateVisual();
        }
    }
}