namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// The folders the live streams are written to. Each server instance writes below a folder of its
/// own, marked with the process that owns it, and each FFmpeg process it starts is marked in its
/// stream's folder. At start-up the folders of instances that are gone are cleared, and an FFmpeg
/// process one of them left running (a server that was killed before the job object ended it, or
/// a version without one) is ended, so it no longer holds the camera's connection or its files.
/// </summary>
public static partial class HlsFolders
{
    /// <summary>
    /// The file in an instance's folder that names the process owning it.
    /// </summary>
    public const string OwnerFileName = "owner.json";

    /// <summary>
    /// The file in a stream's folder that names the FFmpeg process writing it.
    /// </summary>
    public const string ProcessFileName = "ffmpeg.json";

    // Two readings of one start time differ by rounding only.
    private static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets the folder below which every instance has its own.
    /// </summary>
    public static string Root
        => Path.Combine(Path.GetTempPath(), "linksoft-hls");

    /// <summary>
    /// Makes a folder for this instance, marked with this process.
    /// </summary>
    /// <param name="root">The folder below which it is made.</param>
    /// <returns>The instance's folder.</returns>
    public static string CreateInstanceFolder(string root)
    {
        var folder = Path.Combine(root, "instance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        using var self = Process.GetCurrentProcess();
        WriteMarker(Path.Combine(folder, OwnerFileName), self);
        return folder;
    }

    /// <summary>
    /// Marks a stream's folder with the FFmpeg process that writes it.
    /// </summary>
    /// <param name="streamFolder">The stream's folder.</param>
    /// <param name="process">The FFmpeg process.</param>
    public static void MarkProcess(
        string streamFolder,
        Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        try
        {
            WriteMarker(Path.Combine(streamFolder, ProcessFileName), process);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Only the start-up sweep reads it; the job object ends the process anyway.
        }
    }

    /// <summary>
    /// Clears the folders of instances that are gone, ending the FFmpeg processes they left
    /// running. A folder whose owner still runs (another server, a test host) is left alone.
    /// </summary>
    /// <param name="root">The folder below which every instance has its own.</param>
    /// <param name="logger">Where what was cleared is written.</param>
    public static void SweepStale(
        string root,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(root))
        {
            return;
        }

        string[] folders;
        try
        {
            folders = Directory.GetDirectories(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var folder in folders)
        {
            var owner = Path.Combine(folder, OwnerFileName);
            if (File.Exists(owner))
            {
                if (IsRunning(ReadMarker(owner), out _))
                {
                    continue;
                }

                EndLeftProcesses(folder, logger);
            }
            else if (!IsLegacyStreamFolder(folder))
            {
                // Not one of ours.
                continue;
            }

            TryDelete(folder, logger);
        }
    }

    private static void EndLeftProcesses(
        string instanceFolder,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        IEnumerable<string> markers;
        try
        {
            markers = Directory.GetFiles(instanceFolder, ProcessFileName, SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var marker in markers)
        {
            if (!IsRunning(ReadMarker(marker), out var process))
            {
                continue;
            }

            using (process)
            {
                try
                {
                    // Only the FFmpeg that was started: a process id is used again by others.
                    if (process!.ProcessName.StartsWith("ffmpeg", StringComparison.OrdinalIgnoreCase))
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5000);
                        LogLeftProcessEnded(logger, process.Id, Path.GetDirectoryName(marker) ?? marker);
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    LogLeftProcessNotEnded(logger, ex, Path.GetDirectoryName(marker) ?? marker);
                }
            }
        }
    }

    // Whether the process a marker names still runs, and is the same process (same start time).
    private static bool IsRunning(
        Marker? marker,
        out Process? process)
    {
        process = null;
        if (marker is null)
        {
            // Unreadable: it may still be in use.
            return true;
        }

        Process? candidate = null;
        try
        {
            candidate = Process.GetProcessById(marker.Pid);
            if (candidate.HasExited ||
                (candidate.StartTime.ToUniversalTime() - marker.StartedUtc).Duration() > StartTimeTolerance)
            {
                candidate.Dispose();
                return false;
            }

            process = candidate;
            return true;
        }
        catch (ArgumentException)
        {
            // No process with that id.
            candidate?.Dispose();
            return false;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Cannot be told (another user's process): left alone.
            candidate?.Dispose();
            return true;
        }
    }

    // A folder an older server wrote directly below the root: one per camera.
    private static bool IsLegacyStreamFolder(string folder)
        => Guid.TryParseExact(Path.GetFileName(folder), "N", out _);

    private static void TryDelete(
        string folder,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogStaleFolderNotDeleted(logger, ex, folder);
        }
    }

    private static void WriteMarker(
        string path,
        Process process)
        => File.WriteAllText(path, JsonSerializer.Serialize(new Marker(process.Id, process.StartTime.ToUniversalTime())));

    private static Marker? ReadMarker(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<Marker>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ended FFmpeg process {ProcessId} that a previous run of the server left writing {Folder}")]
    private static partial void LogLeftProcessEnded(Microsoft.Extensions.Logging.ILogger logger, int processId, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not end the FFmpeg process a previous run of the server left writing {Folder}")]
    private static partial void LogLeftProcessNotEnded(Microsoft.Extensions.Logging.ILogger logger, Exception ex, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete the live-stream folder {Folder} a previous run of the server left")]
    private static partial void LogStaleFolderNotDeleted(Microsoft.Extensions.Logging.ILogger logger, Exception ex, string folder);

    private sealed record Marker(
        int Pid,
        DateTime StartedUtc);
}