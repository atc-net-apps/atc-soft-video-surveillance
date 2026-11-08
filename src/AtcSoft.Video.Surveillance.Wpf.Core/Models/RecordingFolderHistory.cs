namespace AtcSoft.Video.Surveillance.Wpf.Core.Models;

/// <summary>
/// The other CameraWall folders the recordings or the timeline were opened on, the latest first, so
/// both offer them again: a JSON list of paths in the viewer's own data
/// (<c>CameraWall\recording-folders.json</c>). A file that cannot be read or written is no list.
/// </summary>
public sealed class RecordingFolderHistory
{
    /// <summary>
    /// How many folders are kept.
    /// </summary>
    public const int MaximumCount = 5;

    private readonly string path;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingFolderHistory"/> class.
    /// </summary>
    /// <param name="path">The file; the viewer's own when not given.</param>
    public RecordingFolderHistory(string? path = null)
        => this.path = path ?? AtcSoft.Video.Surveillance.Helpers.ApplicationPaths.GetLocalDataPath("CameraWall", "recording-folders.json");

    /// <summary>
    /// Reads the folders, the latest first.
    /// </summary>
    /// <returns>The folders.</returns>
    public IReadOnlyList<string> Read()
    {
        try
        {
            return File.Exists(path)
                ? (JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [])
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Take(MaximumCount)
                    .ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Puts a folder first, and keeps at most <see cref="MaximumCount"/>.
    /// </summary>
    /// <param name="folder">The folder opened.</param>
    /// <returns>The folders, the latest first.</returns>
    public IReadOnlyList<string> Add(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);

        var folders = Read()
            .Where(f => !string.Equals(f, folder, StringComparison.OrdinalIgnoreCase))
            .Prepend(folder)
            .Take(MaximumCount)
            .ToList();
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(folders));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not remembered: the folder still opens.
        }

        return folders;
    }
}