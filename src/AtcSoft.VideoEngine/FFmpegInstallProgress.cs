namespace AtcSoft.VideoEngine;

/// <summary>
/// How far an FFmpeg install has come.
/// </summary>
/// <param name="Stage">The current step.</param>
/// <param name="BytesDone">The bytes downloaded so far.</param>
/// <param name="BytesTotal">The size of the download, when known.</param>
public sealed record FFmpegInstallProgress(
    FFmpegInstallStage Stage,
    long BytesDone,
    long? BytesTotal)
{
    /// <summary>
    /// Gets the share of the download done, from 0 to 1, or <see langword="null"/> when the size is not known.
    /// </summary>
    public double? Fraction
        => BytesTotal is > 0 ? Math.Clamp((double)BytesDone / BytesTotal.Value, 0, 1) : null;
}