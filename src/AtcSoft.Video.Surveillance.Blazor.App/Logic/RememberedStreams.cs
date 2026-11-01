namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// The cameras the viewer had playing in each layout, kept in the browser: after a reload (a
/// language change, the browser started again) the live view starts them again by itself. Only
/// what the viewer started or stopped counts; a stream that broke stays remembered.
/// </summary>
public sealed class RememberedStreams
{
    /// <summary>
    /// The key the browser keeps them under (local storage, per site).
    /// </summary>
    public const string StorageKey = "live-streams";

    private readonly Dictionary<Guid, HashSet<Guid>> byLayout = new();

    /// <summary>
    /// Reads what <see cref="ToText"/> wrote; anything else (nothing kept, an older shape, a
    /// damaged value) is an empty memory.
    /// </summary>
    /// <param name="text">The kept text.</param>
    /// <returns>The remembered streams.</returns>
    public static RememberedStreams Parse(string? text)
    {
        var remembered = new RememberedStreams();
        if (string.IsNullOrWhiteSpace(text))
        {
            return remembered;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<Guid, Guid[]>>(text);
            foreach (var (layoutId, cameraIds) in stored ?? [])
            {
                if (cameraIds is { Length: > 0 })
                {
                    remembered.byLayout[layoutId] = [.. cameraIds];
                }
            }
        }
        catch (JsonException)
        {
            // A damaged value: nothing is remembered.
        }

        return remembered;
    }

    /// <summary>
    /// Gets the cameras remembered for a layout.
    /// </summary>
    /// <param name="layoutId">The layout.</param>
    /// <returns>The cameras, none when nothing is remembered.</returns>
    public IReadOnlyCollection<Guid> For(Guid layoutId)
        => byLayout.TryGetValue(layoutId, out var cameraIds) ? cameraIds : [];

    /// <summary>
    /// Remembers that the viewer started a camera in a layout.
    /// </summary>
    /// <returns><see langword="true"/> when that is new (it is then to be kept).</returns>
    public bool Remember(
        Guid layoutId,
        Guid cameraId)
    {
        if (!byLayout.TryGetValue(layoutId, out var cameraIds))
        {
            cameraIds = [];
            byLayout[layoutId] = cameraIds;
        }

        return cameraIds.Add(cameraId);
    }

    /// <summary>
    /// Forgets a camera the viewer stopped in a layout.
    /// </summary>
    /// <returns><see langword="true"/> when it was remembered (the change is then to be kept).</returns>
    public bool Forget(
        Guid layoutId,
        Guid cameraId)
    {
        if (!byLayout.TryGetValue(layoutId, out var cameraIds) || !cameraIds.Remove(cameraId))
        {
            return false;
        }

        if (cameraIds.Count == 0)
        {
            byLayout.Remove(layoutId);
        }

        return true;
    }

    /// <summary>
    /// Gets the text to keep in the browser.
    /// </summary>
    /// <returns>The remembered streams as JSON.</returns>
    public string ToText()
        => JsonSerializer.Serialize(byLayout.ToDictionary(e => e.Key, e => e.Value.Order().ToArray()));
}