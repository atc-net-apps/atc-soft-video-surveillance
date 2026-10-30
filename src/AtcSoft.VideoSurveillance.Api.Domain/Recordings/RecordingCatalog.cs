namespace AtcSoft.VideoSurveillance.Api.Domain.Recordings;

/// <summary>
/// The recordings the server has: the ones in progress and the files below the recordings root,
/// each with its camera. The one walk of the recordings folder, shared by the listing and the
/// timeline.
/// </summary>
public static class RecordingCatalog
{
    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".avi"];

    /// <summary>
    /// Finds the recordings.
    /// </summary>
    /// <param name="recordingPath">The recordings root.</param>
    /// <param name="cameras">The cameras, to tell which folder belongs to which.</param>
    /// <param name="sessions">The recordings in progress.</param>
    /// <param name="cameraId">Only this camera's recordings; all when <see langword="null"/>.</param>
    /// <param name="now">The time a recording in progress has reached.</param>
    /// <returns>The recordings in progress first, then the files, in no particular order.</returns>
    public static IReadOnlyList<RecordingFile> Find(
        string recordingPath,
        IReadOnlyList<AtcSoft.VideoSurveillance.Models.CameraConfiguration> cameras,
        IReadOnlyList<AtcSoft.VideoSurveillance.Models.RecordingSession> sessions,
        Guid? cameraId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(cameras);
        ArgumentNullException.ThrowIfNull(sessions);

        var cameraNames = new Dictionary<Guid, string>();
        foreach (var camera in cameras)
        {
            cameraNames.TryAdd(camera.Id, camera.Display.DisplayName);
        }

        var found = sessions
            .Where(s => cameraId is null || s.CameraId == cameraId)
            .Select(s => new RecordingFile(
                s.CameraId,
                cameraNames.GetValueOrDefault(s.CameraId, string.Empty),
                s.CurrentFilePath,
                ToRelativePath(s.CurrentFilePath, recordingPath),
                new DateTimeOffset(DateTime.SpecifyKind(s.StartTime, DateTimeKind.Utc)),
                now,
                Length: 0,
                InProgress: true))
            .ToList();

        if (string.IsNullOrEmpty(recordingPath) || !Directory.Exists(recordingPath))
        {
            return found;
        }

        var activeFilePaths = new HashSet<string>(sessions.Select(s => s.CurrentFilePath), StringComparer.OrdinalIgnoreCase);

        // Files are stored under {RecordingPath}/{camera folder}/, directly or in a folder per
        // day, so the camera is the first folder below the root, whatever the layout. Two cameras
        // can share a folder name (same display name); the first one wins rather than the whole
        // listing failing.
        var camerasByFolderName = new Dictionary<string, AtcSoft.VideoSurveillance.Models.CameraConfiguration>(StringComparer.OrdinalIgnoreCase);
        foreach (var camera in cameras)
        {
            camerasByFolderName.TryAdd(RecordingPathHelper.GetCameraFolderName(camera.Display.DisplayName, camera.Id), camera);
        }

        var files = Directory.EnumerateFiles(recordingPath, "*.*", SearchOption.AllDirectories)
            .Where(f => VideoExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !RecordingPathHelper.IsInThumbnailsFolder(f, recordingPath))
            .Where(f => !activeFilePaths.Contains(f));

        foreach (var file in files)
        {
            var folderName = RecordingPathHelper.GetCameraFolderFromPath(file, recordingPath) ?? string.Empty;
            var matched = camerasByFolderName.GetValueOrDefault(folderName);
            if (cameraId is not null && matched?.Id != cameraId)
            {
                continue;
            }

            var info = new FileInfo(file);

            // The name carries the start time (in UTC on the server); the file's creation time
            // changes when it is moved or copied.
            var startedAt = RecordingPathHelper.TryParseTimestamp(info.Name, out var timestamp)
                ? new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Unspecified), TimeSpan.Zero)
                : new DateTimeOffset(info.CreationTimeUtc, TimeSpan.Zero);

            found.Add(new RecordingFile(
                matched?.Id ?? Guid.Empty,
                matched?.Display.DisplayName ?? string.Empty,
                file,
                ToRelativePath(file, recordingPath),
                startedAt,
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                info.Length,
                InProgress: false));
        }

        return found;
    }

    /// <summary>
    /// Gets a path the way clients use it: relative to the recording root, with forward slashes.
    /// A file outside the root keeps its full path.
    /// </summary>
    /// <param name="filePath">The file.</param>
    /// <param name="recordingPath">The recordings root.</param>
    public static string ToRelativePath(
        string filePath,
        string recordingPath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (string.IsNullOrEmpty(recordingPath) || !Path.IsPathRooted(filePath))
        {
            return filePath;
        }

        var relative = Path.GetRelativePath(recordingPath, filePath);
        if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
        {
            return filePath;
        }

        return relative.Replace('\\', '/');
    }
}