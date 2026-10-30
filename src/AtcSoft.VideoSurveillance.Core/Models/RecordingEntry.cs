namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// Represents a recorded video file entry (Core POCO).
/// </summary>
public class RecordingEntry
{
    /// <summary>
    /// Gets the full file path of the recording.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the name of the camera that made the recording.
    /// </summary>
    public required string CameraName { get; init; }

    /// <summary>
    /// Gets the file name without path.
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Gets the date and time when the recording was made.
    /// </summary>
    public DateTime RecordingTime { get; init; }

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets or sets the duration of the recording.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets the formatted file size string.
    /// </summary>
    public string FormattedFileSize => FormatFileSize(FileSizeBytes);

    /// <summary>
    /// Gets the formatted duration string.
    /// </summary>
    public string FormattedDuration => FormatDuration(Duration);

    /// <summary>
    /// Gets the formatted recording time string.
    /// </summary>
    public string FormattedRecordingTime
        => RecordingTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

    /// <summary>
    /// Gets the recording root the file was found under. It is needed to
    /// find a thumbnail that is kept in the separate thumbnails folder.
    /// </summary>
    public string? RecordingRoot { get; init; }

    /// <summary>
    /// Gets the thumbnail file path: wherever the thumbnail actually is, or,
    /// when there is none, next to the video with a .png extension.
    /// </summary>
    public string ThumbnailPath
        => RecordingPathHelper.FindThumbnailPath(FilePath, RecordingRoot)
           ?? Path.ChangeExtension(FilePath, ".png");

    /// <summary>
    /// Gets a value indicating whether a thumbnail file exists for this recording.
    /// </summary>
    public bool HasThumbnail => File.Exists(ThumbnailPath);

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

    /// <inheritdoc />
    public override string ToString()
        => $"RecordingEntry {{ CameraName='{CameraName}', StartTime={RecordingTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}, Duration={FormattedDuration} }}";

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);
        }

        return duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}