namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Moves existing recordings and thumbnails to where the current layout
/// options would have put them, so the recording folder always matches the
/// settings. Safe to run at any time and as often as wanted: a folder that
/// already matches is left untouched.
/// </summary>
public static class RecordingLayoutReorganizer
{
    /// <summary>
    /// Brings every camera folder below <paramref name="recordingRoot"/> in
    /// line with <paramref name="layout"/>.
    /// </summary>
    /// <param name="recordingRoot">The recording root.</param>
    /// <param name="layout">The layout the folder should have.</param>
    /// <param name="videoExtensions">The recording file extensions, with a leading dot.</param>
    /// <param name="fileNamesAreUtc">
    /// Whether the timestamp in a file name is UTC (the server) or local
    /// time (the standalone app). Day folders always follow the local day.
    /// </param>
    /// <returns>What was moved. Failures are returned, never thrown.</returns>
    /// <remarks>
    /// A recording that is still being written is skipped together with its
    /// thumbnail; it is picked up by a later run. Nothing is ever
    /// overwritten: a file whose new name is taken stays where it is and is
    /// reported.
    /// </remarks>
    public static RecordingLayoutReorganizeResult Reorganize(
        string recordingRoot,
        RecordingLayoutOptions layout,
        IReadOnlyCollection<string> videoExtensions,
        bool fileNamesAreUtc = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordingRoot);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(videoExtensions);

        var errors = new List<MediaCleanupRunError>();
        var emptiedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var movedRecordings = 0;
        var movedThumbnails = 0;

        if (!Directory.Exists(recordingRoot))
        {
            return new RecordingLayoutReorganizeResult(movedRecordings, movedThumbnails, errors);
        }

        var root = Path.GetFullPath(recordingRoot);
        var extensions = videoExtensions as IReadOnlySet<string>
            ?? new HashSet<string>(videoExtensions, StringComparer.OrdinalIgnoreCase);

        foreach (var video in EnumerateRecordings(root, extensions, errors))
        {
            try
            {
                if (RecordingPathHelper.IsBeingWritten(video))
                {
                    continue;
                }

                var cameraFolder = RecordingPathHelper.GetCameraFolderFromPath(video, root);
                if (cameraFolder is null)
                {
                    continue;
                }

                var targetVideo = GetTargetVideoPath(video, root, cameraFolder, layout, fileNamesAreUtc);

                // Look for the thumbnail before the video moves: it is found
                // relative to where the video is now.
                var thumbnail = RecordingPathHelper.FindThumbnailPath(video, root);

                if (!Move(video, targetVideo, emptiedFolders, errors))
                {
                    continue;
                }

                if (!PathEquals(video, targetVideo))
                {
                    movedRecordings++;

                    // The activity index and the previews sit next to the video in every layout.
                    var sidecars = RecordingPathHelper.GetSidecarPaths(video);
                    var targets = RecordingPathHelper.GetSidecarPaths(targetVideo);
                    for (var i = 0; i < sidecars.Count; i++)
                    {
                        if (File.Exists(sidecars[i]))
                        {
                            Move(sidecars[i], targets[i], emptiedFolders, errors);
                        }
                    }
                }

                if (thumbnail is null)
                {
                    continue;
                }

                var targetThumbnail = RecordingPathHelper.BuildThumbnailPath(targetVideo, root, layout);
                if (Move(thumbnail, targetThumbnail, emptiedFolders, errors) && !PathEquals(thumbnail, targetThumbnail))
                {
                    movedThumbnails++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(new MediaCleanupRunError(video, ex));
            }
        }

        RemoveEmptied(emptiedFolders, root, errors);

        return new RecordingLayoutReorganizeResult(movedRecordings, movedThumbnails, errors);
    }

    /// <summary>
    /// Brings the recording folder in line with <paramref name="layout"/>,
    /// including the recordings that are running. A file that is being
    /// written cannot be moved, so a running recording that is not where the
    /// layout puts it is restarted first: the recorder closes its file and
    /// carries on in a new one at the new place, and the closed file is then
    /// moved like any other.
    /// </summary>
    /// <param name="recordingRoot">The recording root.</param>
    /// <param name="layout">The layout the folder should have.</param>
    /// <param name="videoExtensions">The recording file extensions, with a leading dot.</param>
    /// <param name="activeSessions">The recordings that are running.</param>
    /// <param name="restartRecording">
    /// Restarts the recording of a camera in a new file; returns whether it did.
    /// </param>
    /// <param name="fileNamesAreUtc">Whether file-name timestamps are UTC.</param>
    public static RecordingLayoutReorganizeResult Synchronize(
        string recordingRoot,
        RecordingLayoutOptions layout,
        IReadOnlyCollection<string> videoExtensions,
        IReadOnlyList<RecordingSession> activeSessions,
        Func<Guid, bool> restartRecording,
        bool fileNamesAreUtc = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordingRoot);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(activeSessions);
        ArgumentNullException.ThrowIfNull(restartRecording);

        foreach (var session in activeSessions)
        {
            if (!string.IsNullOrEmpty(session.CurrentFilePath) &&
                !IsInLayout(session.CurrentFilePath, recordingRoot, layout, fileNamesAreUtc))
            {
                _ = restartRecording(session.CameraId);
            }
        }

