namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Serves the files below the recording folder that is configured right
/// now. A provider bound to the folder at start-up keeps serving the old
/// folder after the recording path is changed in the settings.
/// </summary>
public sealed class RecordingsFileProvider : IFileProvider, IDisposable
{
    private readonly IApplicationSettingsService settingsService;
    private readonly Lock gate = new();
    private PhysicalFileProvider? current;
    private string? currentRoot;

    public RecordingsFileProvider(IApplicationSettingsService settingsService)
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    /// <summary>
    /// Gets the content types of the files that are served: recordings and
    /// their thumbnails, nothing else that happens to lie in the folder.
    /// </summary>
    public static FileExtensionContentTypeProvider ContentTypes { get; } = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".mp4"] = "video/mp4",
            [".mkv"] = "video/x-matroska",
            [".avi"] = "video/x-msvideo",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
        });

    /// <inheritdoc />
    public IFileInfo GetFileInfo(string subpath)
        => Resolve()?.GetFileInfo(subpath) ?? new NotFoundFileInfo(subpath);

    /// <inheritdoc />
    public IDirectoryContents GetDirectoryContents(string subpath)
        => Resolve()?.GetDirectoryContents(subpath) ?? NotFoundDirectoryContents.Singleton;

    /// <inheritdoc />
    public IChangeToken Watch(string filter)
        => NullChangeToken.Singleton;

    /// <inheritdoc />
    public void Dispose()
    {
        lock (gate)
        {
            current?.Dispose();
            current = null;
            currentRoot = null;
        }
    }

    private PhysicalFileProvider? Resolve()
    {
        var root = settingsService.Recording.RecordingPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        lock (gate)
        {
            if (current is not null &&
                string.Equals(currentRoot, root, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            if (!Directory.Exists(root))
            {
                return null;
            }

            current?.Dispose();
            current = new PhysicalFileProvider(Path.GetFullPath(root));
            currentRoot = root;
            return current;
        }
    }
}