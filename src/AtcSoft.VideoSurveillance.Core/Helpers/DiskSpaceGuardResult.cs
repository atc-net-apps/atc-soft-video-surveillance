namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// What the disk-space guard found and did on each volume.
/// </summary>
/// <param name="Volumes">One entry per volume that holds media.</param>
public sealed record DiskSpaceGuardResult(
    IReadOnlyList<DiskSpaceVolumeResult> Volumes)
{
    /// <summary>
    /// Gets the result when the guard is off: nothing checked.
    /// </summary>
    public static DiskSpaceGuardResult None { get; } = new([]);

    /// <summary>
    /// Gets the folders on a volume that is still short.
    /// </summary>
    public IReadOnlyList<string> ShortFolders
        => FoldersIn(DiskSpaceVolumeState.StillShort);

    /// <summary>
    /// Gets the folders on a volume whose space could not be read.
    /// </summary>
    public IReadOnlyList<string> UnreadableFolders
        => FoldersIn(DiskSpaceVolumeState.Unreadable);

    /// <summary>
    /// Gets the files deleted on every volume.
    /// </summary>
    public int DeletedCount
        => Volumes.Sum(v => v.Run.DeletedFiles.Count);

    /// <summary>
    /// Gets what the guard found for the volume a file or folder is on: the state of the
    /// innermost checked folder that holds it; <see cref="DiskSpaceVolumeState.Ok"/> when none does.
    /// </summary>
    /// <param name="path">A recording, or a recording folder.</param>
    public DiskSpaceVolumeState StateOf(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var match = Volumes
            .SelectMany(v => v.Folders.Select(f => (Folder: f, v.State)))
            .Where(x => RecordingRoots.Contains(x.Folder, full))
            .OrderByDescending(x => x.Folder.Length)
            .Select(x => (DiskSpaceVolumeState?)x.State)
            .FirstOrDefault();

        return match ?? DiskSpaceVolumeState.Ok;
    }

    private List<string> FoldersIn(DiskSpaceVolumeState state)
        => Volumes.Where(v => v.State == state).SelectMany(v => v.Folders).ToList();
}