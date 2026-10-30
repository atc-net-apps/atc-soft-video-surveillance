namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

using Rectangle = System.Windows.Shapes.Rectangle;

/// <summary>
/// Overlay control for displaying multiple motion detection bounding boxes.
/// </summary>
/// <remarks>
/// CS0169 / CS0414 are suppressed via &lt;NoWarn&gt; in the .csproj —
/// [DependencyProperty] backing fields are read by the source generator.
/// </remarks>
public partial class MotionBoundingBoxOverlay
{
    private const int DefaultAnalysisWidth = 320;
    private const int DefaultAnalysisHeight = 240;
    private const int MaxBoundingBoxes = 10; // Limit to prevent excessive UI elements

    // Pool of rectangle elements for reuse
    private readonly List<Rectangle> rectanglePool = [];

    // Smooths each box against the box it was a moment ago: by its track id when it has one, else
    // by overlap or nearness, never by its place in the list, which the detector sorts by size.
    private BoxSmoother? smoother;
    private double smootherFactor = double.NaN;

    // Last bounding box state for re-rendering on resize
    private IReadOnlyList<BoundingBox>? lastBoundingBoxes;
    private int lastAnalysisWidth = DefaultAnalysisWidth;
    private int lastAnalysisHeight = DefaultAnalysisHeight;
    private int lastVideoWidth;
    private int lastVideoHeight;

    [DependencyProperty(DefaultValue = true, PropertyChangedCallback = nameof(OnIsOverlayEnabledChanged))]
    private bool isOverlayEnabled;

    [DependencyProperty(DefaultValue = "Red", PropertyChangedCallback = nameof(OnBoxColorChanged))]
    private string boxColor = "Red";

    [DependencyProperty(DefaultValue = 2, PropertyChangedCallback = nameof(OnBoxThicknessChanged))]
    private int boxThickness;

    [DependencyProperty(DefaultValue = 0.3)]
    private double smoothingFactor;

    [DependencyProperty(DefaultValue = DefaultAnalysisWidth)]
    private int analysisWidth;

    [DependencyProperty(DefaultValue = DefaultAnalysisHeight)]
    private int analysisHeight;

    /// <summary>
    /// The displayed video width (after rotation), used to compute letterbox offset.
    /// When set to 0, falls back to stretching across the full container (legacy behavior).
    /// </summary>
    /// <summary>
    /// The boxes to draw, for a host that binds them (the server client's tiles); a host that
    /// drives the overlay from code calls <see cref="UpdateBoundingBoxes"/> instead.
    /// </summary>
    [DependencyProperty(PropertyChangedCallback = nameof(OnBoundingBoxesChanged))]
    private IReadOnlyList<Rect>? boundingBoxes;

    /// <summary>
    /// The boxes to draw with their track ids, for a host that binds them: each box is smoothed
    /// against the earlier box of its own track.
    /// </summary>
    [DependencyProperty(PropertyChangedCallback = nameof(OnMotionBoxesChanged))]
    private IReadOnlyList<BoundingBox>? motionBoxes;

    [DependencyProperty(DefaultValue = 0)]
    private int videoWidth;

    /// <summary>
    /// The displayed video height (after rotation), used to compute letterbox offset.
    /// When set to 0, falls back to stretching across the full container (legacy behavior).
    /// </summary>
    [DependencyProperty(DefaultValue = 0)]
    private int videoHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionBoundingBoxOverlay"/> class.
    /// </summary>
    public MotionBoundingBoxOverlay()
    {
        InitializeComponent();
        OverlayCanvas.SizeChanged += OnCanvasSizeChanged;
    }

