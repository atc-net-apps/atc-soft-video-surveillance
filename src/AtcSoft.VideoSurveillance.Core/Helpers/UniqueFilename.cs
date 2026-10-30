namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Resolves filename collisions by appending a numeric suffix.
/// </summary>
/// <remarks>
/// Used for recording, snapshot, and timelapse outputs where two near-
/// simultaneous events (NTP backward correction, fast motion triggers,
/// rapid segmentation) could otherwise overwrite an existing file.
/// </remarks>
public static class UniqueFilename
{
    private const int MaxSuffix = 999;

    /// <summary>
    /// Returns <paramref name="desiredPath"/> if no file exists at that
    /// location, otherwise inserts <c>_2</c>, <c>_3</c>, … before the
    /// extension until an unused name is found. After
    /// <see cref="MaxSuffix"/> collisions, falls back to appending the
    /// current UTC millisecond for ironclad uniqueness.
    /// </summary>
    /// <param name="desiredPath">The intended output path.</param>
    /// <param name="fileExists">
    /// Optional override of <see cref="File.Exists"/> for testability.
    /// </param>
    public static string EnsureUnique(
        string desiredPath,
        Func<string, bool>? fileExists = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(desiredPath);

        var exists = fileExists ?? File.Exists;

        if (!exists(desiredPath))
        {
            return desiredPath;
        }

        var dir = Path.GetDirectoryName(desiredPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(desiredPath);
        var ext = Path.GetExtension(desiredPath);

        for (var i = 2; i <= MaxSuffix; i++)
        {
            var candidate = Path.Combine(
                dir,
                string.Create(CultureInfo.InvariantCulture, $"{stem}_{i}{ext}"));

            if (!exists(candidate))
            {
                return candidate;
            }
        }

        // Last resort: implausibly many collisions in the same second.
        // Append a UTC millisecond stamp so we never overwrite.
        var ms = DateTime.UtcNow.ToString("HHmmssfff", CultureInfo.InvariantCulture);
        return Path.Combine(dir, string.Create(CultureInfo.InvariantCulture, $"{stem}_{ms}{ext}"));
    }
}