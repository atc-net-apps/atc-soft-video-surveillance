namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// The single owner of where recordings and their thumbnails live on disk.
/// <para>
/// Writers ask it where a new file goes under the configured layout.
/// Readers never look at the layout settings: they walk the tree and ask the
/// helper which camera a file belongs to and where its thumbnail is, so
/// recordings made under any layout, old or new, are found side by side.
/// </para>
/// </summary>
public static partial class RecordingPathHelper
{
    /// <summary>
    /// The folder below the recording root that holds thumbnails when they
    /// are kept apart from the videos. It mirrors the video tree.
    /// </summary>
    public const string ThumbnailsFolderName = "thumbnails";

    /// <summary>
    /// The format of a per-day folder name. It sorts chronologically.
    /// </summary>
    public const string DayFolderFormat = "yyyy-MM-dd";

    /// <summary>
    /// The ending of a recording's activity index, in place of the video's extension.
    /// </summary>
    public const string ActivityExtension = ".activity.jsonl";

    /// <summary>
    /// The ending of a recording's preview pictures (one JPEG of small tiles), in place of the video's extension.
    /// </summary>
    public const string PreviewImageExtension = ".previews.jpg";

    /// <summary>
    /// The ending of the index of a recording's preview pictures, in place of the video's extension.
    /// </summary>
    public const string PreviewIndexExtension = ".previews.json";

    /// <summary>
    /// The ending of a recording's review (a person's verdicts on its motion boxes), in place of the
    /// video's extension.
    /// </summary>
    public const string ReviewExtension = ".review.json";

    private const string TimestampFormat = "yyyyMMdd_HHmmss_fff";
    private const string ThumbnailExtension = ".png";

    private static readonly string[] RecordingVideoExtensions = [".mp4", ".mkv", ".avi"];

    /// <summary>
    /// Gets the folder name used for a camera: its display name with every
    /// character that is invalid in a file name replaced by an underscore.
    /// </summary>
    /// <param name="displayName">The camera's display name.</param>
    /// <param name="cameraId">The camera id, used when the name is empty.</param>
    public static string GetCameraFolderName(
        string? displayName,
        Guid cameraId)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return cameraId.ToString("N")[..8];
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(displayName.Length);
        foreach (var c in displayName)
        {
            sanitized.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
        }

        // A camera must not claim the folder that holds every camera's thumbnails.
        if (sanitized.ToString().Equals(ThumbnailsFolderName, StringComparison.OrdinalIgnoreCase))
        {
            sanitized.Append('_');
        }

