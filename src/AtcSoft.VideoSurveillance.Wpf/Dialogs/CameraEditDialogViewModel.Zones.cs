namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

using CoreCameraRotation = AtcSoft.VideoSurveillance.Enums.CameraRotation;
using CoreMotionZone = AtcSoft.VideoSurveillance.Models.MotionZone;

/// <summary>
/// The camera's motion zones in the dialog: drawn on a snapshot from the server, turned with a
/// rotation chosen in the dialog, and sent only when they changed, so the server keeps (and turns)
/// the stored ones otherwise.
/// </summary>
public partial class CameraEditDialogViewModel
{
    // How long the server may take for a snapshot: it may have to open the camera first.
    private static readonly TimeSpan ZonePictureTimeout = TimeSpan.FromSeconds(30);

    private readonly MotionZoneDraft zoneDraft;
    private readonly Func<Guid, CancellationToken, Task<byte[]?>>? takeSnapshot;

    // The server's snapshot is turned by the camera's stored rotation.
    private readonly CoreCameraRotation storedRotation;
    private BitmapSource? zoneSnapshot;

    [ObservableProperty]
    private ImageSource? zonePicture;

    [ObservableProperty]
    private string? zonePictureStatus;

    [ObservableProperty(AfterChangedCallback = nameof(OnFormChanged))]
    private bool isTakingZonePicture;

    /// <summary>
    /// Gets or sets the camera's motion zones, as shown at the rotation chosen in the dialog. A new
    /// list from the editor is taken as drawn; the zones are copied, never kept.
    /// </summary>
    public IReadOnlyList<CoreMotionZone> MotionZones
    {
        get => zoneDraft.Zones;
        set
        {
            zoneDraft.Change(value ?? []);
            OnPropertyChanged(nameof(MotionZones));
        }
    }

    /// <summary>
    /// Gets the zones to send: all of them for a new camera (none when there are none), and for an
    /// existing one only when they changed, so the server keeps and turns the stored ones.
    /// </summary>
    private List<MotionZone>? ZonesToSend
        => (IsEdit ? zoneDraft.IsChanged : zoneDraft.Zones.Count > 0)
            ? [.. zoneDraft.Zones.Select(z => z.ToApiZone())]
            : null;

    private void OnSelectedRotationChanged()
    {
        zoneDraft.Turn(SelectedRotation.ToCoreRotation());
        OnPropertyChanged(nameof(MotionZones));
        ShowZonePicture();
    }

    private void ShowZonePicture()
        => ZonePicture = zoneSnapshot is null
            ? null
            : MotionZonePicture.Show(zoneSnapshot, storedRotation, SelectedRotation.ToCoreRotation());

    [RelayCommand("TakeZonePicture", CanExecute = nameof(CanTakeZonePicture))]
    private async Task TakeZonePictureAsync()
    {
        IsTakingZonePicture = true;
        try
        {
            using var cts = new CancellationTokenSource(ZonePictureTimeout);
            var png = await takeSnapshot!(cameraId!.Value, cts.Token).ConfigureAwait(true);
            if (png is null)
            {
                ZonePictureStatus = Translations.FailedToCaptureSnapshot;
                return;
            }

            zoneSnapshot = MotionZonePicture.Decode(png);
            ZonePictureStatus = null;
            ShowZonePicture();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or NotSupportedException or InvalidOperationException or IOException)
        {
            ZonePictureStatus = Translations.FailedToCaptureSnapshot;
        }
        finally
        {
            IsTakingZonePicture = false;
        }
    }

    private bool CanTakeZonePicture()
        => takeSnapshot is not null && cameraId is not null && !IsTakingZonePicture;
}