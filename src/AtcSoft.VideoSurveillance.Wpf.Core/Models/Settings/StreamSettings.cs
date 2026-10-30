using CoreSettings = AtcSoft.VideoSurveillance.Models.Settings;

namespace AtcSoft.VideoSurveillance.Wpf.Core.Models.Settings;

/// <summary>
/// Wraps <see cref="CoreSettings.StreamSettings"/> with change notification for WPF binding.
/// </summary>
public partial class StreamSettings : ObservableObject
{
    internal CoreSettings.StreamSettings Core { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamSettings"/> class.
    /// </summary>
    public StreamSettings()
        : this(new CoreSettings.StreamSettings())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamSettings"/> class
    /// wrapping the specified Core instance.
    /// </summary>
    internal StreamSettings(CoreSettings.StreamSettings core)
    {
        Core = core ?? throw new ArgumentNullException(nameof(core));
    }

    /// <summary>
    /// Gets or sets a value indicating whether to use low latency mode.
    /// </summary>
    public bool UseLowLatencyMode
    {
        get => Core.UseLowLatencyMode;
        set
        {
            if (Core.UseLowLatencyMode == value)
            {
                return;
            }

            Core.UseLowLatencyMode = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the maximum latency in milliseconds.
    /// </summary>
    public int MaxLatencyMs
    {
        get => Core.MaxLatencyMs;
        set
        {
            if (Core.MaxLatencyMs == value)
            {
                return;
            }

            Core.MaxLatencyMs = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the RTSP transport protocol.
    /// </summary>
    public string RtspTransport
    {
        get => Core.RtspTransport;
        set
        {
            if (string.Equals(Core.RtspTransport, value, StringComparison.Ordinal))
            {
                return;
            }

            Core.RtspTransport = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the buffer duration in milliseconds.
    /// </summary>
    public int BufferDurationMs
    {
        get => Core.BufferDurationMs;
        set
        {
            if (Core.BufferDurationMs == value)
            {
                return;
            }

            Core.BufferDurationMs = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Creates a deep copy of this instance.
    /// </summary>
    public StreamSettings Clone()
        => new(Core.Clone());

    /// <summary>
    /// Copies values from another instance.
    /// </summary>
    public void CopyFrom(StreamSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        UseLowLatencyMode = source.UseLowLatencyMode;
        MaxLatencyMs = source.MaxLatencyMs;
        RtspTransport = source.RtspTransport;
        BufferDurationMs = source.BufferDurationMs;
    }

    /// <summary>
    /// Determines whether the specified instance has the same values.
    /// </summary>
    public bool ValueEquals(StreamSettings? other)
        => other is not null && Core.ValueEquals(other.Core);
}