        return sanitized.ToString();
    }

    /// <summary>
    /// Builds the path of a new recording:
    /// <c>{root}\{camera}\[{yyyy-MM-dd}\]{camera}_{yyyyMMdd_HHmmss_fff}.{format}</c>.
    /// </summary>
    /// <param name="recordingRoot">The recording root folder.</param>
    /// <param name="cameraFolderName">The camera folder name from <see cref="GetCameraFolderName(string?, Guid)"/>.</param>
    /// <param name="timestamp">The recording start time, as it is written into the file name.</param>
    /// <param name="format">The container format, with or without a leading dot. Defaults to mp4.</param>
    /// <param name="layout">The layout options; <see langword="null"/> means the flat layout.</param>
    /// <remarks>
    /// The day folder always follows the local calendar, also when the file
    /// name carries a UTC timestamp, because people browse by their own day.
    /// </remarks>
    public static string BuildRecordingPath(
        string recordingRoot,
        string cameraFolderName,
        DateTime timestamp,
        string? format,
        RecordingLayoutOptions? layout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordingRoot);
        ArgumentException.ThrowIfNullOrEmpty(cameraFolderName);

        var extension = string.IsNullOrWhiteSpace(format) ? "mp4" : format.TrimStart('.');
        var fileName = $"{cameraFolderName}_{timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture)}.{extension}";
        var folder = Path.Combine(recordingRoot, cameraFolderName);

        if (layout is { UseDailySubfolders: true })
        {
            var localDay = timestamp.Kind == DateTimeKind.Utc ? timestamp.ToLocalTime() : timestamp;
            folder = Path.Combine(folder, localDay.ToString(DayFolderFormat, CultureInfo.InvariantCulture));
        }

        return Path.Combine(folder, fileName);
    }

    /// <summary>
    /// Builds the path a thumbnail is written to for a recording.
    /// </summary>
    /// <param name="videoPath">The recording path.</param>
    /// <param name="recordingRoot">The recording root the video lives under.</param>
    /// <param name="layout">The layout options; <see langword="null"/> means next to the video.</param>
    /// <returns>
    /// Next to the video, or, with <see cref="RecordingLayoutOptions.SeparateThumbnailFolder"/>,
    /// at the same relative path below <c>{root}\thumbnails</c>.
    /// </returns>
    public static string BuildThumbnailPath(
        string videoPath,
        string? recordingRoot,
        RecordingLayoutOptions? layout = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        if (layout is { SeparateThumbnailFolder: true } &&
            TryGetSeparateThumbnailPath(videoPath, recordingRoot, out var separatePath))
        {
            return separatePath;
        }

        return Path.ChangeExtension(videoPath, ThumbnailExtension);
    }

    /// <summary>
    /// Builds an example of where a recording and its thumbnail go under a
    /// layout, relative to the recording folder. For showing a user what a
    /// layout option does before they choose it.
    /// </summary>
    /// <param name="cameraName">The camera name to use in the example.</param>
    /// <param name="timestamp">The recording time to use in the example.</param>
    /// <param name="format">The container format, with or without a leading dot.</param>
    /// <param name="layout">The layout to illustrate.</param>
    public static (string Video, string Thumbnail) BuildExample(
        string cameraName,
        DateTime timestamp,
        string? format,
        RecordingLayoutOptions layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        // Any rooted path will do: only the part below it is returned.
        var root = Path.Combine(Path.GetTempPath(), "recordings");
        var video = BuildRecordingPath(root, GetCameraFolderName(cameraName, Guid.Empty), timestamp, format, layout);
        var thumbnail = BuildThumbnailPath(video, root, layout);

        return (Path.GetRelativePath(root, video), Path.GetRelativePath(root, thumbnail));
    }

    /// <summary>
    /// Gets every place the thumbnail of a recording may be, newest layout
    /// first. Use it to find or delete a thumbnail without knowing which
    /// layout the recording was made under.
    /// </summary>
    /// <param name="videoPath">The recording path.</param>
    /// <param name="recordingRoot">The recording root the video lives under.</param>
    public static IReadOnlyList<string> GetThumbnailCandidates(
        string videoPath,
        string? recordingRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        var sameFolder = Path.ChangeExtension(videoPath, ThumbnailExtension);

        return TryGetSeparateThumbnailPath(videoPath, recordingRoot, out var separatePath)
            ? [separatePath, sameFolder]
            : [sameFolder];
    }

    /// <summary>
    /// Builds the path of a recording's activity index (what moved when). It always sits next to
    /// the video, in every layout, so it moves with the video.
    /// </summary>
    /// <param name="videoPath">The recording path.</param>
    public static string BuildActivityPath(string videoPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        return Path.ChangeExtension(videoPath, ActivityExtension);
    }

    /// <summary>
    /// Builds the path of a recording's preview pictures: the small tiles a timeline shows under
    /// the mouse, in one JPEG. It always sits next to the video, in every layout.
    /// </summary>
    /// <param name="videoPath">The recording path.</param>
    public static string BuildPreviewImagePath(string videoPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        return Path.ChangeExtension(videoPath, PreviewImageExtension);
    }

    /// <summary>
    /// Builds the path of the index of a recording's preview pictures (where each tile is, and of
    /// which moment). It is written last, so the pictures are complete when it is there.
    /// </summary>
    /// <param name="videoPath">The recording path.</param>
    public static string BuildPreviewIndexPath(string videoPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        return Path.ChangeExtension(videoPath, PreviewIndexExtension);
    }

    /// <summary>
    /// Builds the path of a recording's review: a person's verdicts on its motion boxes
    /// (<c>Helpers.Review.RecordingReviewFile</c>). It always sits next to the video, in every layout.
    /// </summary>
    /// <param name="videoPath">The recording path.</param>
    public static string BuildReviewPath(string videoPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        return Path.ChangeExtension(videoPath, ReviewExtension);
    }

    /// <summary>
    /// Gets the files that always sit next to a recording's video, in every layout: its activity
    /// index, its preview pictures with their index, and its review.
    /// </summary>
    /// <param name="videoPath">The recording path.</param>
    public static IReadOnlyList<string> GetSidecarPaths(string videoPath)
        => [BuildActivityPath(videoPath), BuildPreviewImagePath(videoPath), BuildPreviewIndexPath(videoPath), BuildReviewPath(videoPath)];

    /// <summary>
    /// Gets every file that belongs to a recording besides the video: the places its thumbnail
    /// may be, its activity index, its preview pictures and its review. Use it to delete or protect a
    /// recording with all it has.
    /// </summary>
    /// <param name="videoPath">The recording path.</param>
    /// <param name="recordingRoot">The recording root the video lives under.</param>
    public static IReadOnlyList<string> GetCompanionPaths(
        string videoPath,
        string? recordingRoot)
        => [.. GetThumbnailCandidates(videoPath, recordingRoot), .. GetSidecarPaths(videoPath)];

    /// <summary>
    /// Finds the existing thumbnail of a recording, whichever layout it was made under.
    /// </summary>
    /// <param name="videoPath">The recording path.</param>
    /// <param name="recordingRoot">The recording root the video lives under.</param>
    /// <returns>The thumbnail path, or <see langword="null"/> when there is none.</returns>
    public static string? FindThumbnailPath(
        string videoPath,
        string? recordingRoot)
    {
        foreach (var candidate in GetThumbnailCandidates(videoPath, recordingRoot))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the camera folder a file belongs to: the first folder below the
    /// recording root. This holds for every layout, also with a day folder
    /// between the camera folder and the file.
    /// </summary>
    /// <param name="filePath">The path of a recording.</param>
    /// <param name="recordingRoot">The recording root.</param>
    /// <returns>The camera folder name, or <see langword="null"/> when the file is not inside a camera folder.</returns>
    public static string? GetCameraFolderFromPath(
        string filePath,
        string recordingRoot)
    {
        var segments = GetRelativeSegments(filePath, recordingRoot);

        // {camera}\...\file — at least a folder and a file name.
        return segments is { Length: >= 2 } ? segments[0] : null;
    }

    /// <summary>
    /// Determines whether a path lies inside the separate thumbnails folder.
    /// </summary>
    /// <param name="path">The file or folder path.</param>
    /// <param name="recordingRoot">The recording root.</param>
    public static bool IsInThumbnailsFolder(
        string path,
        string recordingRoot)
    {
        var segments = GetRelativeSegments(path, recordingRoot);

        return segments is { Length: >= 1 } &&
               segments[0].Equals(ThumbnailsFolderName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the recording start time from a recording file name. Accepts
    /// names with or without milliseconds, and with a collision suffix.
    /// </summary>
    /// <param name="fileName">The file name or path, e.g. <c>Front Door_20261012_101500_123_2.mkv</c>.</param>
    /// <param name="timestamp">The parsed time, as written in the name.</param>
    public static bool TryParseTimestamp(
        string? fileName,
        out DateTime timestamp)
    {
        timestamp = default;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var match = TimestampRegex().Match(Path.GetFileNameWithoutExtension(fileName));
        if (!match.Success)
        {
            return false;
        }

        if (!DateTime.TryParseExact(
                match.Groups["date"].Value + match.Groups["time"].Value,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        if (match.Groups["ms"].Success)
        {
            parsed = parsed.AddMilliseconds(int.Parse(match.Groups["ms"].Value, CultureInfo.InvariantCulture));
        }

        timestamp = parsed;
        return true;
    }

    /// <summary>
    /// Turns a recording path relative to the recording root into a URL path.
    /// Each folder and file name is escaped on its own, so a name containing
    /// <c>#</c>, <c>?</c>, <c>&amp;</c>, <c>+</c> or <c>%</c> cannot be read as
    /// a fragment, a query or an escape sequence, and the separators stay.
    /// </summary>
    /// <param name="relativePath">The relative path, with either kind of slash.</param>
    public static string EscapePathSegments(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return string.Empty;
        }

        var segments = relativePath.Split(
            ['/', '\\'],
            StringSplitOptions.RemoveEmptyEntries);

        return string.Join('/', segments.Select(Uri.EscapeDataString));
    }

    /// <summary>
    /// Resolves the path of a recording that came from outside (a client names a recording by the
    /// path the listing gave it). It may only lead to a video in a camera folder below the root.
    /// </summary>
    /// <param name="recordingRoot">The recording folder.</param>
    /// <param name="relativePath">The recording's path below it (<c>{camera}/.../{file}</c>), with either kind of slash.</param>
    /// <param name="fullPath">The full path, when it names a recording.</param>
    /// <returns>Whether the path names a recording; whether the file is there is not checked.</returns>
    public static bool TryResolveRecording(
        string recordingRoot,
        string? relativePath,
        out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(recordingRoot) || string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var relative = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relative))
        {
            return false;
        }

        var root = Path.GetFullPath(recordingRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, relative));

        // ".." may not lead out of the recording folder.
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // {camera}\...\{file}: a video, in a camera folder, not among the thumbnails.
        if (!RecordingVideoExtensions.Contains(Path.GetExtension(candidate), StringComparer.OrdinalIgnoreCase) ||
            RecordingPathHelper.GetCameraFolderFromPath(candidate, recordingRoot) is null ||
            RecordingPathHelper.IsInThumbnailsFolder(candidate, recordingRoot))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    /// <summary>
    /// Determines whether a recording is still being written, that is,
    /// whether another process or thread has the file open for writing.
    /// Such a file has no finished header or index yet, so reading its
    /// length or duration gives nothing useful.
    /// </summary>
    /// <param name="filePath">The recording path.</param>
    public static bool IsBeingWritten(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            // Asking to share the file with readers only is refused while a
            // writer has it open.
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryGetSeparateThumbnailPath(
        string videoPath,
        string? recordingRoot,
        out string thumbnailPath)
    {
        thumbnailPath = string.Empty;

        if (string.IsNullOrEmpty(recordingRoot))
        {
            return false;
        }

        var segments = GetRelativeSegments(videoPath, recordingRoot);
        if (segments is null ||
            segments.Length == 0 ||
            segments[0].Equals(ThumbnailsFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = Path.Combine(segments);
        thumbnailPath = Path.ChangeExtension(
            Path.Combine(Path.GetFullPath(recordingRoot), ThumbnailsFolderName, relative),
            ThumbnailExtension);
        return true;
    }

    /// <summary>
    /// Splits a path into its segments below the root, or returns
    /// <see langword="null"/> when the path is not below the root.
    /// </summary>
    private static string[]? GetRelativeSegments(
        string path,
        string root)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root))
        {
            return null;
        }

        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        if (relative == "." ||
            Path.IsPathRooted(relative) ||
            relative.StartsWith("..", StringComparison.Ordinal))
        {
            return null;
        }

        return relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
    }

    // {name}_{yyyyMMdd}_{HHmmss}[_{fff}][_{collision suffix}]
    [GeneratedRegex(@"_(?<date>\d{8})_(?<time>\d{6})(?:_(?<ms>\d{3}))?(?:_\d{1,2})?$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TimestampRegex();
}