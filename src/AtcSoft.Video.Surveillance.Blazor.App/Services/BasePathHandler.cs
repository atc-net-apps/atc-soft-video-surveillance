namespace AtcSoft.Video.Surveillance.Blazor.App.Services;

/// <summary>
/// Sends every API request below the server's path when it lives under one of a proxy's site
/// (<see cref="BasePath"/>). The innermost handler: the credentials and the retries see the
/// operation's own path.
/// </summary>
public sealed class BasePathHandler(Uri baseAddress) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestUri is { } uri)
        {
            request.RequestUri = BasePath.Keep(baseAddress, uri);
        }

        return base.SendAsync(request, cancellationToken);
    }
}