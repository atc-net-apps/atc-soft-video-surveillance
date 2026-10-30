// ReSharper disable RedundantArgumentDefaultValue
namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// JSON file-based implementation of <see cref="ICameraStorageService"/>.
/// </summary>
[Registration(Lifetime.Singleton)]
public partial class CameraStorageService : ICameraStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(),
            new CameraConfigurationJsonValueConverter(),
        },
    };

    private readonly string storagePath;
    private readonly ILogger<CameraStorageService> logger;
    private CameraStorageData data = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraStorageService"/> class.
    /// Uses the default storage location from <see cref="ApplicationPaths.DefaultCameraDataPath"/>.
    /// </summary>
    /// <param name="logger">The logger; a failed save is written to it.</param>
    public CameraStorageService(ILogger<CameraStorageService>? logger = null)
        : this(ApplicationPaths.DefaultCameraDataPath, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraStorageService"/> class
    /// with a custom storage path.
    /// </summary>
    /// <param name="storagePath">The path to the storage file.</param>
    /// <param name="logger">The logger; a failed save is written to it.</param>
    public CameraStorageService(
        string storagePath,
        ILogger<CameraStorageService>? logger = null)
    {
        this.storagePath = storagePath;
        this.logger = logger ?? NullLogger<CameraStorageService>.Instance;
        Load();
    }

    /// <inheritdoc/>
    public Guid? StartupLayoutId
    {
        get => data.StartupLayoutId;
        set
        {
            data.StartupLayoutId = value;
            Save();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<CameraConfiguration> GetAllCameras()
        => data.Cameras.AsReadOnly();

    /// <inheritdoc/>
    public CameraConfiguration? GetCameraById(Guid id)
        => data.Cameras.Find(c => c.Id == id);

    /// <inheritdoc/>
    public void AddOrUpdateCamera(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var existingIndex = data.Cameras.FindIndex(c => c.Id == camera.Id);
        if (existingIndex >= 0)
        {
            data.Cameras[existingIndex] = camera;
        }
        else
        {
            data.Cameras.Add(camera);
        }

        Save();
    }

    /// <inheritdoc/>
    public bool DeleteCamera(Guid id)
    {
        var camera = data.Cameras.Find(c => c.Id == id);
        if (camera is null)
        {
            return false;
        }

        data.Cameras.Remove(camera);

        // Remove camera from all layouts
        foreach (var layout in data.Layouts)
        {
            layout.Items.RemoveAll(item => item.CameraId == id);
        }

        Save();
        return true;
    }

    /// <inheritdoc/>
    public IReadOnlyList<CameraLayout> GetAllLayouts()
        => data.Layouts.AsReadOnly();

    /// <inheritdoc/>
    public CameraLayout? GetLayoutById(Guid id)
        => data.Layouts.Find(l => l.Id == id);

    /// <inheritdoc/>
    public void AddOrUpdateLayout(CameraLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var existingIndex = data.Layouts.FindIndex(l => l.Id == layout.Id);
        if (existingIndex >= 0)
        {
            layout.ModifiedAt = DateTime.UtcNow;
            data.Layouts[existingIndex] = layout;
        }
        else
        {
            data.Layouts.Add(layout);
        }

        Save();
    }

    /// <inheritdoc/>
    public bool DeleteLayout(Guid id)
    {
        var layout = data.Layouts.Find(l => l.Id == id);
        if (layout is null)
        {
            return false;
        }

        data.Layouts.Remove(layout);

        // Clear startup layout if deleted
        if (data.StartupLayoutId == id)
        {
            data.StartupLayoutId = null;
        }

        Save();
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Camera passwords are written encrypted (<see cref="StoredSecret"/>); the cameras in memory
    /// keep their plain passwords.
    /// </remarks>
    public void Save()
    {
        var json = JsonSerializer.SerializeToNode(data, JsonOptions);
        _ = TransformPasswords(json, StoredSecret.Protect);

        if (json is null || !SafeJsonFile.TryWrite(storagePath, json, JsonOptions))
        {
            LogSaveFailed(storagePath);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Encrypted passwords are decrypted. A file with plain passwords (written before they were
    /// encrypted) is written again at once, twice, so the backup copy holds no plain password either.
    /// </remarks>
    public void Load()
    {
        var json = SafeJsonFile.TryRead<JsonNode>(storagePath, JsonOptions);
        if (json is null)
        {
            data = new CameraStorageData();
            return;
        }

        var hadPlainPasswords = HasPlainPassword(json);
        _ = TransformPasswords(json, StoredSecret.Unprotect);
        data = json.Deserialize<CameraStorageData>(JsonOptions) ?? new CameraStorageData();

        if (hadPlainPasswords)
        {
            Save();
            Save();
        }
    }

    // Every "password" in the file, whatever the format version put it in.
    private static bool TransformPasswords(
        JsonNode? node,
        Func<string?, string?> transform)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var (name, value) in jsonObject.ToList())
                {
                    if (IsPassword(name, value, out var text))
                    {
                        var transformed = transform(text);
                        if (!string.Equals(transformed, text, StringComparison.Ordinal))
                        {
                            jsonObject[name] = transformed is null ? null : JsonValue.Create(transformed);
                            changed = true;
                        }
                    }
                    else
                    {
                        changed |= TransformPasswords(value, transform);
                    }
                }

                break;
            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    changed |= TransformPasswords(item, transform);
                }

                break;
        }

        return changed;
    }

    private static bool HasPlainPassword(JsonNode? node)
        => node switch
        {
            JsonObject jsonObject => jsonObject.Any(p =>
                IsPassword(p.Key, p.Value, out var text)
                    ? !string.IsNullOrEmpty(text) && !StoredSecret.IsProtected(text)
                    : HasPlainPassword(p.Value)),
            JsonArray jsonArray => jsonArray.Any(HasPlainPassword),
            _ => false,
        };

    private static bool IsPassword(
        string name,
        JsonNode? value,
        out string? text)
    {
        text = null;
        return string.Equals(name, "password", StringComparison.OrdinalIgnoreCase) &&
               value is JsonValue jsonValue &&
               jsonValue.TryGetValue(out text);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The cameras could not be saved to '{StoragePath}'; changes since the last save are lost when the app closes")]
    private partial void LogSaveFailed(string storagePath);
}