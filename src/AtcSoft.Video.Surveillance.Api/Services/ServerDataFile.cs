namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// One of the server's data files (cameras, settings, accounts, webhooks): read so a file that
/// cannot be read is never mistaken for a missing one, and never written over while it could
/// not be kept. A file that cannot be read is logged at Error and copied aside; when it cannot
/// even be copied (locked, access denied), saving is refused until the server is restarted.
/// </summary>
public sealed partial class ServerDataFile
{
    private readonly Microsoft.Extensions.Logging.ILogger logger;
    private volatile bool mayOverwrite = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerDataFile"/> class.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="logger">Where what was found is written.</param>
    public ServerDataFile(
        string path,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(logger);

        Path = path;
        this.logger = logger;
    }

    /// <summary>
    /// Gets the file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets a value indicating whether a save may replace the file: false when it could not be
    /// read at start-up and could not be copied aside either.
    /// </summary>
    public bool MayOverwrite => mayOverwrite;

    /// <summary>
    /// Gets what the last read found.
    /// </summary>
    public JsonFileReadOutcome Outcome { get; private set; } = JsonFileReadOutcome.Missing;

    /// <summary>
    /// Reads the file with the serializer.
    /// </summary>
    /// <typeparam name="T">Type to read.</typeparam>
    /// <param name="options">The serializer options.</param>
    /// <param name="lockedAttempts">How many times a locked file is tried.</param>
    /// <returns>The value; <c>default</c> when there is none (logged when the file exists).</returns>
    public T? Read<T>(
        JsonSerializerOptions? options,
        int lockedAttempts = 5)
        => Read(json => JsonSerializer.Deserialize<T>(json, options), lockedAttempts);

    /// <summary>
    /// Reads the file with a parser of the caller's own, which throws or returns <c>null</c>
    /// when the text does not hold a valid value.
    /// </summary>
    /// <typeparam name="T">Type to read.</typeparam>
    /// <param name="parse">Turns the file's text into the value.</param>
    /// <param name="lockedAttempts">How many times a locked file is tried.</param>
    /// <returns>The value; <c>default</c> when there is none (logged when the file exists).</returns>
    public T? Read<T>(
        Func<string, T?> parse,
        int lockedAttempts = 5)
    {
        var result = SafeJsonFile.Read(Path, parse, lockedAttempts);
        mayOverwrite = result.MayOverwrite;
        Outcome = result.Outcome;

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
            LogNotSaving(Path);
        }

        return result.Value;
    }

    /// <summary>
    /// Writes the file atomically, unless it may not be overwritten.
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
        if (!mayOverwrite)
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "The data file {Path} is missing; its backup (.bak) was read instead")]
    private partial void LogReadFromBackupMissing(string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "The data file {Path} could not be read; its backup (.bak) was read instead. The file that could not be read was copied to: {Copies}")]
    private partial void LogReadFromBackup(Exception? ex, string path, string copies);

    [LoggerMessage(Level = LogLevel.Error, Message = "The data file {Path} and its backup could not be read; the server starts without what they hold. The files that could not be read were copied to: {Copies}")]
    private partial void LogUnreadable(Exception? ex, string path, string copies);

    [LoggerMessage(Level = LogLevel.Error, Message = "The data file {Path} could not be read and could not be copied aside; changes are not saved to it until the server is restarted with the file readable")]
    private partial void LogNotSaving(string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Not saving {Path}: it could not be read at start-up and would be lost; restart the server once the file can be read")]
    private partial void LogSaveRefused(string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save {Path}")]
    private partial void LogSaveFailed(string path);
}