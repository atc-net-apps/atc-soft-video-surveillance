namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// The recording a review is of: its file name and its SHA-256, so a review kept apart from its
/// recording (a corpus) still finds it, and a review of another file is never used.
/// </summary>
public sealed class ReviewRecording
{
    /// <summary>
    /// Gets or sets the recording's file name, without its folder.
    /// </summary>
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 of the recording's bytes, in lower-case hex.
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;
}