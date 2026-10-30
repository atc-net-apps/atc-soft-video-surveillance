namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Pure file-system cleanup pass shared by the WPF and server media-cleanup
/// services. No timers, no logging, no DI — input is a directory + retention
/// rules + active-path skip set; output is counts, bytes freed, and the list
/// of deleted files (for the caller to log).
/// </summary>
public static class MediaCleanupRunner
{
    /// <summary>
    /// How old an empty folder must be before a cleanup pass removes it. A
    /// recording creates its folder a moment before its file, and cleanup can
    /// run at any time.
    /// </summary>
    public static readonly TimeSpan EmptyDirectoryGracePeriod = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Deletes files in <paramref name="rootPath"/> (recursive) whose
    /// extension is in <paramref name="extensions"/> and whose last-write
    /// time is strictly before <paramref name="cutoff"/>, except those whose
    /// normalized full path is in <paramref name="skipPaths"/>.
    /// </summary>
    /// <param name="rootPath">Directory to scan recursively.</param>
    /// <param name="extensions">
    /// Lowercase file extensions including the leading dot, e.g. <c>.mkv</c>.
    /// </param>
    /// <param name="cutoff">Files older than this are eligible for deletion.</param>
    /// <param name="skipPaths">
    /// Case-insensitive set of full paths to leave untouched (active recordings
    /// and their companions: thumbnails and activity indexes).
    /// </param>
    /// <param name="deleteCompanionThumbnail">
    /// When <c>true</c>, deleting a media file also deletes its companions: its <c>.png</c>
    /// thumbnail, whether it sits next to the file or in the separate
    /// thumbnails folder below <paramref name="rootPath"/>, and its activity index.
    /// </param>
    /// <param name="nestedRoots">
    /// Camera recording folders inside <paramref name="rootPath"/>: a recording in one of them
    /// has its thumbnail in that folder's own thumbnails folder.
    /// </param>
    public static MediaCleanupRunResult CleanDirectory(
        string rootPath,
        IReadOnlyCollection<string> extensions,
        DateTime cutoff,
        IReadOnlySet<string> skipPaths,
        bool deleteCompanionThumbnail,
        IReadOnlyCollection<string>? nestedRoots = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootPath);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(skipPaths);

