namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// JSON file-based implementation of <see cref="ICameraStorageService"/> for the server edition.
/// The cameras' passwords are encrypted in the file only: the cameras in memory
/// keep them as they are, since the streams need them.
/// </summary>
public sealed partial class JsonCameraStorageService : ICameraStorageService
{
    private const string PasswordProperty = "password";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string storagePath;
    private readonly SecretProtector secrets;
    private readonly ILogger<JsonCameraStorageService> logger;

    // All access to data.Cameras / data.Layouts goes through this lock.
    // Without it, CameraConnectionService / handlers iterating GetAllCameras()
    // would throw InvalidOperationException if a REST UpdateCamera mutated
    // the list mid-iteration. Reads return snapshots so callers can iterate
    // outside the lock without blocking writers.
    private readonly Lock dataLock = new();
    private CameraStorageData data = new();

    public JsonCameraStorageService(
        string storagePath,
        SecretProtector secrets,
        ILogger<JsonCameraStorageService> logger)
    {
        this.storagePath = storagePath;
        this.secrets = secrets;
        this.logger = logger;
        Load();
    }

    /// <inheritdoc/>
    public Guid? StartupLayoutId
    {
        get
        {
            lock (dataLock)
            {
                return data.StartupLayoutId;
            }
        }

        set
        {
            lock (dataLock)
            {
                data.StartupLayoutId = value;
                SaveLocked();
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<CameraConfiguration> GetAllCameras()
    {
        lock (dataLock)
        {
            return data.Cameras.ToList();
        }
    }

    /// <inheritdoc/>
    public CameraConfiguration? GetCameraById(Guid id)
    {
        lock (dataLock)
        {
            return data.Cameras.Find(c => c.Id == id);
        }
    }

    /// <inheritdoc/>
    public void AddOrUpdateCamera(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        lock (dataLock)
        {
            var existingIndex = data.Cameras.FindIndex(c => c.Id == camera.Id);
            if (existingIndex >= 0)
            {
                data.Cameras[existingIndex] = camera;
            }
            else
            {
                data.Cameras.Add(camera);
            }

            SaveLocked();
        }
    }

    /// <inheritdoc/>
    public bool DeleteCamera(Guid id)
    {
        lock (dataLock)
        {
            var camera = data.Cameras.Find(c => c.Id == id);
            if (camera is null)
            {
                return false;
            }

            data.Cameras.Remove(camera);

            foreach (var layout in data.Layouts)
            {
                layout.Items.RemoveAll(item => item.CameraId == id);
            }

            SaveLocked();
            return true;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<CameraLayout> GetAllLayouts()
    {
        lock (dataLock)
        {
            return data.Layouts.ToList();
        }
    }

    /// <inheritdoc/>
    public CameraLayout? GetLayoutById(Guid id)
    {
        lock (dataLock)
        {
            return data.Layouts.Find(l => l.Id == id);
        }
    }

    /// <inheritdoc/>
    public void AddOrUpdateLayout(CameraLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        lock (dataLock)
        {
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

            SaveLocked();
        }
    }

    /// <inheritdoc/>
    public bool DeleteLayout(Guid id)
    {
        lock (dataLock)
        {
            var layout = data.Layouts.Find(l => l.Id == id);
            if (layout is null)
            {
                return false;
            }

            data.Layouts.Remove(layout);

            if (data.StartupLayoutId == id)
            {
                data.StartupLayoutId = null;
            }

            SaveLocked();
            return true;
        }
    }

    /// <inheritdoc/>
    public void Save()
    {
        lock (dataLock)
        {
            SaveLocked();
        }
    }

    /// <inheritdoc/>
    public void Load()
    {
        var node = SafeJsonFile.TryRead<JsonNode>(storagePath, JsonOptions);
        var plainCount = node is null ? 0 : UnprotectPasswords(node);
        var loaded = node?.Deserialize<CameraStorageData>(JsonOptions);
        lock (dataLock)
        {
            if (loaded is not null)
            {
                data = loaded;
                LogStorageLoaded(data.Cameras.Count, data.Layouts.Count, storagePath);
            }
            else
            {
                data = new CameraStorageData();
            }

            if (plainCount > 0)
            {
                // Twice: the first save keeps the old file, plain passwords
                // and all, as the backup; the second replaces that too.
                SaveLocked();
                SaveLocked();
                LogPasswordsEncrypted(plainCount, storagePath);
            }
        }
    }

    // Caller must already hold dataLock. Extracted so the public mutators
    // don't have to acquire-release-reacquire when they need to persist.
    private void SaveLocked()
    {
        var node = JsonSerializer.SerializeToNode(data, JsonOptions)!;
        foreach (var authentication in Authentications(node))
        {
            authentication[PasswordProperty] = secrets.Protect(authentication[PasswordProperty]?.GetValue<string>());
        }

        if (!SafeJsonFile.TryWrite(storagePath, node, JsonOptions))
        {
            LogStorageSaveFailed(new IOException("Atomic save failed"), storagePath);
        }
    }

    // Reads the passwords in a file as it was read; returns how many were
    // written before encryption. One this server cannot read is left out.
    private int UnprotectPasswords(JsonNode node)
    {
        var plainCount = 0;
        foreach (var authentication in Authentications(node))
        {
            var stored = authentication[PasswordProperty]?.GetValue<string>();
            if (!secrets.TryUnprotect(stored, out var password, out var wasPlain))
            {
                var camera = authentication.Parent?.AsObject();
                LogPasswordUnreadable(
                    camera?["id"]?.GetValue<string>() ?? string.Empty,
                    camera?["display"]?["displayName"]?.GetValue<string>() ?? string.Empty);
            }

            if (wasPlain)
            {
                plainCount++;
            }

            authentication[PasswordProperty] = password;
        }

        return plainCount;
    }

    private static IEnumerable<JsonObject> Authentications(JsonNode node)
        => node["cameras"]?.AsArray()
               .Select(camera => camera?["authentication"])
               .OfType<JsonObject>()
           ?? [];
}