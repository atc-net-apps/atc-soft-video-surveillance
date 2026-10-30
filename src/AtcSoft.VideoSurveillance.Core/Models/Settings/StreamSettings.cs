namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// Represents streaming settings for a network camera (Core POCO).
/// </summary>
public class StreamSettings
{
    public bool UseLowLatencyMode { get; set; } = true;

    public int MaxLatencyMs { get; set; } = 500;

    public string RtspTransport { get; set; } = "tcp";

    public int BufferDurationMs { get; set; }

    /// <inheritdoc />
    public override string ToString()
        => $"StreamSettings {{ Transport='{RtspTransport}', Buffer={BufferDurationMs.ToString(CultureInfo.InvariantCulture)}ms }}";

    public StreamSettings Clone()
        => new()
        {
            UseLowLatencyMode = UseLowLatencyMode,
            MaxLatencyMs = MaxLatencyMs,
            RtspTransport = RtspTransport,
            BufferDurationMs = BufferDurationMs,
        };

    public void CopyFrom(StreamSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        UseLowLatencyMode = source.UseLowLatencyMode;
        MaxLatencyMs = source.MaxLatencyMs;
        RtspTransport = source.RtspTransport;
        BufferDurationMs = source.BufferDurationMs;
    }

    public bool ValueEquals(StreamSettings? other)
    {
        if (other is null)
        {
            return false;
        }

        return UseLowLatencyMode == other.UseLowLatencyMode &&
               MaxLatencyMs == other.MaxLatencyMs &&
               string.Equals(RtspTransport, other.RtspTransport, StringComparison.Ordinal) &&
               BufferDurationMs == other.BufferDurationMs;
    }
}