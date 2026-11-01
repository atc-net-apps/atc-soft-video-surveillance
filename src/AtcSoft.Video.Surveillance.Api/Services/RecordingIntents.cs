namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// <see cref="IRecordingIntents"/> over a file in the server's data folder
/// (<c>recording-intents.json</c>: the ids of the cameras to record).
/// </summary>
public sealed class RecordingIntents : IRecordingIntents
{
    private static readonly JsonSerializerOptions FileOptions = new() { WriteIndented = true };

    private readonly Lock sync = new();
    private readonly ServerDataFile file;
    private readonly HashSet<Guid> wanted;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingIntents"/> class.
    /// </summary>
    /// <param name="filePath">The file the intents are kept in.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="lockedAttempts">How many times a locked file is tried at start-up.</param>
    public RecordingIntents(
        string filePath,
        ILogger<RecordingIntents> logger,
        int lockedAttempts = ServerDataFile.DefaultLockedAttempts)
    {
        file = new ServerDataFile(filePath, logger, lockedAttempts);
        wanted = [.. file.Read<List<Guid>>(FileOptions) ?? []];
    }

    /// <inheritdoc />
    public bool IsWanted(Guid cameraId)
    {
        lock (sync)
        {
            return wanted.Contains(cameraId);
        }
    }

    /// <inheritdoc />
    public void Want(Guid cameraId)
    {
        lock (sync)
        {
            if (wanted.Add(cameraId))
            {
                file.TryWrite(wanted.ToList(), FileOptions);
            }
        }
    }

    /// <inheritdoc />
    public void Drop(Guid cameraId)
    {
        lock (sync)
        {
            if (wanted.Remove(cameraId))
            {
                file.TryWrite(wanted.ToList(), FileOptions);
            }
        }
    }
}