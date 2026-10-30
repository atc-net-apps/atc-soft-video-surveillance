namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// Per-camera setting overrides that allow individual cameras to deviate from application-level defaults.
/// </summary>
public class CameraOverrides
{
    /// <summary>
    /// Gets or sets connection-related overrides.
    /// </summary>
    public ConnectionOverrides Connection { get; set; } = new();

    /// <summary>
    /// Gets or sets camera display overrides.
    /// </summary>
    public CameraDisplayOverrides CameraDisplay { get; set; } = new();

    /// <summary>
    /// Gets or sets performance overrides.
    /// </summary>
    public PerformanceOverrides Performance { get; set; } = new();

    /// <summary>
    /// Gets or sets recording overrides.
    /// </summary>
    public RecordingOverrides Recording { get; set; } = new();

    /// <summary>
    /// Gets or sets motion detection overrides.
    /// </summary>
    public MotionDetectionOverrides MotionDetection { get; set; } = new();

    /// <summary>
    /// Determines whether any override is set.
    /// </summary>
    public bool HasAnyOverride()
        => Connection.HasAnyOverride() ||
           CameraDisplay.HasAnyOverride() ||
           Performance.HasAnyOverride() ||
           Recording.HasAnyOverride() ||
           MotionDetection.HasAnyOverride();

    /// <summary>
    /// Creates a deep copy of this camera overrides.
    /// </summary>
    public CameraOverrides Clone()
        => new()
        {
            Connection = Connection.Clone(),
            CameraDisplay = CameraDisplay.Clone(),
            Performance = Performance.Clone(),
            Recording = Recording.Clone(),
            MotionDetection = MotionDetection.Clone(),
        };

    /// <summary>
    /// Copies values from another camera overrides instance.
    /// </summary>
    public void CopyFrom(CameraOverrides? source)
    {
        Connection.CopyFrom(source?.Connection);
        CameraDisplay.CopyFrom(source?.CameraDisplay);
        Performance.CopyFrom(source?.Performance);
        Recording.CopyFrom(source?.Recording);
        MotionDetection.CopyFrom(source?.MotionDetection);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var count = 0;
        if (Connection.HasAnyOverride())
        {
            count++;
        }

        if (CameraDisplay.HasAnyOverride())
        {
            count++;
        }

        if (Performance.HasAnyOverride())
        {
            count++;
        }

        if (Recording.HasAnyOverride())
        {
            count++;
        }

        if (MotionDetection.HasAnyOverride())
        {
            count++;
        }

        return $"CameraOverrides {{ SectionsOverridden={count.ToString(CultureInfo.InvariantCulture)} }}";
    }

    /// <summary>
    /// Determines whether the specified instance has the same values.
    /// </summary>
    public bool ValueEquals(CameraOverrides? other)
    {
        if (other is null)
        {
            return !HasAnyOverride();
        }

        return Connection.ValueEquals(other.Connection) &&
               CameraDisplay.ValueEquals(other.CameraDisplay) &&
               Performance.ValueEquals(other.Performance) &&
               Recording.ValueEquals(other.Recording) &&
               MotionDetection.ValueEquals(other.MotionDetection);
    }
}