namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// Application-level performance settings and defaults for video playback.
/// </summary>
public class PerformanceSettings
{
    public string VideoQuality { get; set; } = "Auto";

    public bool HardwareAcceleration { get; set; } = true;

    public bool LowLatencyMode { get; set; }

    public int BufferDurationMs { get; set; } = 500;

    public string RtspTransport { get; set; } = "tcp";

    public int MaxLatencyMs { get; set; } = 500;

    /// <inheritdoc />
    public override string ToString()
        => $"PerformanceSettings {{ Quality='{VideoQuality}', HwAccel={HardwareAcceleration}, LowLatency={LowLatencyMode} }}";
}