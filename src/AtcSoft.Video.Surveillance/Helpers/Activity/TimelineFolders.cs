namespace AtcSoft.Video.Surveillance.Helpers.Activity;

/// <summary>
/// Opens another CameraWall's folder for the timeline, read only: a data folder shared from another
/// computer (<c>\\pc\CameraWall</c>, with <c>cameras.json</c>, <c>recordings</c>,
/// <c>availability</c> and <c>logs</c>), its <c>recordings</c> folder, or any folder of camera
/// folders. The cameras come from its <c>cameras.json</c> (only their ids and names: the passwords
/// in it are not read); a camera folder no camera owns (a camera renamed or deleted) gets a lane of
/// its own, named by the folder.
/// </summary>
public static class TimelineFolders
{
    private const string CamerasFileName = "cameras.json";
    private const string RecordingsFolderName = "recordings";
    private const string LogsFolderName = "logs";

    /// <summary>
    /// Opens a folder.
    /// </summary>
    /// <param name="folder">The folder chosen.</param>
    /// <returns>What the timeline reads, or <see langword="null"/> when the folder cannot be reached.</returns>
    public static TimelineFolderSource? Open(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);

        try
        {
            if (!Directory.Exists(folder))
            {
                return null;
            }

            var trimmed = Path.TrimEndingDirectorySeparator(folder);
            string root;
            string recordings;
            if (File.Exists(Path.Combine(trimmed, CamerasFileName)))
            {
                root = trimmed;
                var inside = Path.Combine(trimmed, RecordingsFolderName);
                recordings = Directory.Exists(inside) ? inside : trimmed;
            }
            else if (Path.GetDirectoryName(trimmed) is { Length: > 0 } parent && File.Exists(Path.Combine(parent, CamerasFileName)))
            {
                root = parent;
                recordings = trimmed;
            }
            else
            {
                root = trimmed;
                recordings = trimmed;
            }

            var cameras = ReadCameras(Path.Combine(root, CamerasFileName)).ToList();
            var owned = new HashSet<string>(cameras.Select(c => RecordingPathHelper.GetCameraFolderName(c.Name, c.Id)), StringComparer.OrdinalIgnoreCase);
            foreach (var camera in Directory.EnumerateDirectories(recordings)
                         .Select(Path.GetFileName)
                         .OfType<string>()
                         .Where(name => !owned.Contains(name) && !string.Equals(name, RecordingPathHelper.ThumbnailsFolderName, StringComparison.OrdinalIgnoreCase))
                         .Order(StringComparer.CurrentCultureIgnoreCase))
            {
                cameras.Add(new TimelineCamera(IdOf(camera), camera));
            }

            var availability = Path.Combine(root, AvailabilityFolder.FolderName);
            var logs = Path.Combine(root, LogsFolderName);
            return new TimelineFolderSource(
                root,
                cameras,
                recordings,
                Directory.Exists(availability) ? availability : null,
                Directory.Exists(logs) ? logs : null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the ids and names of the cameras in a <c>cameras.json</c>; nothing when it is missing or
    /// not understood.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The cameras, in the file's order.</returns>
    public static IReadOnlyList<TimelineCamera> ReadCameras(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            if (!TryGet(document.RootElement, "cameras", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var cameras = new List<TimelineCamera>();
            foreach (var camera in list.EnumerateArray())
            {
                if (camera.ValueKind != JsonValueKind.Object ||
                    !TryGet(camera, "id", out var id) ||
                    id.ValueKind != JsonValueKind.String ||
                    !Guid.TryParse(id.GetString(), out var cameraId))
                {
                    continue;
                }

                var name = TryGet(camera, "display", out var display) &&
                           display.ValueKind == JsonValueKind.Object &&
                           TryGet(display, "displayName", out var displayName) &&
                           displayName.ValueKind == JsonValueKind.String
                    ? displayName.GetString() ?? string.Empty
                    : string.Empty;
                cameras.Add(new TimelineCamera(cameraId, name));
            }

            return cameras;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    // The same folder name always gives the same lane.
    private static Guid IdOf(string folderName)
        => new(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(folderName.ToUpperInvariant()))[..16]);

    private static bool TryGet(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}