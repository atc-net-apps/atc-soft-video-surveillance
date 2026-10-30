namespace AtcSoft.VideoSurveillance.Wpf.Core.Models;

/// <summary>
/// Represents a recorded video file entry.
/// </summary>
public class RecordingEntry : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

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
    public long FileSizeBytes { get; private set; }

    /// <summary>
    /// Gets or sets the duration of the recording.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets the recording's activity for the sparkline in the list, or <see langword="null"/> for a
    /// recording without an activity index.
    /// </summary>
    public IReadOnlyList<int>? Activity { get; init; }

    /// <summary>
    /// Gets a value indicating whether the file is still being recorded,
    /// as far as the list knows. Such a file has no finished index: it has
    /// no length to show and plays as a black picture.
    /// </summary>
    public bool IsBeingRecorded { get; private set; }

    /// <summary>
    /// Creates the entry of a file that is still being recorded.
    /// </summary>
    /// <param name="filePath">The full file path.</param>
    /// <param name="cameraName">The camera that makes the recording.</param>
    /// <param name="recordingRoot">The recording root the file was found under.</param>
    /// <param name="recordingTime">When the recording started.</param>
    /// <param name="fileSizeBytes">The size of the file so far.</param>
    /// <returns>The entry.</returns>
    public static RecordingEntry Ongoing(
        string filePath,
        string cameraName,
        string? recordingRoot,
        DateTime recordingTime,
        long fileSizeBytes)
        => new()
        {
            FilePath = filePath,
            CameraName = cameraName,
            RecordingRoot = recordingRoot,
            RecordingTime = recordingTime,
            FileSizeBytes = fileSizeBytes,
            IsBeingRecorded = true,
        };

    /// <summary>
    /// Creates the entry of a finished recording.
    /// </summary>
    /// <param name="filePath">The full file path.</param>
    /// <param name="cameraName">The camera that made the recording.</param>
    /// <param name="recordingRoot">The recording root the file was found under.</param>
    /// <param name="recordingTime">When the recording started.</param>
    /// <param name="fileSizeBytes">The size of the file.</param>
    /// <param name="duration">The length of the recording.</param>
    /// <returns>The entry.</returns>
    public static RecordingEntry Finished(
        string filePath,
        string cameraName,
        string? recordingRoot,
        DateTime recordingTime,
        long fileSizeBytes,
        TimeSpan duration)
        => new()
        {
            FilePath = filePath,
            CameraName = cameraName,
            RecordingRoot = recordingRoot,
            RecordingTime = recordingTime,
            FileSizeBytes = fileSizeBytes,
            Duration = duration,
        };

    /// <summary>
    /// Gets the entry with the recording's activity for the sparkline.
    /// </summary>
    /// <param name="activity">The sparkline, or <see langword="null"/> without an index.</param>
    /// <returns>A copy of the entry with it.</returns>
    public RecordingEntry WithActivity(IReadOnlyList<int>? activity)
        => new()
        {
            FilePath = FilePath,
            CameraName = CameraName,
            RecordingRoot = RecordingRoot,
            RecordingTime = RecordingTime,
            FileSizeBytes = FileSizeBytes,
            Duration = Duration,
            IsBeingRecorded = IsBeingRecorded,
            Activity = activity,
        };

    /// <summary>
    /// The recording has ended: the entry becomes that of an ordinary
    /// recording, with the size and length the finished file has. Its
    /// thumbnail was written when the recording stopped.
    /// </summary>
    /// <param name="fileSizeBytes">The size of the finished file.</param>
    /// <param name="duration">The length of the recording.</param>
    public void MarkFinished(
        long fileSizeBytes,
        TimeSpan duration)
    {
        FileSizeBytes = fileSizeBytes;
        Duration = duration;
        IsBeingRecorded = false;

        foreach (var name in (string[])[
            nameof(IsBeingRecorded), nameof(FileSizeBytes), nameof(Duration),
            nameof(FormattedFileSize), nameof(FormattedDuration),
            nameof(ThumbnailPath), nameof(HasThumbnail), nameof(ThumbnailImage)])
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Gets the formatted file size string.
    /// </summary>
    public string FormattedFileSize => FormatFileSize(FileSizeBytes);

    /// <summary>
    /// Gets the formatted duration string.
    /// </summary>
    public string FormattedDuration
        => IsBeingRecorded ? Translations.RecordingInProgress : FormatDuration(Duration);

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

    /// <summary>
    /// Gets the thumbnail image, or null if the thumbnail doesn't exist.
    /// This property safely loads the image and handles missing files gracefully.
    /// </summary>
    public BitmapImage? ThumbnailImage
    {
        get
        {
            var path = ThumbnailPath;
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
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