namespace AtcSoft.Video.Surveillance.Api.Domain.Recordings;

/// <summary>
/// The recordings the server has: the ones in progress and the files below the recordings root,
/// each with its camera. The one walk of the recordings folder, shared by the listing and the
/// timeline.
/// </summary>
public static class RecordingCatalog
{
    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".avi"];

    // Hidden and system files too, as Directory.EnumerateFiles finds them.
    private static readonly EnumerationOptions TopLevel = new() { AttributesToSkip = default };

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
        IReadOnlyList<AtcSoft.Video.Surveillance.Models.CameraConfiguration> cameras,
        IReadOnlyList<AtcSoft.Video.Surveillance.Models.RecordingSession> sessions,
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
        var camerasByFolderName = new Dictionary<string, AtcSoft.Video.Surveillance.Models.CameraConfiguration>(StringComparer.OrdinalIgnoreCase);
        foreach (var camera in cameras)
        {
            camerasByFolderName.TryAdd(RecordingPathHelper.GetCameraFolderName(camera.Display.DisplayName, camera.Id), camera);
        }

        var files = EnumerateVideos(new DirectoryInfo(recordingPath), cameraId, camerasByFolderName)
            .Where(f => !activeFilePaths.Contains(f.FullName));

        foreach (var info in files)
        {
            var file = info.FullName;
            var folderName = RecordingPathHelper.GetCameraFolderFromPath(file, recordingPath) ?? string.Empty;
            var matched = camerasByFolderName.GetValueOrDefault(folderName);
            if (cameraId is not null && matched?.Id != cameraId)
            {
                continue;
            }

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

    // The videos below the root, with their size and times as the walk read them (no second
    // look at each file). One camera's recordings are all in its own folder, so only that
    // folder is walked for it; the separate thumbnails folder holds no recording.
    private static IEnumerable<FileInfo> EnumerateVideos(
        DirectoryInfo root,
        Guid? cameraId,
        Dictionary<string, AtcSoft.Video.Surveillance.Models.CameraConfiguration> camerasByFolderName)
    {
        var found = new List<FileInfo>();
        if (cameraId is not null)
        {
            var folder = camerasByFolderName.FirstOrDefault(c => c.Value.Id == cameraId).Key;
            if (folder is not null)
            {
                Walk(new DirectoryInfo(Path.Combine(root.FullName, folder)), found);
            }
        }
        else
        {
            AddVideos(root, found);
            foreach (var directory in SubDirectories(root)
                         .Where(d => !d.Name.Equals(RecordingPathHelper.ThumbnailsFolderName, StringComparison.OrdinalIgnoreCase)))
            {
                Walk(directory, found);
            }
        }

        return found;
    }

    // Folder by folder: one that is removed, or cannot be read, while the walk runs (a day folder
    // the clean-up deletes) is left out, and never fails the whole listing.
    private static void Walk(
        DirectoryInfo folder,
        List<FileInfo> found)
    {
        var stack = new Stack<DirectoryInfo>();
        stack.Push(folder);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            AddVideos(current, found);
            foreach (var sub in SubDirectories(current))
            {
                stack.Push(sub);
            }
        }
    }

    private static void AddVideos(
        DirectoryInfo folder,
        List<FileInfo> found)
    {
        try
        {
            found.AddRange(folder
                .EnumerateFiles("*", TopLevel)
                .Where(f => VideoExtensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Gone or unreadable meanwhile.
        }
    }

    private static List<DirectoryInfo> SubDirectories(DirectoryInfo folder)
    {
        try
        {
            return [.. folder.EnumerateDirectories("*", TopLevel)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
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