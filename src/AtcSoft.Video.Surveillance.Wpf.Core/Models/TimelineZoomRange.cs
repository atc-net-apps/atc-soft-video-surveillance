namespace AtcSoft.Video.Surveillance.Wpf.Core.Models;

/// <summary>
/// The range slider under a timeline (CameraWall's and the desktop client's): its two ends, in
/// minutes from the span's start, and when the person moved them. Only a move the person made
/// zooms: a slider also changes its ends by itself (it clamps them to its own maximum before the
/// bound one arrives), and such a change is put back. A move is told once the thumbs rest, so a
/// drag does not reload the lanes many times.
/// </summary>
public sealed partial class TimelineZoomRange : ObservableObject
{
    // A range slider's thumbs move many times a second: the lanes are read again once they rest.
    private static readonly TimeSpan SettleInterval = TimeSpan.FromMilliseconds(350);

    private readonly Action<double, double> settled;
    private bool setting;
    private bool touched;
    private double wantedStart;
    private double wantedEnd = 24 * 60;
    private DispatcherTimer? timer;

    [ObservableProperty]
    private double maximum = 24 * 60;

    [ObservableProperty(AfterChangedCallback = nameof(OnEndsChanged))]
    private double start;

    [ObservableProperty(AfterChangedCallback = nameof(OnEndsChanged))]
    private double end = 24 * 60;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineZoomRange"/> class.
    /// </summary>
    /// <param name="settled">Told the ends, once the person's move has rested.</param>
    public TimelineZoomRange(Action<double, double> settled)
        => this.settled = settled ?? throw new ArgumentNullException(nameof(settled));

    /// <summary>
    /// Gets a value indicating whether the ends cover the whole span.
    /// </summary>
    public bool IsWhole
        => AtcSoft.Video.Surveillance.Helpers.Activity.TimelineSpans.IsWhole(Start, End, Maximum);

    /// <summary>
    /// Sets the slider from the code: a new span, or the whole of it again.
    /// </summary>
    /// <param name="startMinutes">The lower end.</param>
    /// <param name="endMinutes">The upper end.</param>
    /// <param name="maximumMinutes">The span's length.</param>
    public void Set(
        double startMinutes,
        double endMinutes,
        double maximumMinutes)
    {
        (wantedStart, wantedEnd) = (startMinutes, endMinutes);
        setting = true;
        try
        {
            Maximum = maximumMinutes;
            Start = startMinutes;
            End = endMinutes;
        }
        finally
        {
            setting = false;
        }
    }

    /// <summary>
    /// Tells that the person took hold of the slider (mouse or keys): its next moves zoom.
    /// </summary>
    public void Touch()
        => touched = true;

    private void OnEndsChanged()
    {
        if (setting)
        {
            return;
        }

        if (!touched)
        {
            // The slider moved its ends by itself: put back what the code set.
            if (Math.Abs(Start - wantedStart) > 0.5 || Math.Abs(End - wantedEnd) > 0.5)
            {
                _ = Application.Current?.Dispatcher.BeginInvoke(() => Set(wantedStart, wantedEnd, Maximum));
            }

            return;
        }

        if (timer is null)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = SettleInterval };
            timer.Tick += OnSettled;
        }

        timer.Stop();
        timer.Start();
    }

    private void OnSettled(
        object? sender,
        EventArgs e)
    {
        timer?.Stop();
        touched = false;
        (wantedStart, wantedEnd) = (Start, End);
        settled(Start, End);
    }
}