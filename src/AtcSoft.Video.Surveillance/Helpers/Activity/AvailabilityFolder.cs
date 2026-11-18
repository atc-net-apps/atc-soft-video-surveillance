namespace AtcSoft.Video.Surveillance.Helpers.Activity;

/// <summary>
/// CameraWall's folder of availability history (<see cref="AvailabilityLog"/>): one file per camera,
/// <c>{id:N}.jsonl</c>, and one for the app's own starts and stops, <see cref="AppFileName"/>. The
/// app's starts and stops are noted as <see cref="AvailabilityChange.ServerStarted"/> and
/// <see cref="AvailabilityChange.ServerStopped"/>, as the server notes its own.
/// </summary>
public static class AvailabilityFolder
{
    /// <summary>
    /// The name of the folder below CameraWall's data folder.
    /// </summary>
    public const string FolderName = "availability";

    /// <summary>
    /// The file of the app's starts and stops.
    /// </summary>
    public const string AppFileName = "app.jsonl";

    /// <summary>
    /// The file the running app notes the time in now and then: an app that stopped without saying
    /// so (a crash, a power cut) is noted as stopped then, at its next start.
    /// </summary>
    public const string AliveFileName = "app.alive";

    /// <summary>
    /// Gets the file of a camera's history.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="cameraId">The camera.</param>
    public static string CameraFile(
        string folder,
        Guid cameraId)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);

        return Path.Combine(folder, cameraId.ToString("N") + ".jsonl");
    }

    /// <summary>
    /// Reads a camera's history with the app's starts and stops.
    /// </summary>
    /// <param name="folder">The folder; nothing when it is missing or cannot be read.</param>
    /// <param name="cameraId">The camera.</param>
    /// <returns>The changes, oldest first.</returns>
    public static IReadOnlyList<AvailabilityEntry> Read(
        string folder,
        Guid cameraId)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);

        return
        [
            .. AvailabilityLog.Read(CameraFile(folder, cameraId))
                .Concat(AvailabilityLog.Read(Path.Combine(folder, AppFileName)))
                .OrderBy(e => e.Time),
        ];
    }

    /// <summary>
    /// Gets when the app last ran without noting its stop: the last start in the app's file has no
    /// stop after it. The stop is then the last time the app said it was alive, or the start itself.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns>When the app stopped, or <see langword="null"/> when its last run noted its stop.</returns>
    public static DateTimeOffset? FindUnannouncedStop(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);

        var history = AvailabilityLog.Read(Path.Combine(folder, AppFileName));
        if (history.Count == 0 || history[^1].Change != AvailabilityChange.ServerStarted)
        {
            return null;
        }

        var started = history[^1].Time;
        try
        {
            if (DateTimeOffset.TryParse(File.ReadAllText(Path.Combine(folder, AliveFileName)), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var said) && said > started)
            {
                return said;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No sign of life after the start: it stopped at once.
        }

        return started;
    }

    /// <summary>
    /// Gets the earliest change noted in the folder: before it, only the app's log can tell.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The earliest change, or <see langword="null"/> when there is none.</returns>
    public static DateTimeOffset? Earliest(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);

        var first = AvailabilityLog.Read(Path.Combine(folder, AppFileName));
        return first.Count > 0 ? first[0].Time : null;
    }
}