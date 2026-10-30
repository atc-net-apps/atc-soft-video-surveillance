namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Atomic JSON file I/O with verify-and-rename semantics.
/// Protects against power-loss / mid-write corruption that would otherwise
/// destroy persistent configuration on a single failed write.
/// </summary>
public static class SafeJsonFile
{
    /// <summary>
    /// Serializes <paramref name="value"/> and writes it to <paramref name="path"/>
    /// atomically: write to <c>{path}.tmp</c>, verify the temp file round-trips,
    /// then <see cref="File.Replace(string, string, string?)"/> into place keeping
    /// the previous file as <c>{path}.bak</c>. A crash mid-write leaves either the
    /// previous file or the new file fully intact — never a half-written one.
    /// </summary>
    /// <typeparam name="T">Type of <paramref name="value"/> being serialized.</typeparam>
    /// <returns><c>true</c> on success, <c>false</c> if any step failed.</returns>
    public static bool TryWrite<T>(
        string path,
        T value,
        JsonSerializerOptions? options = null)
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
            File.WriteAllText(tempPath, json);

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