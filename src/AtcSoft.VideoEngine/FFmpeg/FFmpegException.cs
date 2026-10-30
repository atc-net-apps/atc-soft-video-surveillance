namespace AtcSoft.VideoEngine.FFmpeg;

/// <summary>
/// Exception thrown when an FFmpeg operation fails.
/// </summary>
public sealed class FFmpegException : Exception
{
    public FFmpegException()
    {
    }

    public FFmpegException(string message)
        : base(message)
    {
    }

    public FFmpegException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }

    public FFmpegException(
        int errorCode,
        string? messagePrefix = null)
        : base(FormatMessage(errorCode, messagePrefix))
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the FFmpeg error code.
    /// </summary>
    public int ErrorCode { get; }

    private static string FormatMessage(
        int errorCode,
        string? prefix)
    {
        var ffmpegMessage = FFmpegLoader.ErrorCodeToMessage(errorCode);
        return string.IsNullOrEmpty(prefix)
            ? $"FFmpeg error {errorCode}: {ffmpegMessage}"
            : $"{prefix}: FFmpeg error {errorCode}: {ffmpegMessage}";
    }
}