namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// How full the disk with the recordings is. Recordings stop when it runs
/// out, so the apps show this where it is seen: in the status bar.
/// </summary>
/// <param name="FreeBytes">The space left.</param>
/// <param name="TotalBytes">The size of the disk.</param>
/// <param name="MinFreeBytes">The space the disk-space guard keeps free; zero when the guard is off.</param>
public sealed record DiskSpaceStatus(
    long FreeBytes,
    long TotalBytes,
    long MinFreeBytes)
{
    private const double Kilo = 1024;
    private const long BytesPerMegabyte = 1024L * 1024;

    /// <summary>
    /// Gets a value indicating whether less space is left than the
    /// recordings need. New recordings are then refused.
    /// </summary>
    public bool IsLow => MinFreeBytes > 0 && FreeBytes < MinFreeBytes;

    /// <summary>
    /// Gets the share of the disk that is in use, 0 to 100.
    /// </summary>
    public int UsedPercent
        => TotalBytes > 0
            ? (int)Math.Round(100.0 * (TotalBytes - FreeBytes) / TotalBytes, MidpointRounding.AwayFromZero)
            : 0;

    /// <summary>
    /// Gets the status as text ("120 GB free of 500 GB").
    /// </summary>
    public string Text => $"{Format(FreeBytes)} free of {Format(TotalBytes)}";

    /// <summary>
    /// Reads the disk a folder is on.
    /// </summary>
    /// <param name="path">The folder; it does not have to exist yet.</param>
    /// <param name="minFreeSpaceMb">The space the disk-space guard keeps free, in megabytes.</param>
    /// <param name="guardEnabled">Whether the disk-space guard is on.</param>
    /// <returns>The status; <see langword="null"/> when the path has no disk that can be read.</returns>
    public static DiskSpaceStatus? Read(
        string? path,
        int minFreeSpaceMb,
        bool guardEnabled = true)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady)
            {
                return null;
            }

            var minFreeBytes = guardEnabled ? Math.Max(0, minFreeSpaceMb) * BytesPerMegabyte : 0;
            return new DiskSpaceStatus(drive.AvailableFreeSpace, drive.TotalSize, minFreeBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // A network share, a drive that is gone, a path that is none:
            // the status bar then says nothing, which is better than failing.
            return null;
        }
    }

    private static string Format(long bytes)
    {
        var megabytes = Math.Max(0, bytes) / Kilo / Kilo;
        if (megabytes < Kilo)
        {
            return megabytes.ToString("0", CultureInfo.InvariantCulture) + " MB";
        }

        var gigabytes = megabytes / Kilo;
        if (gigabytes < Kilo)
        {
            return FormatShort(gigabytes) + " GB";
        }

        return FormatShort(gigabytes / Kilo) + " TB";
    }

    // One decimal while the number is small enough for it to matter.
    private static string FormatShort(double value)
        => value.ToString(value < 10 ? "0.#" : "0", CultureInfo.InvariantCulture);
}