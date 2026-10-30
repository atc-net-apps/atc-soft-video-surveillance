namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// The time marks under the lanes of a timeline of every camera, each label centred on its place
/// and kept inside the bar. The text takes the inherited foreground and font size.
/// </summary>
public sealed partial class TimelineMarksBar : FrameworkElement
{
    [DependencyProperty(PropertyChangedCallback = nameof(OnLookChanged))]
    private IReadOnlyList<TimelineMark>? marks;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => new(0, Math.Ceiling(System.Windows.Documents.TextElement.GetFontSize(this) * 1.5));

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        var width = ActualWidth;
        if (Marks is not { Count: > 0 } all || width <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface(System.Windows.Documents.TextElement.GetFontFamily(this), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        foreach (var mark in all)
        {
            var text = new FormattedText(
                mark.Label,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                System.Windows.Documents.TextElement.GetFontSize(this),
                System.Windows.Documents.TextElement.GetForeground(this) ?? Brushes.Gray,
                dpi);
            var x = Math.Clamp((mark.Fraction * width) - (text.Width / 2), 0, Math.Max(width - text.Width, 0));
            drawingContext.DrawText(text, new Point(x, 0));
        }
    }

    private static void OnLookChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is TimelineMarksBar bar)
        {
            bar.InvalidateVisual();
        }
    }
}