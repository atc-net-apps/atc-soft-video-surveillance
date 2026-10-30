namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// The browser's local storage for the session (<c>atcSession</c> in session.js).
/// </summary>
public sealed class BrowserSessionStorage(IJSRuntime js) : ISessionStorage
{
    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken)
        => js.InvokeAsync<string?>("atcSession.get", cancellationToken);

    public ValueTask WriteAsync(
        string value,
        CancellationToken cancellationToken)
        => js.InvokeVoidAsync("atcSession.set", cancellationToken, value);

    public async ValueTask RemoveIfAsync(
        string expected,
        CancellationToken cancellationToken)
    {
        try
        {
            await js.InvokeVoidAsync("atcSession.clearIf", cancellationToken, expected).ConfigureAwait(false);
        }
        catch (JSException)
        {
            // An older session.js still in the browser's cache: the refused session is refused again next time.
        }
    }
}