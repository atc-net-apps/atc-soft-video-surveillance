namespace AtcSoft.Video.Surveillance.Helpers;

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
    // How long the oldest-first list of a short volume is worked through before the volume is
    // walked again. A full disk is an NVR's usual state, and every recording that starts asks
    // first: walking the whole archive each time made each start wait on the walk.
    private static readonly TimeSpan CandidateListLifetime = TimeSpan.FromMinutes(10);

    private static readonly EnumerationOptions Recursive = new() { AttributesToSkip = default, RecurseSubdirectories = true };

    private readonly IFreeSpaceReader reader;
    private readonly Lock gate = new();
    private readonly Dictionary<string, DiskSpaceVolumeState> lastStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CandidateList> candidateLists = new(StringComparer.OrdinalIgnoreCase);

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
        var deleted = new List<string>();
        var deletedThumbnails = new List<string>();
        long freed = 0;

        // The list kept from an earlier pass first; should it run out while the volume is still
        // short, the volume is walked again: a file it did not know of may be older.
        var signature = Signature(folders, recordingRoots, snapshotRoot, request);
        var fresh = false;
        while (freed < needed)
        {
            if (!candidateLists.TryGetValue(volume, out var list) ||
                list.Count == 0 ||
                !string.Equals(list.MadeFrom, signature, StringComparison.Ordinal) ||
                System.Diagnostics.Stopwatch.GetElapsedTime(list.MadeAt) > CandidateListLifetime)
            {
                if (fresh)
                {
                    break;
                }

                list = new CandidateList(signature, Candidates(folders, recordingRoots, snapshotRoot, request, errors));
                candidateLists[volume] = list;
                fresh = true;
            }

            freed += DeleteOldest(list, needed - freed, request, deleted, deletedThumbnails, errors);
        }

        // Deleting the last recording of a day leaves its folder empty. Only the folders of what
        // was deleted are looked at (the clean-up sweeps the rest); a folder made a moment ago
        // may be about to get a recording, so young ones stay.
        RemoveEmptyFoldersOf(deleted.Concat(deletedThumbnails), folders, errors);

        var run = new MediaCleanupRunResult(deleted, deletedThumbnails, freed, errors) { StillShort = freed < needed };
        var state = run.StillShort ? DiskSpaceVolumeState.StillShort : DiskSpaceVolumeState.Reclaimed;
        return new DiskSpaceVolumeResult(volume, folders, state, free, run);
    }

    // Deletes from the front of the list until enough is freed or the list is used up. Each file
    // is looked at again first: it may have gone meanwhile, or be recorded into now.
    private static long DeleteOldest(
        CandidateList list,
        long needed,
        DiskSpaceGuardRequest request,
        List<string> deleted,
        List<string> deletedThumbnails,
        List<MediaCleanupRunError> errors)
    {
        long freed = 0;
        while (freed < needed && list.TryTake(out var file, out var isRecording))
        {
            if (request.ProtectedPaths.Contains(file.FullName))
            {
                continue;
            }

            try
            {
                file.Refresh();
                if (!file.Exists)
                {
                    continue;
                }

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

        return freed;
    }

    // The folders the deleted files were in, and the folders above them up to (not including)
    // the media folder they belong to, deepest first, while they are empty and old enough.
    private static void RemoveEmptyFoldersOf(
        IEnumerable<string> deletedFiles,
        IReadOnlyList<string> mediaFolders,
        List<MediaCleanupRunError> errors)
    {
        var oldestAllowedUtc = DateTime.UtcNow - MediaCleanupRunner.EmptyDirectoryGracePeriod;
        var looked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in deletedFiles)
        {
            var root = mediaFolders
                .Where(f => RecordingRoots.Contains(f, file))
                .MaxBy(f => f.Length);
            if (root is null)
            {
                continue;
            }

            for (var folder = Path.GetDirectoryName(file);
                 folder is not null &&
                 folder.Length > root.Length &&
                 looked.Add(folder);
                 folder = Path.GetDirectoryName(folder))
            {
                try
                {
                    if (!Directory.Exists(folder) ||
                        Directory.EnumerateFileSystemEntries(folder).Any() ||
                        Directory.GetCreationTimeUtc(folder) > oldestAllowedUtc)
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
            }
        }
    }

    // What a kept list was made from: another set of folders or extensions needs a new walk.
    private static string Signature(
        IReadOnlyList<string> folders,
        IReadOnlyList<string> recordingRoots,
        string? snapshotRoot,
        DiskSpaceGuardRequest request)
        => string.Join(
            '|',
            string.Join(';', folders.Order(StringComparer.OrdinalIgnoreCase)),
            string.Join(';', recordingRoots.Order(StringComparer.OrdinalIgnoreCase)),
            snapshotRoot,
            string.Join(';', request.RecordingExtensions.Order(StringComparer.OrdinalIgnoreCase)),
            string.Join(';', request.SnapshotExtensions.Order(StringComparer.OrdinalIgnoreCase)));

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
                // The size and time of each file come with the walk: no second look at each.
                foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", Recursive))
                {
                    var full = file.FullName;
                    if (request.ProtectedPaths.Contains(full))
                    {
                        continue;
                    }

                    var extension = file.Extension;
                    if (isRecordingFolder && recordingExtensions.Contains(extension))
                    {
                        candidates.Add((file, true));
                    }
                    else if (isSnapshotFolder &&
                             snapshotExtensions.Contains(extension) &&
                             !recordingRoots.Any(r => RecordingRoots.Contains(r, full)))
                    {
                        candidates.Add((file, false));
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

    // A volume's media, oldest first, from one walk; used under the guard's lock.
    private sealed class CandidateList(
        string signature,
        List<(FileInfo File, bool IsRecording)> candidates)
    {
        private int next;

        public string MadeFrom { get; } = signature;

        public long MadeAt { get; } = System.Diagnostics.Stopwatch.GetTimestamp();

        public int Count
            => candidates.Count - next;

        public bool TryTake(
            [NotNullWhen(true)] out FileInfo? file,
            out bool isRecording)
        {
            if (next >= candidates.Count)
            {
                file = null;
                isRecording = false;
                return false;
            }

            (file, isRecording) = candidates[next++];
            return true;
        }
    }
}