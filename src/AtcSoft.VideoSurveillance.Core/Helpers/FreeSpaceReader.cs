namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Reads the free space of the volume a folder is on. On Windows it asks for the folder itself,
/// so a network share (<c>\\server\share</c>) and a volume mounted in a folder are read
/// correctly, which <see cref="DriveInfo"/> cannot do.
/// </summary>
public sealed partial class FreeSpaceReader : IFreeSpaceReader
{
    /// <summary>
    /// Gets the reader for the machine.
    /// </summary>
    public static FreeSpaceReader Default { get; } = new();

    /// <inheritdoc />
    public string? GetVolume(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(path);
            var existing = NearestExistingFolder(full) ?? full;
            if (OperatingSystem.IsWindows())
            {
                var buffer = new char[1024];
                return GetVolumePathName(existing, buffer, buffer.Length)
                    ? new string(buffer, 0, Array.IndexOf(buffer, '\0') is var end and >= 0 ? end : buffer.Length)
                    : Path.GetPathRoot(full);
            }

            // Elsewhere the volume is the deepest mount point the path is below.
            return DriveInfo.GetDrives()
                .Select(d => d.RootDirectory.FullName)
                .Where(r => existing.StartsWith(r, StringComparison.Ordinal))
                .OrderByDescending(r => r.Length)
                .FirstOrDefault() ?? Path.GetPathRoot(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public long? GetFreeBytes(string volume)
    {
        if (string.IsNullOrWhiteSpace(volume))
        {
            return null;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                return GetDiskFreeSpaceEx(volume, out var freeToCaller, out _, out _)
                    ? (long)Math.Min(freeToCaller, long.MaxValue)
                    : null;
            }

            var drive = new DriveInfo(volume);
            return drive.IsReady ? drive.AvailableFreeSpace : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? NearestExistingFolder(string path)
    {
        var folder = path;
        while (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
        {
            folder = Path.GetDirectoryName(folder);
        }

        return string.IsNullOrEmpty(folder) ? null : folder;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetDiskFreeSpaceEx(
        string directoryName,
        out ulong freeBytesAvailableToCaller,
        out ulong totalNumberOfBytes,
        out ulong totalNumberOfFreeBytes);

    [LibraryImport("kernel32.dll", EntryPoint = "GetVolumePathNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetVolumePathName(
        string fileName,
        [Out] char[] volumePathName,
        int bufferLength);
}