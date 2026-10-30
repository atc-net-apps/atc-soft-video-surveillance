namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Where the browser keeps its session between visits (local storage, shared
/// by every tab of the site), as the text <see cref="StoredSession"/> writes.
/// </summary>
public interface ISessionStorage
{
    /// <summary>
    /// Gets what is kept; <see langword="null"/> when nothing is.
    /// </summary>
    ValueTask<string?> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Keeps a session, in place of what was kept.
    /// </summary>
    ValueTask WriteAsync(
        string value,
        CancellationToken cancellationToken);

    /// <summary>
    /// Forgets what is kept, but only while it is still <paramref name="expected"/>
    /// (compared and removed in one step, so another tab's newer session stays).
    /// </summary>
    ValueTask RemoveIfAsync(
        string expected,
        CancellationToken cancellationToken);
}