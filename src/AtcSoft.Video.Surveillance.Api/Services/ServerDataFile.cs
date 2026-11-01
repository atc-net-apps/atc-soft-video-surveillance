namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// One of the server's data files (cameras, settings, accounts, webhooks): read so a file that
/// cannot be read is never mistaken for a missing one, and never written over while it could
/// not be kept. A file that cannot be read is logged at Error and copied aside. A file that is
/// locked at start-up (a backup or a virus scanner) is waited for. When it can be neither read
/// nor copied aside, saving is refused until it can be copied aside.
/// </summary>
public sealed partial class ServerDataFile
{
    /// <summary>
    /// How many times a locked file is tried at start-up by default, half a second apart: a minute.
    /// </summary>
    public const int DefaultLockedAttempts = 120;

    private readonly Microsoft.Extensions.Logging.ILogger logger;
    private readonly int lockedAttempts;
    private readonly Action<string>? onPreserved;
    private readonly Lock sync = new();
    private List<string> unpreserved = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerDataFile"/> class.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="logger">Where what was found is written.</param>
    /// <param name="lockedAttempts">How many times a locked file is tried, half a second apart.</param>
    /// <param name="onPreserved">Told of each copy made of a file that could not be read (a file with secrets restricts it).</param>
    public ServerDataFile(
        string path,
        Microsoft.Extensions.Logging.ILogger logger,
        int lockedAttempts = DefaultLockedAttempts,
        Action<string>? onPreserved = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(logger);

        Path = path;
        this.logger = logger;
        this.lockedAttempts = Math.Max(1, lockedAttempts);
        this.onPreserved = onPreserved;
    }

    /// <summary>
    /// Gets the file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets a value indicating whether a save may replace the file: false while a file that
    /// could not be read has not been copied aside.
    /// </summary>
    public bool MayOverwrite
    {
        get
        {
            lock (sync)
            {
                return unpreserved.Count == 0;
            }
        }
    }

    /// <summary>
    /// Gets what the last read found.
    /// </summary>
    public JsonFileReadOutcome Outcome { get; private set; } = JsonFileReadOutcome.Missing;

    /// <summary>
    /// Reads the file with the serializer.
    /// </summary>
    /// <typeparam name="T">Type to read.</typeparam>
    /// <param name="options">The serializer options.</param>
    /// <returns>The value; <c>default</c> when there is none (logged when the file exists).</returns>
    public T? Read<T>(JsonSerializerOptions? options)
        => Read(json => JsonSerializer.Deserialize<T>(json, options));

    /// <summary>
    /// Reads the file with a parser of the caller's own, which throws or returns <c>null</c>
    /// when the text does not hold a valid value.
    /// </summary>
    /// <typeparam name="T">Type to read.</typeparam>
    /// <param name="parse">Turns the file's text into the value.</param>
    /// <returns>The value; <c>default</c> when there is none (logged when the file exists).</returns>
    public T? Read<T>(Func<string, T?> parse)
    {
        var result = SafeJsonFile.Read(Path, parse, lockedAttempts);
        lock (sync)
        {
            unpreserved = [.. result.UnpreservedFiles];
        }

        Outcome = result.Outcome;
        foreach (var copy in result.PreservedCopies)
        {
            onPreserved?.Invoke(copy);
        }

        var copies = string.Join(", ", result.PreservedCopies);
        switch (result.Outcome)
        {
            case JsonFileReadOutcome.ReadFromBackup when result.Error is FileNotFoundException:
                LogReadFromBackupMissing(Path);
                break;
            case JsonFileReadOutcome.ReadFromBackup:
                LogReadFromBackup(result.Error, Path, copies);
                break;
            case JsonFileReadOutcome.Unreadable:
                LogUnreadable(result.Error, Path, copies);
                break;
        }

        if (!result.MayOverwrite)
        {
            LogNotSaving(Path, string.Join(", ", result.UnpreservedFiles));
        }

        return result.Value;
    }

    /// <summary>
    /// Writes the file atomically, unless a file that could not be read would be lost.
    /// </summary>
    /// <typeparam name="T">Type of <paramref name="value"/>.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="options">The serializer options.</param>
    /// <param name="writeTempFile">Writes the temp file; see <see cref="SafeJsonFile.TryWrite{T}"/>.</param>
    /// <returns><c>true</c> when the file was written.</returns>
    public bool TryWrite<T>(
        T value,
        JsonSerializerOptions? options,
        Action<string, string>? writeTempFile = null)
    {
        if (!TryPreservePending())
        {
            LogSaveRefused(Path);
            return false;
        }

        if (SafeJsonFile.TryWrite(Path, value, options, writeTempFile))
        {
            return true;
        }

        LogSaveFailed(Path);
        return false;
    }

    // A file that was locked at start-up may be free by now: once it is copied aside, saving
    // can no longer lose it.
    private bool TryPreservePending()
    {
        lock (sync)
        {
            if (unpreserved.Count == 0)
            {
                return true;
            }

            foreach (var file in unpreserved.ToList())
            {
                if (!File.Exists(file))
                {
                    unpreserved.Remove(file);
                    continue;
                }

                if (SafeJsonFile.TryPreserve(file, out var copy))
                {
                    unpreserved.Remove(file);
                    onPreserved?.Invoke(copy);
                    LogPreservedLater(file, copy);
                }
            }

            return unpreserved.Count == 0;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The data file {Path} is missing; its backup (.bak) was read instead")]
    private partial void LogReadFromBackupMissing(string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "The data file {Path} could not be read; its backup (.bak) was read instead. The file that could not be read was copied to: {Copies}")]
    private partial void LogReadFromBackup(Exception? ex, string path, string copies);

    [LoggerMessage(Level = LogLevel.Error, Message = "The data file {Path} and its backup could not be read; the server starts without what they hold. The files that could not be read were copied to: {Copies}")]
    private partial void LogUnreadable(Exception? ex, string path, string copies);

    [LoggerMessage(Level = LogLevel.Error, Message = "The data file {Path} could not be read, and {Files} could not be copied aside; changes are not saved to it until they can be. Restart the server once the file can be read")]
    private partial void LogNotSaving(string path, string files);

    [LoggerMessage(Level = LogLevel.Error, Message = "Not saving {Path}: a file that could not be read at start-up would be lost; restart the server once the file can be read")]
    private partial void LogSaveRefused(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The data file {File}, which could not be read at start-up, was copied to {Copy}; it is now saved over")]
    private partial void LogPreservedLater(string file, string copy);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save {Path}")]
    private partial void LogSaveFailed(string path);
}