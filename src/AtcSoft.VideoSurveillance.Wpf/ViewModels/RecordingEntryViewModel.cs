namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// Adapts the API Recording model for display in the recordings browser.
/// </summary>
public sealed class RecordingEntryViewModel
{
    private readonly Recording recording;
    private readonly Uri apiBaseUrl;
    private readonly string? mediaToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingEntryViewModel"/> class.
    /// </summary>
    /// <param name="recording">The recording.</param>
    /// <param name="apiBaseUrl">The server.</param>
    /// <param name="mediaToken">The session's media token: the player and the image send no header, so the address carries it.</param>
    public RecordingEntryViewModel(
        Recording recording,
        Uri apiBaseUrl,
        string? mediaToken = null)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(apiBaseUrl);

        this.recording = recording;
        this.apiBaseUrl = apiBaseUrl;
        this.mediaToken = mediaToken;
    }

    public Guid Id => recording.Id;

    public Guid CameraId => recording.CameraId;

    public string CameraName => recording.CameraName ?? string.Empty;

    public string FileName => System.IO.Path.GetFileName(recording.FilePath);

    /// <summary>
    /// Gets the recording's path below the recordings root, as the server names it.
    /// </summary>
    public string FilePath => recording.FilePath;

    public DateTimeOffset RecordingTime => recording.StartedAt;

    public long FileSizeBytes => recording.FileSizeBytes;

    public TimeSpan Duration => ParseIsoDuration(recording.Duration);

    public bool HasThumbnail => recording.HasThumbnail;

    /// <summary>
    /// Gets the recording's activity for the sparkline in the list, or <see langword="null"/> for a
    /// recording without an activity index.
    /// </summary>
    public IReadOnlyList<int>? Activity => recording.Activity;

    /// <summary>
    /// Gets a value indicating whether the recording is still being made.
    /// </summary>
    public bool IsInProgress => recording.InProgress;

    public Uri PlaybackUrl
        => BuildFileUrl(recording.FilePath);

    /// <summary>
    /// Gets where the thumbnail is fetched from, or <c>null</c> when the
    /// recording has none.
    /// </summary>
    public Uri? ThumbnailUrl
        => string.IsNullOrEmpty(recording.ThumbnailPath)
            ? null
            : BuildFileUrl(recording.ThumbnailPath);

    public string FormattedFileSize => FormatFileSize(FileSizeBytes);

    public string FormattedDuration => FormatDuration(Duration);

    public string FormattedRecordingTime
        => RecordingTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

    /// <summary>
    /// Gets where a player or an image fetches a recording's file from, with the session's media token.
    /// </summary>
    /// <param name="apiBaseUrl">The server.</param>
    /// <param name="relativePath">The file's path below the recordings root, as the server names it.</param>
    /// <param name="mediaToken">The session's media token, or <see langword="null"/>.</param>
    public static Uri FileUrl(
        Uri apiBaseUrl,
        string relativePath,
        string? mediaToken)
    {
        ArgumentNullException.ThrowIfNull(apiBaseUrl);

        var query = string.IsNullOrEmpty(mediaToken) ? string.Empty : "?media=" + Uri.EscapeDataString(mediaToken);
        return new Uri($"{apiBaseUrl.GetLeftPart(UriPartial.Authority)}/recordings-files/{AtcSoft.VideoSurveillance.Helpers.RecordingPathHelper.EscapePathSegments(relativePath)}{query}");
    }

    private Uri BuildFileUrl(string relativePath)
        => FileUrl(apiBaseUrl, relativePath, mediaToken);

    private static TimeSpan ParseIsoDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return TimeSpan.Zero;
        }

        try
        {
            return System.Xml.XmlConvert.ToTimeSpan(duration);
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;

        return bytes switch
        {
            >= gb => $"{bytes / (double)gb:F2} GB",
            >= mb => $"{bytes / (double)mb:F2} MB",
            >= kb => $"{bytes / (double)kb:F2} KB",
            _ => $"{bytes} B",
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);
        }

        return duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}