namespace AtcSoft.Video.Surveillance.Wpf.Core.Windows;

/// <summary>
/// The motion boxes over a recording's playback: the recording is run through the detector in the
/// background when it opens (with its camera's settings), and the boxes at the player's position
/// are shown while <see cref="ShowMotionBoxes"/> is on. A toggle in the player turns them on and off.
/// </summary>
public sealed partial class FullScreenRecordingWindowViewModel
{
    // As often as CameraWall analyses a live camera, so the boxes move as they do live.
    private static readonly TimeSpan BoxInterval = TimeSpan.FromSeconds(1 / RecordingBoxLoaders.FramesPerSecond);

    private RecordingBoxLoader? boxLoader;
    private RecordingBoxes? recordingBoxes;
    private CancellationTokenSource? boxLoading;
    private DispatcherTimer? boxTimer;

    [ObservableProperty]
    private bool showMotionBoxes = true;

    [ObservableProperty]
    private IReadOnlyList<BoundingBox>? motionBoxes;

    [ObservableProperty]
    private bool isFindingMotionBoxes;

    [ObservableProperty]
    private int boxAnalysisWidth;

    [ObservableProperty]
    private int boxAnalysisHeight;

    /// <summary>
    /// Gets a value indicating whether this player can show motion boxes (CameraWall plays its own
    /// files and analyses them; a client of the server cannot yet).
    /// </summary>
    public bool CanShowMotionBoxes
        => boxLoader is not null;

    /// <summary>
    /// Gets the boxes' colour, as the app's bounding box settings say.
    /// </summary>
    public string BoxColor { get; private set; } = "Red";

    /// <summary>
    /// Gets the boxes' line thickness.
    /// </summary>
    public int BoxThickness { get; private set; } = 2;

    /// <summary>
    /// Shows the recording's motion boxes over its playback, drawn as the app draws them live.
    /// Call before the window opens.
    /// </summary>
    /// <param name="loader">Gets the boxes (<see cref="RecordingBoxLoaders.FromFile"/>).</param>
    /// <param name="style">The app's bounding box settings: colour, thickness, and whether boxes start shown.</param>
    public void UseMotionBoxes(
        RecordingBoxLoader loader,
        BoundingBoxSettings? style = null)
    {
        boxLoader = loader ?? throw new ArgumentNullException(nameof(loader));
        if (style is not null)
        {
            BoxColor = style.Color;
            BoxThickness = style.Thickness;
            ShowMotionBoxes = style.ShowInFullScreen;
        }

        OnPropertyChanged(nameof(CanShowMotionBoxes));
        OnPropertyChanged(nameof(BoxColor));
        OnPropertyChanged(nameof(BoxThickness));
    }

    [RelayCommand]
    private void ToggleMotionBoxes()
        => ShowMotionBoxes = !ShowMotionBoxes;

    // Started when the recording opens: the analysis runs while it plays.
    private async Task FindMotionBoxesAsync()
    {
        if (boxLoader is null)
        {
            return;
        }

        boxLoading = new CancellationTokenSource();
        IsFindingMotionBoxes = true;
        try
        {
            recordingBoxes = await boxLoader(boxLoading.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            IsFindingMotionBoxes = false;
        }

        if (recordingBoxes is null || disposed)
        {
            return;
        }

        BoxAnalysisWidth = recordingBoxes.AnalysisWidth;
        BoxAnalysisHeight = recordingBoxes.AnalysisHeight;
        boxTimer = new DispatcherTimer { Interval = BoxInterval };
        boxTimer.Tick += (_, _) => MotionBoxes = ShowMotionBoxes && mediaElement is not null && recordingBoxes is { } found
            ? found.At(mediaElement.Position)
            : null;
        boxTimer.Start();
    }

    private void StopMotionBoxes()
    {
        boxTimer?.Stop();
        boxTimer = null;
        boxLoading?.Cancel();
        boxLoading?.Dispose();
        boxLoading = null;
    }
}