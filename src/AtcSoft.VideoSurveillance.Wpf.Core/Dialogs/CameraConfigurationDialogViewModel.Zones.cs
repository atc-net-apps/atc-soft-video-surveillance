namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

/// <summary>
/// The camera's motion zones in the dialog: drawn on a snapshot, turned with a rotation chosen in
/// the dialog, and saved with the camera.
/// </summary>
public partial class CameraConfigurationDialogViewModel
{
    // How long a snapshot may take, from opening the camera to the first picture.
    private static readonly TimeSpan ZonePictureTimeout = TimeSpan.FromSeconds(15);

    private readonly MotionZoneDraft zoneDraft;

    // The snapshot as taken, and the rotation it was taken at: the tile's picture is turned by the
    // stored rotation, a picture from a player opened here is not turned.
    private BitmapSource? zoneSnapshot;
    private CameraRotation zoneSnapshotRotation;

    [ObservableProperty]
    private ImageSource? zonePicture;

    [ObservableProperty]
    private string? zonePictureStatus = Translations.MotionZonesNoPicture;

    [ObservableProperty(AfterChangedCallback = nameof(OnIsTakingZonePictureChanged))]
    private bool isTakingZonePicture;

    /// <summary>
    /// Gets or sets the camera's motion zones, as shown at the rotation chosen in the dialog. A new
    /// list from the editor is taken as drawn; the zones are copied, never kept.
    /// </summary>
    public IReadOnlyList<MotionZone> MotionZones
    {
        get => zoneDraft.Zones;
        set
        {
            zoneDraft.Change(value ?? []);
            ApplyZones();
        }
    }

    private static void OnIsTakingZonePictureChanged()
        => CommandManager.InvalidateRequerySuggested();

    private void TurnZones(CameraRotation rotation)
    {
        zoneDraft.Turn(rotation);
        ApplyZones();
        ShowZonePicture();
    }

    // The camera (and so Save's change check) gets its own copies of the zones as shown.
    private void ApplyZones()
    {
        Camera.MotionZones = [.. zoneDraft.Zones.Select(z => z.Clone())];
        OnPropertyChanged(nameof(MotionZones));
    }

    private void ShowZonePicture()
        => ZonePicture = zoneSnapshot is null
            ? null
            : MotionZonePicture.Show(zoneSnapshot, zoneSnapshotRotation, Camera.Display.Rotation);

    [RelayCommand("TakeZonePicture", CanExecute = nameof(CanTakeZonePicture))]
    private async Task TakeZonePictureAsync()
    {
        IsTakingZonePicture = true;
        try
        {
            using var cts = new CancellationTokenSource(ZonePictureTimeout);
            var (png, rotation) = await CaptureZonePictureAsync(cts.Token).ConfigureAwait(true);
            if (png is null)
            {
                ZonePictureStatus = Translations.FailedToCaptureSnapshot;
                return;
            }

            zoneSnapshot = MotionZonePicture.Decode(png);
            zoneSnapshotRotation = rotation;
            ZonePictureStatus = null;
            ShowZonePicture();
        }
        catch (Exception ex) when (ex is OperationCanceledException or NotSupportedException or InvalidOperationException or IOException)
        {
            ZonePictureStatus = Translations.FailedToCaptureSnapshot;
        }
        finally
        {
            IsTakingZonePicture = false;
        }
    }

    private bool CanTakeZonePicture()
        => !IsTakingZonePicture && !IsTesting && HasMinimumSourceIdentity();

    private async Task<(byte[]? Png, CameraRotation Rotation)> CaptureZonePictureAsync(
        CancellationToken cancellationToken)
    {
        // The running tile first: a USB camera opens only once, and many network cameras allow few sessions.
        if (livePicture is not null &&
            await livePicture(cancellationToken).ConfigureAwait(true) is { } shown)
        {
            return (shown, originalCamera.Display.Rotation);
        }

        var locator = AtcSoft.VideoSurveillance.Helpers.CameraUriHelper.BuildSourceLocator(Camera.Core);
        return (await CaptureWithPlayerAsync(locator, cancellationToken).ConfigureAwait(true), CameraRotation.None);
    }

    private async Task<byte[]?> CaptureWithPlayerAsync(
        AtcSoft.VideoSurveillance.Helpers.SourceLocator locator,
        CancellationToken cancellationToken)
    {
        using var player = videoPlayerFactory.Create();
        var playing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        player.StateChanged += OnPlayerStateChanged;

        try
        {
            player.Open(locator.Uri, CreateStreamOptions(locator));
            if (!await playing.Task.WaitAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            // Playing comes before the first picture is decoded: ask again for a moment.
            while (true)
            {
                if (await player.CaptureFrameAsync(cancellationToken).ConfigureAwait(false) is { } png)
                {
                    return png;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            player.StateChanged -= OnPlayerStateChanged;
            player.Close();
        }

        void OnPlayerStateChanged(
            object? sender,
            PlayerStateChangedEventArgs e)
        {
            switch (e.NewState)
            {
                case PlayerState.Playing:
                    playing.TrySetResult(true);
                    break;
                case PlayerState.Error:
                    playing.TrySetResult(false);
                    break;
            }
        }
    }
}