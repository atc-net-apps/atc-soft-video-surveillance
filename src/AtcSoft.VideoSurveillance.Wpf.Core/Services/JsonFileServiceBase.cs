namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// Abstract base class for services that persist a data model to a local JSON file.
/// Provides fault-tolerant <see cref="Load"/> and <see cref="Save"/> with a consistent
/// serialization strategy (write-indented, case-insensitive property names).
/// </summary>
/// <typeparam name="T">The data model type. Must have a parameterless constructor.</typeparam>
public abstract class JsonFileServiceBase<T>
    where T : new()
{
    [SuppressMessage("Design", "S2743", Justification = "Intentional: each closed generic type gets its own static options instance")]
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string filePath;
    private readonly string directory;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFileServiceBase{T}"/> class.
    /// </summary>
    /// <param name="filePath">Absolute path to the JSON file.</param>
    protected JsonFileServiceBase(string filePath)
    {
        this.filePath = filePath;
        directory = Path.GetDirectoryName(filePath)!;
    }

    /// <summary>
    /// Gets or sets the deserialized data model.
    /// </summary>
    protected T Data { get; set; } = new();

    /// <summary>
    /// Loads data from the JSON file. Falls back to a new <typeparamref name="T"/> on any error.
    /// </summary>
    public void Load()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Data = new T();
                OnLoaded();
                return;
            }

            var json = File.ReadAllText(filePath);
            Data = JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();
        }
        catch
        {
            Data = new T();
        }

        OnLoaded();
    }

    /// <summary>
    /// Saves data to the JSON file. Creates the directory if needed.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(Data, JsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // Fault-tolerant: silently fail on save errors
        }
    }

    /// <summary>
    /// Called after <see cref="Load"/> completes, regardless of success or fallback.
    /// Override to perform post-load logic such as computing derived state.
    /// </summary>
    protected virtual void OnLoaded()
    {
    }
}