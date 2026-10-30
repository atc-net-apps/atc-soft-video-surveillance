namespace AtcSoft.VideoEngine;

/// <summary>
/// An FFmpeg install that failed: the download, its checksum, or its contents. The target
/// folder is left as it was.
/// </summary>
public sealed class FFmpegInstallException : Exception
{
    public FFmpegInstallException()
    {
    }

    public FFmpegInstallException(string message)
        : base(message)
    {
    }

    public FFmpegInstallException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}