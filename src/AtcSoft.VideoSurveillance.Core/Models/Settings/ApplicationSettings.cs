namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// Root container for all application settings.
/// </summary>
public class ApplicationSettings
{
    public GeneralSettings General { get; set; } = new();

    public CameraDisplayAppSettings CameraDisplay { get; set; } = new();

    public ConnectionAppSettings Connection { get; set; } = new();

    public PerformanceSettings Performance { get; set; } = new();

    public MotionDetectionSettings MotionDetection { get; set; } = new();

    public RecordingSettings Recording { get; set; } = new();

    public AdvancedSettings Advanced { get; set; } = new();

    /// <inheritdoc />
    public override string ToString()
        => "ApplicationSettings";
}