    private static void OnBoundingBoxesChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionBoundingBoxOverlay overlay)
        {
            overlay.UpdateBoundingBoxes(
                e.NewValue as IReadOnlyList<Rect>,
                new Size(overlay.ActualWidth, overlay.ActualHeight));
        }
    }

    private static void OnMotionBoxesChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionBoundingBoxOverlay overlay)
        {
            overlay.UpdateMotionBoxes(
                e.NewValue as IReadOnlyList<BoundingBox>,
                new Size(overlay.ActualWidth, overlay.ActualHeight));
        }
    }

    /// <summary>
    /// Updates the bounding box display with new positions for multiple motion regions.
    /// </summary>
    /// <param name="boundingBoxes">The bounding boxes in analysis coordinates, or null/empty to hide all.</param>
    /// <param name="containerSize">The size of the container to map coordinates to.</param>
    public void UpdateBoundingBoxes(
        IReadOnlyList<Rect>? boundingBoxes,
        Size containerSize)
        => UpdateMotionBoxes(
            boundingBoxes?.Select(b => new BoundingBox { X = b.X, Y = b.Y, Width = b.Width, Height = b.Height }).ToList(),
            containerSize);

    /// <summary>
    /// Updates the bounding box display with boxes that may carry their track ids: a box with an
    /// id is smoothed against the earlier box of its own track.
    /// </summary>
    /// <param name="boundingBoxes">The bounding boxes in analysis coordinates, or null/empty to hide all.</param>
    /// <param name="containerSize">The size of the container to map coordinates to.</param>
    public void UpdateMotionBoxes(
        IReadOnlyList<BoundingBox>? boundingBoxes,
        Size containerSize)
    {
        if (!IsOverlayEnabled ||
            boundingBoxes is null ||
            boundingBoxes.Count == 0 ||
            containerSize.Width <= 0 ||
            containerSize.Height <= 0)
        {
            HideBoundingBoxes();
            return;
        }

        // Store last state for re-rendering on resize
        lastBoundingBoxes = boundingBoxes;
        lastAnalysisWidth = AnalysisWidth;
        lastAnalysisHeight = AnalysisHeight;
        lastVideoWidth = VideoWidth;
        lastVideoHeight = VideoHeight;

        // Prefer the canvas's own size when available (it lives in the overlay window),
        // falling back to the passed containerSize for the initial call before layout.
        if (OverlayCanvas.ActualWidth > 0 && OverlayCanvas.ActualHeight > 0)
        {
            containerSize = new Size(OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight);
        }

        // Limit the number of boxes to prevent performance issues
        var boxCount = Math.Min(boundingBoxes.Count, MaxBoundingBoxes);

        // Ensure we have enough rectangles in the pool
        EnsureRectangles(boxCount);

        // The video is fitted uniformly inside the container, so boxes map into
        // the video area and never onto the black bars. VideoWidth/VideoHeight
        // are the displayed (rotated) size.
        var videoArea = MotionBoxMapper.CalculateVideoArea(
            containerSize.Width,
            containerSize.Height,
            VideoWidth,
            VideoHeight);

        var targets = new List<BoundingBox>(boxCount);
        for (var i = 0; i < boxCount; i++)
        {
            var box = boundingBoxes[i];
            targets.Add(MotionBoxMapper.MapToVideoArea(
                box,
                AnalysisWidth,
                AnalysisHeight,
                videoArea));
        }

        var smoothed = Smoother().Smooth(targets);
        for (var i = 0; i < boxCount; i++)
        {
            var rect = rectanglePool[i];
            Canvas.SetLeft(rect, smoothed[i].X);
            Canvas.SetTop(rect, smoothed[i].Y);
            rect.Width = Math.Max(1, smoothed[i].Width);
            rect.Height = Math.Max(1, smoothed[i].Height);
            rect.Visibility = Visibility.Visible;
            System.Windows.Automation.AutomationProperties.SetAutomationId(rect, boundingBoxes[i].TrackId is { } trackId ? $"box-{trackId}" : $"box-at-{i}");
            System.Windows.Automation.AutomationProperties.SetHelpText(rect, DescribeBox(boundingBoxes[i]));
        }

        // Hide unused rectangles
        for (var i = boxCount; i < rectanglePool.Count; i++)
        {
            rectanglePool[i].Visibility = Visibility.Collapsed;
        }

        System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(this)?.InvalidatePeer();
    }

    /// <summary>
    /// Gets the rectangles of the boxes on screen, for UI Automation.
    /// </summary>
    internal IEnumerable<Rectangle> ShownBoxRectangles()
        => rectanglePool.Where(r => r.Visibility == Visibility.Visible);

    /// <inheritdoc />
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        => new MotionBoundingBoxOverlayAutomationPeer(this);

    /// <summary>
    /// Describes a box as the detector gave it, before smoothing and fitting to the screen:
    /// <c>x,y,width,height</c> in video pixels (in analysis pixels without a video size), so a
    /// UI test can compare it with the detector's output or the truth.
    /// </summary>
    private string DescribeBox(BoundingBox box)
    {
        var scaleX = VideoWidth > 0 && AnalysisWidth > 0 ? VideoWidth / (double)AnalysisWidth : 1;
        var scaleY = VideoHeight > 0 && AnalysisHeight > 0 ? VideoHeight / (double)AnalysisHeight : 1;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{box.X * scaleX:0.##},{box.Y * scaleY:0.##},{box.Width * scaleX:0.##},{box.Height * scaleY:0.##}");
    }

    /// <summary>
    /// Updates the bounding box display with a single position (backward compatibility).
    /// </summary>
    /// <param name="boundingBox">The bounding box in analysis coordinates, or null to hide.</param>
    /// <param name="containerSize">The size of the container to map coordinates to.</param>
    public void UpdateBoundingBox(
        Rect? boundingBox,
        Size containerSize)
    {
        if (boundingBox.HasValue)
        {
            UpdateBoundingBoxes([boundingBox.Value], containerSize);
        }
        else
        {
            UpdateBoundingBoxes(null, containerSize);
        }
    }

    /// <summary>
    /// Hides all bounding box overlays.
    /// </summary>
    public void HideBoundingBoxes()
    {
        lastBoundingBoxes = null;

        foreach (var rect in rectanglePool)
        {
            rect.Visibility = Visibility.Collapsed;
        }

        smoother?.Reset();
        System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(this)?.InvalidatePeer();
    }

    /// <summary>
    /// Hides the bounding box overlay (backward compatibility).
    /// </summary>
    public void HideBoundingBox()
    {
        HideBoundingBoxes();
    }

    /// <summary>
    /// Resets the smoothing state for all boxes, causing the next update to set positions directly.
    /// </summary>
    public void ResetSmoothing()
        => smoother?.Reset();

    private BoxSmoother Smoother()
    {
        if (smoother is null || double.IsNaN(smootherFactor) || Math.Abs(smootherFactor - SmoothingFactor) > 1e-9)
        {
            smoother = new BoxSmoother(SmoothingFactor);
            smootherFactor = SmoothingFactor;
        }

        return smoother;
    }

    private void OnCanvasSizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        ResetSmoothing();
        ReRenderLastBoundingBoxes();
    }

    private void ReRenderLastBoundingBoxes()
    {
        if (lastBoundingBoxes is null ||
            lastBoundingBoxes.Count == 0 ||
            OverlayCanvas.ActualWidth <= 0 ||
            OverlayCanvas.ActualHeight <= 0)
        {
            return;
        }

        // Temporarily set stored values for re-rendering
        var currentAnalysisWidth = AnalysisWidth;
        var currentAnalysisHeight = AnalysisHeight;
        var currentVideoWidth = VideoWidth;
        var currentVideoHeight = VideoHeight;

        AnalysisWidth = lastAnalysisWidth;
        AnalysisHeight = lastAnalysisHeight;
        VideoWidth = lastVideoWidth;
        VideoHeight = lastVideoHeight;

        UpdateMotionBoxes(
            lastBoundingBoxes,
            new Size(OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight));

        AnalysisWidth = currentAnalysisWidth;
        AnalysisHeight = currentAnalysisHeight;
        VideoWidth = currentVideoWidth;
        VideoHeight = currentVideoHeight;
    }

    private void EnsureRectangles(int count)
    {
        // Add more rectangles to the pool if needed
        while (rectanglePool.Count < count)
        {
            var rect = new Rectangle
            {
                Fill = Brushes.Transparent,
                Visibility = Visibility.Collapsed,
            };

            UpdateRectangleBrush(rect);
            UpdateRectangleThickness(rect);

            rectanglePool.Add(rect);
            OverlayCanvas.Children.Add(rect);
        }
    }

    private static void OnIsOverlayEnabledChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionBoundingBoxOverlay overlay && e.NewValue is false)
        {
            overlay.HideBoundingBoxes();
        }
    }

    private static void OnBoxColorChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionBoundingBoxOverlay overlay)
        {
            overlay.UpdateAllBrushes();
        }
    }

    private static void OnBoxThicknessChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionBoundingBoxOverlay overlay)
        {
            overlay.UpdateAllThicknesses();
        }
    }

    private void UpdateAllBrushes()
    {
        foreach (var rect in rectanglePool)
        {
            UpdateRectangleBrush(rect);
        }
    }

    private void UpdateRectangleBrush(Rectangle rect)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(BoxColor);
            rect.Stroke = new SolidColorBrush(color);
        }
        catch
        {
            // Fallback to default red color if parsing fails
            rect.Stroke = new SolidColorBrush(Colors.Red);
        }
    }

    private void UpdateAllThicknesses()
    {
        foreach (var rect in rectanglePool)
        {
            UpdateRectangleThickness(rect);
        }
    }

    private void UpdateRectangleThickness(Rectangle rect)
    {
        rect.StrokeThickness = BoxThickness;
    }
}