namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// The folders recordings live in: the global recording folder and each camera's own folder
/// (<c>Overrides.Recording.RecordingPath</c>). Everything that reads recordings reads all of
/// them, each folder once: a folder inside another is read as part of it.
/// </summary>
public static class RecordingRoots
{
    /// <summary>
    /// Gets the folders to read, the global one first, as full paths.
    /// </summary>
    /// <param name="globalRoot">The global recording folder, or <see langword="null"/>.</param>
    /// <param name="cameraRoots">Each camera's own recording folder; blanks are skipped.</param>
    public static IReadOnlyList<string> Collect(
        string? globalRoot,
        IEnumerable<string?> cameraRoots)
    {
        ArgumentNullException.ThrowIfNull(cameraRoots);

        var candidates = new List<string>();
        foreach (var root in new[] { globalRoot }.Concat(cameraRoots))
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root.Trim()));
            if (!candidates.Contains(full, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(full);
            }
        }

        return candidates
            .Where(root => !candidates.Any(other => !ReferenceEquals(other, root) && IsInside(root, other)))
            .ToList();
    }

    /// <summary>
    /// Gets the innermost of the folders that holds a file: the recording folder the file
    /// belongs to when a camera's own folder sits inside another.
    /// </summary>
    /// <param name="path">A file or folder.</param>
    /// <param name="folders">The recording folders.</param>
    /// <returns>The folder as a full path, or <see langword="null"/> when none holds the path.</returns>
    public static string? Innermost(
        string path,
        IEnumerable<string?> folders)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(folders);

        var full = Path.GetFullPath(path);
        return folders
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => Path.TrimEndingDirectorySeparator(Path.GetFullPath(f!.Trim())))
            .Where(f => Contains(f, full))
            .OrderByDescending(f => f.Length)
            .FirstOrDefault();
    }

    /// <summary>
    /// Determines whether a path is a folder or lies inside it.
    /// </summary>
    /// <param name="folder">The folder, as a full path.</param>
    /// <param name="path">The path, as a full path.</param>
    public static bool Contains(
        string folder,
        string path)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(path);

        var trimmed = Path.TrimEndingDirectorySeparator(folder);
        return string.Equals(Path.TrimEndingDirectorySeparator(path), trimmed, StringComparison.OrdinalIgnoreCase) ||
               IsInside(path, trimmed);
    }

    private static bool IsInside(
        string folder,
        string possibleParent)
        => folder.StartsWith(possibleParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}