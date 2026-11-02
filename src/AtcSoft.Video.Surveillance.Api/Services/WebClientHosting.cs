namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// The server serves its web client from its own address: the client's files, and its page for
/// every address a browser opens. The client's pages share addresses with the API (<c>/cameras</c>,
/// <c>/recordings</c>, …): a browser loading a page asks for HTML as a navigation
/// (<c>Sec-Fetch-Mode: navigate</c>), which the API never gets from its clients, so that is what
/// tells them apart.
/// </summary>
public static class WebClientHosting
{
    /// <summary>
    /// The setting that turns the web client off (<c>WebClient__Enabled=false</c>), for a server whose
    /// web client is hosted elsewhere. On by default.
    /// </summary>
    public const string EnabledKey = "WebClient:Enabled";

    // Addresses that are never the web client, whatever the request: the files a page or player
    // fetches itself, the event stream, the health checks and the API reference.
    private static readonly string[] NeverThePage =
    [
        "/streams",
        "/recordings-files",
        "/events",
        "/health",
        "/openapi",
        "/api-reference",
        "/_framework",
        "/_content",
    ];

    /// <summary>
    /// Gets a value indicating whether the server serves the web client.
    /// </summary>
    public static bool IsEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetValue(EnabledKey, defaultValue: true);
    }

    /// <summary>
    /// Gets a value indicating whether a request is a browser loading one of the web client's
    /// pages, which is answered with the client's <c>index.html</c>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> for a page load.</returns>
    public static bool IsPageLoad(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            return false;
        }

        var path = request.Path.Value ?? "/";
        if (Path.HasExtension(path) ||
            NeverThePage.Any(prefix => request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Every browser since 2020 says what a request is for; one that does not (an old browser)
        // is taken by what it accepts first.
        var mode = request.Headers["Sec-Fetch-Mode"].ToString();
        if (mode.Length > 0)
        {
            return string.Equals(mode, "navigate", StringComparison.OrdinalIgnoreCase);
        }

        var accept = request.Headers.Accept.ToString();
        return accept.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Serves the web client: its files, and its page for a browser's page load. Call before the
    /// API's endpoints are mapped.
    /// </summary>
    /// <param name="app">The server.</param>
    public static void UseWebClient(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var index = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
        if (!index.Exists)
        {
            return;
        }

        app.Use(async (context, next) =>
        {
            if (!IsPageLoad(context.Request))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            // Never kept by the browser: a new version of the server brings a new client.
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache, no-store";
            await context.Response.SendFileAsync(index, context.RequestAborted).ConfigureAwait(false);
        });

        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();
    }
}