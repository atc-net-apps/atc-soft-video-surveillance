namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// Volumes for the disk-space guard: folders whose free space a test sets. A path below no
/// such folder is on a volume whose space cannot be read.
/// </summary>
internal sealed class FakeVolumes : AtcSoft.VideoSurveillance.Services.IFreeSpaceReader
{
    private const string Unknown = "unknown-volume";

    private readonly Dictionary<string, long?> free = new(StringComparer.OrdinalIgnoreCase);

    public void Set(
        string volume,
        long? freeBytes)
        => free[Path.TrimEndingDirectorySeparator(Path.GetFullPath(volume))] = freeBytes;

    public string? GetVolume(string path)
    {
        var full = Path.GetFullPath(path);
        return free.Keys.FirstOrDefault(v => full.StartsWith(v + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(full, v, StringComparison.OrdinalIgnoreCase))
               ?? Unknown;
    }

    public long? GetFreeBytes(string volume)
        => free.GetValueOrDefault(volume);
}