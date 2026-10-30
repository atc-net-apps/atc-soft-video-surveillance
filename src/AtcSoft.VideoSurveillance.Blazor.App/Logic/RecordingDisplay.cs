namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// How a recording is shown: its length, size, name and where it is fetched from.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1055:URI return values should not be strings",
    Justification = "The address goes straight into an HTML attribute.")]
[SuppressMessage(
    "Design",
    "CA1054:URI parameters should not be strings",
    Justification = "The base address is a configured string.")]
public static class RecordingDisplay
{
    /// <summary>
    /// Shown where a value is not known.
    /// </summary>
    public const string Unknown = "–";

    private const string FilesPath = "recordings-files";

    /// <summary>
    /// Formats a length for display. The server sends ISO 8601 ("PT1M5S");
    /// a clock value ("00:01:05") is read too.
    /// </summary>
    public static string FormatDuration(string? duration)
    {
        if (!TryParseDuration(duration, out var value) || value <= TimeSpan.Zero)
        {
            return Unknown;
        }

        return value.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{value.Minutes}:{value.Seconds:00}");
    }

    /// <summary>
    /// Gets a length in seconds, as the activity of a recording is asked for.
    /// </summary>
    /// <returns>The seconds, or 0 when the length is not known.</returns>
    public static double DurationSeconds(string? duration)
        => TryParseDuration(duration, out var value) && value > TimeSpan.Zero
            ? value.TotalSeconds
            : 0;

    /// <summary>
    /// Formats a file size for display.
    /// </summary>
    public static string FormatSize(long bytes)
    {
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;

        return bytes switch
        {
            <= 0 => Unknown,
            >= gb => string.Create(CultureInfo.CurrentCulture, $"{bytes / (double)gb:F2} GB"),
            >= mb => string.Create(CultureInfo.CurrentCulture, $"{bytes / (double)mb:F1} MB"),
            >= kb => string.Create(CultureInfo.CurrentCulture, $"{bytes / (double)kb:F1} KB"),
            _ => string.Create(CultureInfo.CurrentCulture, $"{bytes} B"),
        };
    }

    /// <summary>
    /// Gets the file name of a recording from its path.
    /// </summary>
    public static string FileName(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return string.Empty;
        }

        var index = filePath.LastIndexOfAny(['/', '\\']);
        return index < 0 ? filePath : filePath[(index + 1)..];
    }

    /// <summary>
    /// Gets a value indicating whether a recording can be analysed for its activity timeline: a
    /// finished recording without an index.
    /// </summary>
    /// <param name="recording">The recording, as the listing gave it.</param>
    public static bool CanAnalyse(Recording recording)
        => recording is { Activity: null, InProgress: false };

    /// <summary>
    /// Gets a value indicating whether a browser is sure to play the file.
    /// MP4 is; MKV and AVI depend on the browser and usually do not play,
    /// and a browser does not apply the rotation of an MKV.
    /// </summary>
    public static bool PlaysInBrowser(string? filePath)
        => filePath is not null &&
           filePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the address a recording or a thumbnail is fetched from, or
    /// <c>null</c> without a path. Each folder and file name is escaped on
    /// its own, so a camera name with '#', '?', '&amp;', '+' or '%' in it
    /// cannot change the meaning of the address.
    /// </summary>
    /// <param name="apiBaseUrl">The server.</param>
    /// <param name="relativePath">The file, below the recording folder.</param>
    /// <param name="mediaToken">The session's media token: a video or an image sends no header, so the address carries it.</param>
    public static string? FileUrl(
        string apiBaseUrl,
        string? relativePath,
        string? mediaToken = null)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        var escaped = string.Join(
            '/',
            relativePath
                .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        var query = string.IsNullOrEmpty(mediaToken) ? string.Empty : "?media=" + Uri.EscapeDataString(mediaToken);
        return $"{(apiBaseUrl ?? string.Empty).TrimEnd('/')}/{FilesPath}/{escaped}{query}";
    }

    /// <summary>
    /// Gets the address that makes the browser save a recording instead of
    /// showing it: the server sends the file as an attachment.
    /// </summary>
    public static string? DownloadUrl(
        string apiBaseUrl,
        string? relativePath,
        string? mediaToken = null)
        => FileUrl(apiBaseUrl, relativePath, mediaToken) is { } url
            ? url + (url.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "download=1"
            : null;

    private static bool TryParseDuration(
        string? duration,
        out TimeSpan value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(duration))
        {
            return false;
        }

        if (duration.StartsWith('P'))
        {
            try
            {
                value = System.Xml.XmlConvert.ToTimeSpan(duration);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        return TimeSpan.TryParse(duration, CultureInfo.InvariantCulture, out value);
    }
}