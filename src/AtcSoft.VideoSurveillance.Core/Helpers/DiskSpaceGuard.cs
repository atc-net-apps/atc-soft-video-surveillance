namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Keeps free space on every volume that holds media: the global recording folder, each
/// camera's own folder and the snapshot folder (with the time-lapse pictures). On a volume that
/// is short it deletes the oldest media first, across all folders on that volume, until the
/// volume has the space back or nothing more may be deleted. Recordings being written are never
/// deleted. One instance serves a whole app: two passes at the same moment would each delete
/// what the other already freed, so they take turns.
/// </summary>
public sealed class DiskSpaceGuard
{
    private readonly IFreeSpaceReader reader;
    private readonly Lock gate = new();
    private readonly Dictionary<string, DiskSpaceVolumeState> lastStates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskSpaceGuard"/> class.
    /// </summary>
    /// <param name="reader">Reads the volumes and their free space.</param>
    public DiskSpaceGuard(IFreeSpaceReader reader)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    /// <summary>
    /// Gets the reader the guard measures with, for a status that must agree with the guard.
    /// </summary>
    public IFreeSpaceReader Reader => reader;

    /// <summary>
    /// Gets the paths never to delete: the recordings being written and every place their
    /// thumbnail may be, taken from the innermost recording folder that holds each recording.
    /// </summary>
    /// <param name="activeFiles">The recordings being written.</param>
    /// <param name="recordingFolders">Every recording folder, nested ones included.</param>
    /// <returns>A case-insensitive set of full paths.</returns>
    public static IReadOnlySet<string> ProtectedPaths(
        IEnumerable<string> activeFiles,
        IReadOnlyList<string> recordingFolders)
    {
        ArgumentNullException.ThrowIfNull(activeFiles);
        ArgumentNullException.ThrowIfNull(recordingFolders);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in activeFiles.Where(f => !string.IsNullOrEmpty(f)))
        {
            set.Add(Path.GetFullPath(file));
            foreach (var companion in RecordingPathHelper.GetCompanionPaths(file, RecordingRoots.Innermost(file, recordingFolders)))
            {
                set.Add(Path.GetFullPath(companion));
            }
        }