        return Reorganize(recordingRoot, layout, videoExtensions, fileNamesAreUtc);
    }

    /// <summary>
    /// Determines whether a recording and its thumbnail already are where
    /// <paramref name="layout"/> puts them. A file outside the root, or not
    /// in a camera folder, is not this layout's concern and counts as in place.
    /// </summary>
    /// <param name="videoPath">The recording path.</param>
    /// <param name="recordingRoot">The recording root.</param>
    /// <param name="layout">The layout to check against.</param>
    /// <param name="fileNamesAreUtc">Whether file-name timestamps are UTC.</param>
    public static bool IsInLayout(
        string videoPath,
        string recordingRoot,
        RecordingLayoutOptions layout,
        bool fileNamesAreUtc = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);
        ArgumentException.ThrowIfNullOrEmpty(recordingRoot);
        ArgumentNullException.ThrowIfNull(layout);

        var root = Path.GetFullPath(recordingRoot);
        var cameraFolder = RecordingPathHelper.GetCameraFolderFromPath(videoPath, root);
        if (cameraFolder is null ||
            cameraFolder.Equals(RecordingPathHelper.ThumbnailsFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var targetVideo = GetTargetVideoPath(videoPath, root, cameraFolder, layout, fileNamesAreUtc);
        if (!PathEquals(videoPath, targetVideo))
        {
            return false;
        }

        var thumbnail = RecordingPathHelper.FindThumbnailPath(videoPath, root);
        return thumbnail is null ||
               PathEquals(thumbnail, RecordingPathHelper.BuildThumbnailPath(targetVideo, root, layout));
    }

    private static List<string> EnumerateRecordings(
        string root,
        IReadOnlySet<string> extensions,
        List<MediaCleanupRunError> errors)
    {
        var recordings = new List<string>();

        try
        {
            // Only camera folders hold recordings; the thumbnails folder and
            // files directly in the root are not the recorder's to move.
            foreach (var cameraFolder in Directory.EnumerateDirectories(root))
            {
                if (Path.GetFileName(cameraFolder).Equals(RecordingPathHelper.ThumbnailsFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                recordings.AddRange(Directory
                    .EnumerateFiles(cameraFolder, "*.*", SearchOption.AllDirectories)
                    .Where(f => extensions.Contains(Path.GetExtension(f))));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(new MediaCleanupRunError(root, ex));
        }

        return recordings;
    }

    private static string GetTargetVideoPath(
        string video,
        string root,
        string cameraFolder,
        RecordingLayoutOptions layout,
        bool fileNamesAreUtc)
    {
        var folder = Path.Combine(root, cameraFolder);
        if (layout.UseDailySubfolders)
        {
            folder = Path.Combine(folder, GetDayFolderName(video, fileNamesAreUtc));
        }

        return Path.Combine(folder, Path.GetFileName(video));
    }

    private static string GetDayFolderName(
        string video,
        bool fileNamesAreUtc)
    {
        // A recording that already sits in a day folder keeps its day.
        var currentFolder = Path.GetFileName(Path.GetDirectoryName(video)) ?? string.Empty;
        if (DateOnly.TryParseExact(currentFolder, RecordingPathHelper.DayFolderFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return currentFolder;
        }

        DateTime day;
        if (RecordingPathHelper.TryParseTimestamp(video, out var timestamp))
        {
            day = fileNamesAreUtc
                ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc).ToLocalTime()
                : timestamp;
        }
        else
        {
            day = File.GetLastWriteTime(video);
        }

        return day.ToString(RecordingPathHelper.DayFolderFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Moves a file. Returns <see langword="true"/> when the file is at
    /// <paramref name="target"/> afterwards, also when it already was.
    /// </summary>
    private static bool Move(
        string source,
        string target,
        HashSet<string> emptiedFolders,
        List<MediaCleanupRunError> errors)
    {
        if (PathEquals(source, target))
        {
            return true;
        }

        if (File.Exists(target))
        {
            errors.Add(new MediaCleanupRunError(source, new IOException($"'{target}' already exists.")));
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(source, target);
            emptiedFolders.Add(Path.GetDirectoryName(source)!);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(new MediaCleanupRunError(source, ex));
            return false;
        }
    }

    /// <summary>
    /// Removes the folders files were moved out of when that left them
    /// empty, and their parents up to (not including) the root.
    /// </summary>
    private static void RemoveEmptied(
        HashSet<string> emptiedFolders,
        string root,
        List<MediaCleanupRunError> errors)
    {
        foreach (var start in emptiedFolders.OrderByDescending(f => f.Length))
        {
            var folder = start;
            while (!string.IsNullOrEmpty(folder) && !PathEquals(folder, root))
            {
                try
                {
                    if (!Directory.Exists(folder) || Directory.EnumerateFileSystemEntries(folder).Any())
                    {
                        break;
                    }

                    Directory.Delete(folder);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    errors.Add(new MediaCleanupRunError(folder, ex));
                    break;
                }

                folder = Path.GetDirectoryName(folder);
            }
        }
    }

    private static bool PathEquals(
        string left,
        string right)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
}