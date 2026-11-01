namespace AtcSoft.Video.Surveillance.Helpers;

/// <summary>
/// Atomic JSON file I/O with verify-and-rename semantics.
/// Protects against power-loss / mid-write corruption that would otherwise
/// destroy persistent configuration on a single failed write.
/// </summary>
public static class SafeJsonFile
{
    private const int DefaultLockedAttempts = 5;

    private static readonly TimeSpan LockedRetryDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Serializes <paramref name="value"/> and writes it to <paramref name="path"/>
    /// atomically: write to <c>{path}.tmp</c>, verify the temp file round-trips,
    /// then <see cref="File.Replace(string, string, string?)"/> into place keeping
    /// the previous file as <c>{path}.bak</c>. A crash mid-write leaves either the
    /// previous file or the new file fully intact — never a half-written one.
    /// </summary>
    /// <typeparam name="T">Type of <paramref name="value"/> being serialized.</typeparam>
    /// <param name="path">The file to write.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">The serializer options.</param>
    /// <param name="writeTempFile">
    /// Writes the temp file (path, text); <see cref="File.WriteAllText(string, string?)"/> when not given.
    /// A file with secrets passes one that creates it with restricted access, which the replaced
    /// file then keeps: <see cref="File.Replace(string, string, string?)"/> keeps the access of the file it replaces.
    /// </param>
    /// <returns><c>true</c> on success, <c>false</c> if any step failed.</returns>
    public static bool TryWrite<T>(
        string path,
        T value,
        JsonSerializerOptions? options = null,
        Action<string, string>? writeTempFile = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var tempPath = path + ".tmp";
        var backupPath = path + ".bak";

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(value, options);

            // Write+flush the temp file
            (writeTempFile ?? File.WriteAllText)(tempPath, json);

            // Round-trip verify before promoting; catches truncated writes
            // (e.g. disk full mid-write) and serializer regressions.
            var verifyJson = File.ReadAllText(tempPath);
            _ = JsonSerializer.Deserialize<T>(verifyJson, options);

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, backupPath);
            }
            else
            {
                File.Move(tempPath, path);
            }

            return true;
        }
        catch
        {
            TryDeleteIfExists(tempPath);
            return false;
        }
    }

    /// <summary>
    /// Reads and deserializes JSON from <paramref name="path"/>. If the primary
    /// file is missing, empty, or fails to parse, falls back to <c>{path}.bak</c>
    /// written by a prior successful <see cref="TryWrite{T}"/>. Returns the
    /// type's default if neither file yields a valid value.
    /// </summary>
    /// <typeparam name="T">Type to deserialize.</typeparam>
    public static T? TryRead<T>(
        string path,
        JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (TryReadOne<T>(path, options, out var value))
        {
            return value;
        }

        if (TryReadOne<T>(path + ".bak", options, out var backup))
        {
            return backup;
        }

        return default;
    }

    /// <summary>
    /// Reads a data file that must not be lost: tells a missing file from one that cannot be read.
    /// Falls back to <c>{path}.bak</c> when the file is missing or cannot be read, and copies every
    /// file that exists but cannot be read aside (<c>{path}.unreadable-{time}</c>) so a later save
    /// does not destroy it. A file that is locked (a sharing violation, e.g. a virus scanner or a
    /// backup at boot) is tried again a few times before it counts as unreadable.
    /// </summary>
    /// <typeparam name="T">Type to read.</typeparam>
    /// <param name="path">The file to read.</param>
    /// <param name="options">The serializer options.</param>
    /// <param name="lockedAttempts">How many times a locked file is tried.</param>
    /// <returns>What was found; the caller must not save when <see cref="JsonFileReadResult{T}.MayOverwrite"/> is false.</returns>
    public static JsonFileReadResult<T> Read<T>(
        string path,
        JsonSerializerOptions? options = null,
        int lockedAttempts = DefaultLockedAttempts)
        => Read(path, json => JsonSerializer.Deserialize<T>(json, options), lockedAttempts);

    /// <summary>
    /// Reads a data file that must not be lost, with a parser of the caller's own: see
    /// <see cref="Read{T}(string, JsonSerializerOptions?, int)"/>. The parser throws, or returns
    /// <c>null</c>, when the text does not hold a valid value.
    /// </summary>
    /// <typeparam name="T">Type to read.</typeparam>
    /// <param name="path">The file to read.</param>
    /// <param name="parse">Turns the file's text into the value.</param>
    /// <param name="lockedAttempts">How many times a locked file is tried.</param>
    /// <returns>What was found; the caller must not save when <see cref="JsonFileReadResult{T}.MayOverwrite"/> is false.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any failure of the parser means the file cannot be read; the exception is returned.")]
    public static JsonFileReadResult<T> Read<T>(
        string path,
        Func<string, T?> parse,
        int lockedAttempts = DefaultLockedAttempts)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(parse);

        var backupPath = path + ".bak";
        var primaryExists = File.Exists(path);
        var backupExists = File.Exists(backupPath);
        if (!primaryExists && !backupExists)
        {
            return new JsonFileReadResult<T>(JsonFileReadOutcome.Missing, default, null, [], []);
        }

        Exception? primaryError = null;
        if (primaryExists)
        {
            if (TryReadWithParser(path, parse, lockedAttempts, out var value, out primaryError))
            {
                return new JsonFileReadResult<T>(JsonFileReadOutcome.Read, value, null, [], []);
            }
        }
        else
        {
            primaryError = new FileNotFoundException("The file is missing.", path);
        }

        var preserved = new List<string>();
        var unpreserved = new List<string>();
        if (primaryExists)
        {
            Preserve(path, preserved, unpreserved);
        }

        if (backupExists &&
            TryReadWithParser(backupPath, parse, lockedAttempts, out var backup, out _))
        {
            return new JsonFileReadResult<T>(JsonFileReadOutcome.ReadFromBackup, backup, primaryError, preserved, unpreserved);
        }

        if (backupExists)
        {
            Preserve(backupPath, preserved, unpreserved);
        }

        return new JsonFileReadResult<T>(JsonFileReadOutcome.Unreadable, default, primaryError, preserved, unpreserved);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any failure of the parser means the file cannot be read; the exception is returned.")]
    private static bool TryReadWithParser<T>(
        string path,
        Func<string, T?> parse,
        int lockedAttempts,
        out T? value,
        out Exception? error)
    {
        value = default;
        if (!TryReadText(path, lockedAttempts, out var json, out error))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            error = new InvalidDataException("The file is empty.");
            return false;
        }

        try
        {
            value = parse(json);
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }

        if (value is null)
        {
            error = new InvalidDataException("The file holds no value.");
            return false;
        }

        return true;
    }

    // Reads the text; a locked file (a sharing violation) is tried again.
    private static bool TryReadText(
        string path,
        int lockedAttempts,
        out string text,
        out Exception? error)
    {
        text = string.Empty;
        error = null;
        var attempt = 1;
        while (true)
        {
            try
            {
                text = File.ReadAllText(path);
                return true;
            }
            catch (IOException ex) when (ex is not FileNotFoundException and not DirectoryNotFoundException && attempt < lockedAttempts)
            {
                attempt++;
                Thread.Sleep(LockedRetryDelay);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex;
                return false;
            }
        }
    }

    /// <summary>
    /// Copies a file that cannot be read aside (<c>{path}.unreadable-{time}</c>), so a later
    /// save cannot destroy it.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="copy">Where it was copied.</param>
    /// <returns><c>false</c> when the file could not be copied (locked, access denied).</returns>
    public static bool TryPreserve(
        string path,
        [NotNullWhen(true)] out string? copy)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var target = string.Create(
            CultureInfo.InvariantCulture,
            $"{path}.unreadable-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}");
        try
        {
            File.Copy(path, target, overwrite: false);
            copy = target;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            copy = null;
            return false;
        }
    }

    private static void Preserve(
        string path,
        List<string> preserved,
        List<string> unpreserved)
    {
        if (TryPreserve(path, out var copy))
        {
            preserved.Add(copy);
        }
        else
        {
            unpreserved.Add(path);
        }
    }

    private static bool TryReadOne<T>(
        string path,
        JsonSerializerOptions? options,
        out T? value)
    {
        value = default;

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            value = JsonSerializer.Deserialize<T>(json, options);
            return value is not null;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup; never propagate
        }
    }
}