        return set;
    }

    /// <summary>
    /// Checks every volume that holds media and reclaims space on those that are short.
    /// </summary>
    /// <param name="request">What to keep free, and where.</param>
    /// <returns>What was found and done on each volume.</returns>
    public DiskSpaceGuardResult Enforce(DiskSpaceGuardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (gate)
        {
            var recordingRoots = RecordingRoots.Collect(null, request.RecordingFolders);
            var snapshotRoot = string.IsNullOrWhiteSpace(request.SnapshotFolder)
                ? null
                : Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.SnapshotFolder));

            var groups = recordingRoots
                .Concat(snapshotRoot is null ? [] : [snapshotRoot])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .GroupBy(folder => reader.GetVolume(folder) ?? folder, StringComparer.OrdinalIgnoreCase);

            var volumes = new List<DiskSpaceVolumeResult>();
            foreach (var group in groups)
            {
                var volume = EnforceVolume(group.Key, [.. group], recordingRoots, snapshotRoot, request);
                var changed = !lastStates.TryGetValue(volume.Volume, out var last) || last != volume.State;
                lastStates[volume.Volume] = volume.State;
                volumes.Add(volume with { StateChanged = changed });
            }

            return new DiskSpaceGuardResult(volumes);
        }
    }

    /// <summary>
    /// Keeps the recordings of all folders together within a size, deleting the oldest first
    /// with their thumbnails. Recordings being written count toward the size but stay.
    /// </summary>
    /// <param name="recordingFolders">Every recording folder, nested ones included.</param>
    /// <param name="maxBytes">The size to keep within; 0 or less is no limit.</param>
    /// <param name="protectedPaths">Full paths never to delete (<see cref="ProtectedPaths"/>).</param>
    /// <param name="recordingExtensions">The recording file extensions, with a leading dot.</param>
    /// <returns>What was deleted.</returns>
    public MediaCleanupRunResult EnforceSizeLimit(
        IReadOnlyList<string> recordingFolders,
        long maxBytes,
        IReadOnlySet<string> protectedPaths,
        IReadOnlyCollection<string> recordingExtensions)
    {
        ArgumentNullException.ThrowIfNull(recordingFolders);
        ArgumentNullException.ThrowIfNull(protectedPaths);
        ArgumentNullException.ThrowIfNull(recordingExtensions);

        if (maxBytes <= 0)
        {
            return Empty();
        }

        lock (gate)
        {
            var errors = new List<MediaCleanupRunError>();
            var extensions = new HashSet<string>(recordingExtensions, StringComparer.OrdinalIgnoreCase);
            var recordings = new List<FileInfo>();
            foreach (var folder in RecordingRoots.Collect(null, recordingFolders).Where(Directory.Exists))
            {
                try
                {
                    recordings.AddRange(Directory
                        .EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                        .Where(f => extensions.Contains(Path.GetExtension(f)))
                        .Select(f => new FileInfo(f)));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    errors.Add(new MediaCleanupRunError(folder, ex));
                }
            }

            var total = recordings.Sum(f => f.Length);
            var deleted = new List<string>();
            var deletedThumbnails = new List<string>();
            long freed = 0;
            var request = new DiskSpaceGuardRequest(recordingFolders, null, 0, protectedPaths, recordingExtensions, []);
            foreach (var file in recordings.Where(f => !protectedPaths.Contains(f.FullName)).OrderBy(f => f.LastWriteTimeUtc))
            {
                if (total - freed <= maxBytes)
                {
                    break;
                }

                try
                {
                    var size = file.Length;
                    file.Delete();
                    deleted.Add(file.FullName);
                    freed += size;
                    DeleteCompanions(file.FullName, request, deletedThumbnails, errors);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    errors.Add(new MediaCleanupRunError(file.FullName, ex));
                }
            }

            return new MediaCleanupRunResult(deleted, deletedThumbnails, freed, errors) { StillShort = total - freed > maxBytes };
        }
    }

    private DiskSpaceVolumeResult EnforceVolume(
        string volume,
        IReadOnlyList<string> folders,
        IReadOnlyList<string> recordingRoots,
        string? snapshotRoot,
        DiskSpaceGuardRequest request)
    {
        var free = reader.GetFreeBytes(volume);
        if (free is null)
        {
            return new DiskSpaceVolumeResult(volume, folders, DiskSpaceVolumeState.Unreadable, null, Empty());
        }

        if (free.Value >= request.MinFreeBytes)
        {
            return new DiskSpaceVolumeResult(volume, folders, DiskSpaceVolumeState.Ok, free, Empty());
        }

        var needed = request.MinFreeBytes - free.Value;
        var errors = new List<MediaCleanupRunError>();
        var candidates = Candidates(folders, recordingRoots, snapshotRoot, request, errors);

        var deleted = new List<string>();
        var deletedThumbnails = new List<string>();
        long freed = 0;
        foreach (var (file, isRecording) in candidates)
        {
            if (freed >= needed)
            {
                break;
            }

            try
            {
                var size = file.Length;
                file.Delete();
                deleted.Add(file.FullName);
                freed += size;
                if (isRecording)
                {
                    freed += DeleteCompanions(file.FullName, request, deletedThumbnails, errors);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(new MediaCleanupRunError(file.FullName, ex));
            }
        }

        // Deleting the last recording of a day leaves its folder empty. A folder made a moment
        // ago may be about to get a recording, so young ones stay.
        if (deleted.Count > 0)
        {
            foreach (var folder in folders)
            {
                errors.AddRange(MediaCleanupRunner.RemoveEmptyDirectoriesBelow(folder, MediaCleanupRunner.EmptyDirectoryGracePeriod).Errors);
            }
        }

        var run = new MediaCleanupRunResult(deleted, deletedThumbnails, freed, errors) { StillShort = freed < needed };
        var state = run.StillShort ? DiskSpaceVolumeState.StillShort : DiskSpaceVolumeState.Reclaimed;
        return new DiskSpaceVolumeResult(volume, folders, state, free, run);
    }

    // The media on a volume that may go, oldest first: the recordings in its recording folders
    // and the pictures in its snapshot folder that are not inside a recording folder (recording
    // thumbnails are pictures too, and go with their recording).
    private static List<(FileInfo File, bool IsRecording)> Candidates(
        IReadOnlyList<string> folders,
        IReadOnlyList<string> recordingRoots,
        string? snapshotRoot,
        DiskSpaceGuardRequest request,
        List<MediaCleanupRunError> errors)
    {
        var recordingExtensions = new HashSet<string>(request.RecordingExtensions, StringComparer.OrdinalIgnoreCase);
        var snapshotExtensions = new HashSet<string>(request.SnapshotExtensions, StringComparer.OrdinalIgnoreCase);
        var candidates = new List<(FileInfo File, bool IsRecording)>();

        foreach (var folder in folders.Where(Directory.Exists))
        {
            var isRecordingFolder = recordingRoots.Contains(folder, StringComparer.OrdinalIgnoreCase);
            var isSnapshotFolder = string.Equals(folder, snapshotRoot, StringComparison.OrdinalIgnoreCase);
            try
            {
                foreach (var path in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                {
                    var full = Path.GetFullPath(path);
                    if (request.ProtectedPaths.Contains(full))
                    {
                        continue;
                    }

                    var extension = Path.GetExtension(full);
                    if (isRecordingFolder && recordingExtensions.Contains(extension))
                    {
                        candidates.Add((new FileInfo(full), true));
                    }
                    else if (isSnapshotFolder &&
                             snapshotExtensions.Contains(extension) &&
                             !recordingRoots.Any(r => RecordingRoots.Contains(r, full)))
                    {
                        candidates.Add((new FileInfo(full), false));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(new MediaCleanupRunError(folder, ex));
            }
        }

        // A recording folder inside the snapshot folder is listed twice: once is enough.
        return candidates
            .DistinctBy(c => c.File.FullName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c.File.LastWriteTimeUtc)
            .ToList();
    }

    private static long DeleteCompanions(
        string video,
        DiskSpaceGuardRequest request,
        List<string> deletedThumbnails,
        List<MediaCleanupRunError> errors)
    {
        long freed = 0;
        foreach (var companion in RecordingPathHelper.GetCompanionPaths(video, RecordingRoots.Innermost(video, request.RecordingFolders)))
        {
            try
            {
                var info = new FileInfo(companion);
                if (!info.Exists || request.ProtectedPaths.Contains(info.FullName))
                {
                    continue;
                }

                var size = info.Length;
                info.Delete();
                deletedThumbnails.Add(info.FullName);
                freed += size;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(new MediaCleanupRunError(companion, ex));
            }
        }

        return freed;
    }

    private static MediaCleanupRunResult Empty()
        => new([], [], 0, []);
}