        var extensionSet = extensions as IReadOnlySet<string>
            ?? new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);

        var deleted = new List<string>();
        var deletedThumbnails = new List<string>();
        var errors = new List<MediaCleanupRunError>();
        long bytesFreed = 0;

        if (!Directory.Exists(rootPath))
        {
            return new MediaCleanupRunResult(deleted, deletedThumbnails, bytesFreed, errors);
        }

        IEnumerable<string> files;
        try
        {
            files = Directory
                .EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                .Where(f => extensionSet.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            errors.Add(new MediaCleanupRunError(rootPath, ex));
            return new MediaCleanupRunResult(deleted, deletedThumbnails, bytesFreed, errors);
        }
        catch (IOException ex)
        {
            errors.Add(new MediaCleanupRunError(rootPath, ex));
            return new MediaCleanupRunResult(deleted, deletedThumbnails, bytesFreed, errors);
        }

        foreach (var file in files)
        {
            try
            {
                if (skipPaths.Contains(Path.GetFullPath(file)))
                {
                    continue;
                }

                var info = new FileInfo(file);
                if (info.LastWriteTime >= cutoff)
                {
                    continue;
                }

                var fileSize = info.Length;
                info.Delete();
                deleted.Add(file);
                bytesFreed += fileSize;

                if (deleteCompanionThumbnail)
                {
                    var thumbnailRoot = RecordingRoots.Innermost(file, [rootPath, .. nestedRoots ?? []]) ?? rootPath;
                    bytesFreed += DeleteCompanions(file, thumbnailRoot, deletedThumbnails);
                }
            }
            catch (IOException ex)
            {
                errors.Add(new MediaCleanupRunError(file, ex));
            }
            catch (UnauthorizedAccessException ex)
            {
                errors.Add(new MediaCleanupRunError(file, ex));
            }
        }

        return new MediaCleanupRunResult(deleted, deletedThumbnails, bytesFreed, errors);
    }

    /// <summary>
    /// Deletes files in <paramref name="rootPath"/> (recursive), oldest first
    /// by <c>LastWriteTime</c>, until the recording drive has
    /// at least <paramref name="targetFreeBytes"/> of free space, or all
    /// eligible files have been exhausted.
    /// </summary>
    /// <param name="rootPath">Directory to scan recursively.</param>
    /// <param name="extensions">
    /// Lowercase file extensions including the leading dot, e.g. <c>.mkv</c>.
    /// </param>
    /// <param name="targetFreeBytes">
    /// Desired minimum free bytes on the drive that hosts
    /// <paramref name="rootPath"/>. When the drive already has this much free
    /// space the method returns immediately with an empty result.
    /// </param>
    /// <param name="skipPaths">
    /// Case-insensitive set of full paths to leave untouched (active recordings
    /// and their companions: thumbnails and activity indexes).
    /// </param>
    /// <param name="deleteCompanionThumbnail">
    /// When <c>true</c>, deleting a media file also deletes its companions: its <c>.png</c>
    /// thumbnail, whether it sits next to the file or in the separate
    /// thumbnails folder below <paramref name="rootPath"/>, and its activity index.
    /// </param>
    /// <returns>
    /// A <see cref="MediaCleanupRunResult"/> describing the deleted files and
    /// bytes freed. Check <see cref="MediaCleanupRunResult.StillShort"/> to
    /// determine whether the target free-space goal was reached.
    /// </returns>
    public static MediaCleanupRunResult ReclaimBySize(
        string rootPath,
        IReadOnlyCollection<string> extensions,
        long targetFreeBytes,
        IReadOnlySet<string> skipPaths,
        bool deleteCompanionThumbnail)
        => ReclaimBySize(
            rootPath,
            extensions,
            targetFreeBytes,
            skipPaths,
            deleteCompanionThumbnail,
            driveRoot => new DriveInfo(driveRoot).AvailableFreeSpace);

    // The free space is read through a function so that tests can give it:
    // the real disk's changes with everything else the machine does.
    internal static MediaCleanupRunResult ReclaimBySize(
        string rootPath,
        IReadOnlyCollection<string> extensions,
        long targetFreeBytes,
        IReadOnlySet<string> skipPaths,
        bool deleteCompanionThumbnail,
        Func<string, long> readFreeBytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootPath);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(skipPaths);
        ArgumentNullException.ThrowIfNull(readFreeBytes);

        var deleted = new List<string>();
        var deletedThumbnails = new List<string>();
        var errors = new List<MediaCleanupRunError>();
        long bytesFreed = 0;

        if (!Directory.Exists(rootPath))
        {
            return new MediaCleanupRunResult(deleted, deletedThumbnails, bytesFreed, errors);
        }

        // Determine drive and check current free space
        var driveRoot = Path.GetPathRoot(Path.GetFullPath(rootPath));
        if (string.IsNullOrEmpty(driveRoot))
        {
            return new MediaCleanupRunResult(deleted, deletedThumbnails, bytesFreed, errors);
        }

        long initialFreeBytes;
        try
        {
            initialFreeBytes = readFreeBytes(driveRoot);
        }
        catch (Exception ex)
        {
            errors.Add(new MediaCleanupRunError(rootPath, ex));
            return new MediaCleanupRunResult(deleted, deletedThumbnails, bytesFreed, errors);
        }

        if (initialFreeBytes >= targetFreeBytes)
        {
            return new MediaCleanupRunResult(deleted, deletedThumbnails, bytesFreed, errors);
        }

        var neededBytes = targetFreeBytes - initialFreeBytes;

        var extensionSet = extensions as IReadOnlySet<string>
            ?? new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);

        IList<FileInfo> files;
        try
        {
            files = Directory
                .EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                .Where(f => extensionSet.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Where(f => !skipPaths.Contains(Path.GetFullPath(f)))
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.LastWriteTime)
                .ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            errors.Add(new MediaCleanupRunError(rootPath, ex));
            return new MediaCleanupRunResult(deleted, deletedThumbnails, bytesFreed, errors) { StillShort = true };
        }
        catch (IOException ex)
        {
            errors.Add(new MediaCleanupRunError(rootPath, ex));
            return new MediaCleanupRunResult(deleted, deletedThumbnails, bytesFreed, errors) { StillShort = true };
        }

        foreach (var file in files)
        {
            if (bytesFreed >= neededBytes)
            {
                break;
            }

            try
            {
                var fileSize = file.Length;
                file.Delete();
                deleted.Add(file.FullName);
                bytesFreed += fileSize;

                if (deleteCompanionThumbnail)
                {
                    bytesFreed += DeleteCompanions(file.FullName, rootPath, deletedThumbnails);
                }
            }
            catch (IOException ex)
            {
                errors.Add(new MediaCleanupRunError(file.FullName, ex));
            }
            catch (UnauthorizedAccessException ex)
            {
                errors.Add(new MediaCleanupRunError(file.FullName, ex));
            }
        }

        // Deleting the last recording of a day (or of a camera) leaves an
        // empty folder behind. Reclaim can run while a recording is starting,
        // so folders created a moment ago are left alone.
        if (deleted.Count > 0)
        {
            errors.AddRange(RemoveEmptyDirectoriesBelow(rootPath, EmptyDirectoryGracePeriod).Errors);
        }

        return new MediaCleanupRunResult(deleted, deletedThumbnails, bytesFreed, errors)
        {
            StillShort = bytesFreed < neededBytes,
        };
    }

    /// <summary>
    /// Removes every empty directory under <paramref name="rootPath"/>,
    /// deepest-first. The root itself is left in place. Failures are
    /// returned in the result; never thrown.
    /// </summary>
    /// <param name="rootPath">The folder whose empty sub-folders are removed. The folder itself is kept.</param>
    /// <param name="minimumAge">
    /// Leaves folders younger than this alone. A recording creates its folder
    /// a moment before its file, so a pass that can run at any time must not
    /// remove a folder that is about to be written to.
    /// </param>
    public static MediaCleanupDirectoryResult RemoveEmptyDirectoriesBelow(
        string rootPath,
        TimeSpan? minimumAge = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootPath);

        var newestAllowedUtc = minimumAge is { } age
            ? DateTime.UtcNow - age
            : DateTime.MaxValue;

        var removed = new List<string>();
        var errors = new List<MediaCleanupRunError>();

        if (!Directory.Exists(rootPath))
        {
            return new MediaCleanupDirectoryResult(removed, errors);
        }

        IEnumerable<string> dirs;
        try
        {
            dirs = Directory
                .EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Length)
                .ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            errors.Add(new MediaCleanupRunError(rootPath, ex));
            return new MediaCleanupDirectoryResult(removed, errors);
        }
        catch (IOException ex)
        {
            errors.Add(new MediaCleanupRunError(rootPath, ex));
            return new MediaCleanupDirectoryResult(removed, errors);
        }

        foreach (var dir in dirs)
        {
            try
            {
                if (string.Equals(dir, rootPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    continue;
                }

                if (Directory.GetCreationTimeUtc(dir) > newestAllowedUtc)
                {
                    continue;
                }

                Directory.Delete(dir);
                removed.Add(dir);
            }
            catch (IOException ex)
            {
                errors.Add(new MediaCleanupRunError(dir, ex));
            }
            catch (UnauthorizedAccessException ex)
            {
                errors.Add(new MediaCleanupRunError(dir, ex));
            }
        }

        return new MediaCleanupDirectoryResult(removed, errors);
    }

    /// <summary>
    /// Deletes recording thumbnails and activity indexes whose recording no
    /// longer exists, for example because the video was deleted by hand.
    /// Looks in the camera folders (thumbnail and index next to the video)
    /// and in the separate thumbnails folder. Files directly in
    /// <paramref name="rootPath"/> are left alone. Both kinds are reported in
    /// <see cref="MediaCleanupRunResult.DeletedThumbnails"/>.
    /// Failures are returned in the result; never thrown.
    /// </summary>
    /// <param name="rootPath">The recording root.</param>
    /// <param name="videoExtensions">The recording file extensions, with a leading dot.</param>
    /// <param name="skipPaths">Case-insensitive set of full paths to leave untouched.</param>
    /// <param name="minimumAge">
    /// Leaves thumbnails younger than this alone, so a pass cannot race a
    /// recording that has just started.
    /// </param>
    /// <param name="excludedDirectories">
    /// Folders below the root that do not belong to the recorder, such as a
    /// snapshot folder placed inside the recording folder.
    /// </param>
    /// <param name="nestedRoots">
    /// Camera recording folders inside <paramref name="rootPath"/>: a thumbnail in one of them is
    /// read against that folder, whose own thumbnails folder mirrors its videos.
    /// </param>
    public static MediaCleanupRunResult RemoveOrphanThumbnails(
        string rootPath,
        IReadOnlyCollection<string> videoExtensions,
        IReadOnlySet<string> skipPaths,
        TimeSpan? minimumAge = null,
        IReadOnlyCollection<string>? excludedDirectories = null,
        IReadOnlyCollection<string>? nestedRoots = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootPath);
        ArgumentNullException.ThrowIfNull(videoExtensions);
        ArgumentNullException.ThrowIfNull(skipPaths);

        var deletedThumbnails = new List<string>();
        var errors = new List<MediaCleanupRunError>();
        long bytesFreed = 0;

        if (!Directory.Exists(rootPath))
        {
            return new MediaCleanupRunResult([], deletedThumbnails, bytesFreed, errors);
        }

        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        string[] roots = [fullRoot, .. nestedRoots ?? []];
        var excluded = (excludedDirectories ?? [])
            .Where(d => !string.IsNullOrEmpty(d))
            .Select(d => Path.TrimEndingDirectorySeparator(Path.GetFullPath(d)) + Path.DirectorySeparatorChar)
            .ToList();
        var newestAllowedUtc = minimumAge is { } age
            ? DateTime.UtcNow - age
            : DateTime.MaxValue;

        List<string> thumbnails;
        try
        {
            thumbnails = Directory
                .EnumerateFiles(fullRoot, "*.png", SearchOption.AllDirectories)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(new MediaCleanupRunError(rootPath, ex));
            return new MediaCleanupRunResult([], deletedThumbnails, bytesFreed, errors);
        }

        foreach (var thumbnail in thumbnails)
        {
            try
            {
                if (skipPaths.Contains(thumbnail) ||
                    excluded.Exists(e => thumbnail.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var thumbnailRoot = RecordingRoots.Innermost(thumbnail, roots) ?? fullRoot;
                var videoFolder = GetVideoFolderOfThumbnail(
                    thumbnail,
                    thumbnailRoot,
                    Path.Combine(thumbnailRoot, RecordingPathHelper.ThumbnailsFolderName));
                if (videoFolder is null)
                {
                    continue;
                }

                var stem = Path.GetFileNameWithoutExtension(thumbnail);
                if (videoExtensions.Any(extension => File.Exists(Path.Combine(videoFolder, stem + extension))))
                {
                    continue;
                }

                bytesFreed += DeleteIfOlder(thumbnail, newestAllowedUtc, deletedThumbnails);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(new MediaCleanupRunError(thumbnail, ex));
            }
        }

        foreach (var ending in new[] { RecordingPathHelper.ActivityExtension, RecordingPathHelper.PreviewImageExtension, RecordingPathHelper.PreviewIndexExtension, RecordingPathHelper.ReviewExtension })
        {
            bytesFreed += RemoveOrphanSidecars(fullRoot, ending, videoExtensions, skipPaths, newestAllowedUtc, excluded, deletedThumbnails, errors);
        }

        return new MediaCleanupRunResult([], deletedThumbnails, bytesFreed, errors);
    }

    // An activity index, the previews and a review always sit beside their video, in every layout.
    private static long RemoveOrphanSidecars(
        string fullRoot,
        string ending,
        IReadOnlyCollection<string> videoExtensions,
        IReadOnlySet<string> skipPaths,
        DateTime newestAllowedUtc,
        List<string> excluded,
        List<string> deleted,
        List<MediaCleanupRunError> errors)
    {
        List<string> indexes;
        try
        {
            indexes = Directory
                .EnumerateFiles(fullRoot, "*" + ending, SearchOption.AllDirectories)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(new MediaCleanupRunError(fullRoot, ex));
            return 0;
        }

        long bytesFreed = 0;
        foreach (var index in indexes)
        {
            try
            {
                var folder = Path.GetDirectoryName(index);
                if (folder is null ||
                    folder.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) ||
                    skipPaths.Contains(index) ||
                    excluded.Exists(e => index.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var name = Path.GetFileName(index);
                var stem = name[..^ending.Length];
                if (videoExtensions.Any(extension => File.Exists(Path.Combine(folder, stem + extension))))
                {
                    continue;
                }

                bytesFreed += DeleteIfOlder(index, newestAllowedUtc, deleted);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(new MediaCleanupRunError(index, ex));
            }
        }

        return bytesFreed;
    }

    private static long DeleteIfOlder(
        string path,
        DateTime newestAllowedUtc,
        List<string> deleted)
    {
        var info = new FileInfo(path);
        if (info.LastWriteTimeUtc > newestAllowedUtc)
        {
            return 0;
        }

        var size = info.Length;
        info.Delete();
        deleted.Add(path);
        return size;
    }

    /// <summary>
    /// Gets the folder the recording of a thumbnail would be in, or
    /// <see langword="null"/> when the file is not in a place the recorder
    /// writes thumbnails to.
    /// </summary>
    private static string? GetVideoFolderOfThumbnail(
        string thumbnailPath,
        string fullRoot,
        string thumbnailsRoot)
    {
        var folder = Path.GetDirectoryName(thumbnailPath);
        if (folder is null || folder.Equals(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!RecordingPathHelper.IsInThumbnailsFolder(thumbnailPath, fullRoot))
        {
            // Flat layout: the thumbnail sits next to its recording.
            return folder;
        }

        // Separate layout: the thumbnails folder mirrors the video tree,
        // thumbnails\{camera}\... below the root.
        var relativeFolder = Path.GetRelativePath(thumbnailsRoot, folder);
        return relativeFolder == "."
            ? null
            : Path.Combine(fullRoot, relativeFolder);
    }

    /// <summary>
    /// Deletes the companions of a deleted media file: its thumbnail from every place a
    /// layout may have put it, and its activity index. Returns the bytes freed.
    /// </summary>
    private static long DeleteCompanions(
        string mediaFilePath,
        string rootPath,
        List<string> deletedThumbnails)
    {
        long bytesFreed = 0;

        foreach (var thumbnailPath in RecordingPathHelper.GetCompanionPaths(mediaFilePath, rootPath))
        {
            if (!File.Exists(thumbnailPath))
            {
                continue;
            }

            var thumbInfo = new FileInfo(thumbnailPath);
            var thumbSize = thumbInfo.Length;
            thumbInfo.Delete();
            deletedThumbnails.Add(thumbnailPath);
            bytesFreed += thumbSize;
        }

        return bytesFreed;
    }
}