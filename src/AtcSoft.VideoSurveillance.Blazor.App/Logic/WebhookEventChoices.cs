namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The events a webhook receiver can be given, as the page offers them: by
/// group. A single event can still be set through the API; the page shows
/// it and leaves it as it is.
/// </summary>
public static class WebhookEventChoices
{
    /// <summary>
    /// Gets the groups of events the server sends, with the name the page shows.
    /// </summary>
    public static IReadOnlyList<(string Pattern, string Label)> Groups { get; } =
    [
        ("camera.*", Translations.CameraConnection),
        ("recording.*", Translations.Recording),
        ("motion.*", Translations.Motion),
    ];

    /// <summary>
    /// Gets what a receiver gets, in words.
    /// </summary>
    /// <param name="events">The receiver's events; none means all.</param>
    /// <returns>The text for the list of receivers.</returns>
    public static string Describe(IReadOnlyCollection<string>? events)
    {
        var (groups, others) = Split(events);
        if ((groups.Count == 0 && others.Count == 0) ||
            (groups.Count == Groups.Count && others.Count == 0))
        {
            return Translations.AllEvents;
        }

        var names = Groups
            .Where(g => groups.Contains(g.Pattern))
            .Select(g => g.Label)
            .Concat(others);

        return string.Join(", ", names);
    }

    /// <summary>
    /// Tells the groups among a receiver's events apart from whatever else
    /// was set by hand.
    /// </summary>
    /// <param name="events">The receiver's events.</param>
    /// <returns>The group patterns, and the other entries in their order.</returns>
    public static (IReadOnlySet<string> Groups, IReadOnlyList<string> Others) Split(
        IReadOnlyCollection<string>? events)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var others = new List<string>();

        foreach (var entry in events ?? [])
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            var trimmed = entry.Trim();
            var group = Groups.FirstOrDefault(g => string.Equals(g.Pattern, trimmed, StringComparison.OrdinalIgnoreCase));
            if (group.Pattern is not null)
            {
                groups.Add(group.Pattern);
            }
            else
            {
                others.Add(trimmed);
            }
        }

        return (groups, others);
    }

    /// <summary>
    /// Gets the events to store for the ticked groups. Every group ticked
    /// (or none) is stored as no choice, which means all events, also ones
    /// the server learns to send later.
    /// </summary>
    /// <param name="tickedGroups">The group patterns that are ticked.</param>
    /// <param name="kept">Entries that were set by hand and are kept.</param>
    /// <returns>The events to send to the server.</returns>
    public static IReadOnlyList<string> ToEvents(
        IReadOnlyCollection<string> tickedGroups,
        IReadOnlyCollection<string> kept)
    {
        ArgumentNullException.ThrowIfNull(tickedGroups);
        ArgumentNullException.ThrowIfNull(kept);

        var ticked = Groups
            .Where(g => tickedGroups.Contains(g.Pattern, StringComparer.OrdinalIgnoreCase))
            .Select(g => g.Pattern)
            .ToList();

        if (kept.Count == 0 && (ticked.Count == 0 || ticked.Count == Groups.Count))
        {
            return [];
        }

        return [.. ticked, .. kept];
    }

    /// <summary>
    /// Gets whether an address is one the server accepts for a receiver.
    /// </summary>
    /// <param name="address">The address as typed.</param>
    /// <returns><see langword="true"/> for an http or https address.</returns>
    public static bool IsUsableAddress(string? address)
        => Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) &&
           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}