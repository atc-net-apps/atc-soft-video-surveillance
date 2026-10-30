namespace AtcSoft.VideoEngine;

/// <summary>
/// The FFmpeg the engine needs is not installed (none, or another version). The apps catch it
/// and offer to install FFmpeg; <see cref="Check"/> says what was found where.
/// </summary>
public sealed class FFmpegNotReadyException : Exception
{
    public FFmpegNotReadyException()
        : this(FFmpegRuntime.Check())
    {
    }

    public FFmpegNotReadyException(string message)
        : base(message)
    {
        Check = FFmpegRuntime.Check();
    }

    public FFmpegNotReadyException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Check = FFmpegRuntime.Check();
    }

    public FFmpegNotReadyException(FFmpegCheck check)
        : base(check?.Describe())
    {
        ArgumentNullException.ThrowIfNull(check);

        Check = check;
    }

    /// <summary>
    /// Gets what the look for FFmpeg found.
    /// </summary>
    public FFmpegCheck Check { get; }
}