namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// USB-camera-specific subset of <see cref="ConnectionSettings"/>.
/// Populated only when the parent camera's
/// <see cref="ConnectionSettings.Source"/> equals
/// <see cref="Enums.CameraSource.Usb"/>.
/// </summary>
public class UsbConnectionSettings
{
    /// <summary>
    /// Stable device identifier — the DirectShow / Media Foundation
    /// symbolic link such as
    /// <c>\\?\usb#vid_046d&amp;pid_085e&amp;mi_00#...</c>.
    /// This is the <em>only</em> identifier that survives a USB hub
    /// reshuffle; the friendly name is for display only.
    /// </summary>
    [Required(ErrorMessage = "USB Device ID is required when source is Usb.")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name as reported by the OS (e.g. <c>Logitech BRIO</c>).
    /// Display-only; do not use for identity.
    /// </summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>
    /// Capture format. <c>null</c> = let the driver pick its default
    /// (matches FFmpeg behaviour when <c>video_size</c>/<c>framerate</c>
    /// are not set).
    /// </summary>
    public UsbStreamFormat? Format { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the recorder also opens the camera's
    /// integrated microphone (if any) and remuxes audio alongside video.
    /// Off by default — the user must opt in. Pair with
    /// <see cref="AudioDeviceName"/> to identify which DirectShow audio
    /// capture device to open; without that the flag is ignored at
    /// stream-open time so we don't blindly grab the first audio
    /// endpoint on the host.
    /// </summary>
    public bool PreferAudio { get; set; }

    /// <summary>
    /// DirectShow friendly name of the companion audio capture device
    /// (e.g. <c>Microphone (Logitech BRIO)</c>). Optional; only consulted
    /// when <see cref="PreferAudio"/> is <see langword="true"/>. Free-form
    /// string because the dshow demuxer accepts whatever name shows up
    /// in <c>ffmpeg -list_devices true -f dshow -i dummy</c>.
    /// </summary>
    public string AudioDeviceName { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString()
        => $"UsbConnectionSettings {{ FriendlyName='{FriendlyName}', Format={Format?.ToString() ?? "(default)"}, Audio={PreferAudio}, AudioDevice='{AudioDeviceName}' }}";

    public UsbConnectionSettings Clone()
        => new()
        {
            DeviceId = DeviceId,
            FriendlyName = FriendlyName,
            Format = Format?.Clone(),
            PreferAudio = PreferAudio,
            AudioDeviceName = AudioDeviceName,
        };

    public void CopyFrom(UsbConnectionSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        DeviceId = source.DeviceId;
        FriendlyName = source.FriendlyName;
        Format = source.Format?.Clone();
        PreferAudio = source.PreferAudio;
        AudioDeviceName = source.AudioDeviceName;
    }

    public bool ValueEquals(UsbConnectionSettings? other)
    {
        if (other is null)
        {
            return false;
        }

        var formatsEqual = (Format is null && other.Format is null) ||
                           (Format?.ValueEquals(other.Format) == true);

        return string.Equals(DeviceId, other.DeviceId, StringComparison.Ordinal) &&
               string.Equals(FriendlyName, other.FriendlyName, StringComparison.Ordinal) &&
               string.Equals(AudioDeviceName, other.AudioDeviceName, StringComparison.Ordinal) &&
               PreferAudio == other.PreferAudio &&
               formatsEqual;
    }
}