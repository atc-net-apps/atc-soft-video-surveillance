namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Reads and writes the index of a recording's preview pictures (<c>{video}.previews.json</c>). The
/// index is written after the JPEG, each to a file of its own first and then renamed, so a reader
/// that finds the index finds a complete JPEG.
/// </summary>
public static class RecordingPreviewFile
{
    /// <summary>
    /// The format's version; an index of another version is not read.
    /// </summary>
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Reads the index of a recording's previews.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <returns>The layout, or <see langword="null"/> when there are no previews (or they cannot be read).</returns>
    public static RecordingPreviews? Read(string videoPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        var indexPath = RecordingPathHelper.BuildPreviewIndexPath(videoPath);
        try
        {
            if (!File.Exists(indexPath) || !File.Exists(RecordingPathHelper.BuildPreviewImagePath(videoPath)))
            {
                return null;
            }

            var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllBytes(indexPath), Options);
            return stored is { Version: FormatVersion, IntervalSeconds: > 0, TileWidth: > 0, TileHeight: > 0, Columns: > 0, GridCount: > 0 }
                ? new RecordingPreviews(
                    stored.IntervalSeconds,
                    stored.TileWidth,
                    stored.TileHeight,
                    stored.Columns,
                    stored.GridCount,
                    [.. (stored.Events ?? []).Select(e => new RecordingPreviewEvent(e.Seconds, e.Tile))])
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a recording's previews: the JPEG, then the index.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <param name="previews">The layout.</param>
    /// <param name="writeImage">Writes the JPEG to the file it is given.</param>
    public static void Write(
        string videoPath,
        RecordingPreviews previews,
        Action<string> writeImage)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);
        ArgumentNullException.ThrowIfNull(previews);
        ArgumentNullException.ThrowIfNull(writeImage);

        var indexPath = RecordingPathHelper.BuildPreviewIndexPath(videoPath);
        var imagePath = RecordingPathHelper.BuildPreviewImagePath(videoPath);

        // An index left from earlier must not point at a JPEG being replaced.
        File.Delete(indexPath);
        WriteThenRename(imagePath, writeImage);

        var stored = new Stored
        {
            Version = FormatVersion,
            IntervalSeconds = previews.IntervalSeconds,
            TileWidth = previews.TileWidth,
            TileHeight = previews.TileHeight,
            Columns = previews.Columns,
            GridCount = previews.GridCount,
            Events = [.. previews.Events.Select(e => new StoredEvent { Seconds = e.Seconds, Tile = e.Tile })],
        };
        WriteThenRename(indexPath, path => File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(stored, Options)));
    }

    private static void WriteThenRename(
        string path,
        Action<string> write)
    {
        var temporary = path + ".tmp";
        try
        {
            write(temporary);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private sealed class Stored
    {
        public int Version { get; set; }

        public double IntervalSeconds { get; set; }

        public int TileWidth { get; set; }

        public int TileHeight { get; set; }

        public int Columns { get; set; }

        public int GridCount { get; set; }

        public List<StoredEvent>? Events { get; set; }
    }

    private sealed class StoredEvent
    {
        public double Seconds { get; set; }

        public int Tile { get; set; }
    }
}