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
    /// Gets the web client's page with its <c>&lt;base href&gt;</c> set to the path the server was
    /// reached under (<c>/surveillance/</c> behind a proxy; <c>/</c> at the root), so the client's
    /// files, pages and API calls stay below it.
    /// </summary>
    /// <param name="page">The page as built, with <c>&lt;base href="/" /&gt;</c>.</param>
    /// <param name="pathBase">The path the request came under.</param>
    /// <returns>The page to send.</returns>
    public static string WithBase(
        string page,
        PathString pathBase)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!pathBase.HasValue)
        {
            return page;
        }

        var href = System.Net.WebUtility.HtmlEncode(pathBase.Value!.TrimEnd('/') + "/");
        return page.Replace("<base href=\"/\" />", $"<base href=\"{href}\" />", StringComparison.Ordinal);
    }

    /// <summary>
    /// Marks an answer that depends on the encodings the browser accepts with
    /// <c>Vary: Accept-Encoding</c>, in place of the <c>Vary: Content-Encoding</c> the web client's
    /// compressed files are sent with.
    /// </summary>
    /// <param name="headers">The response headers.</param>
    public static void FixVary(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var vary = headers.Vary.ToString();
        if (vary.Length == 0 || !vary.Contains(Microsoft.Net.Http.Headers.HeaderNames.ContentEncoding, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var values = vary
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => string.Equals(v, Microsoft.Net.Http.Headers.HeaderNames.ContentEncoding, StringComparison.OrdinalIgnoreCase) ? Microsoft.Net.Http.Headers.HeaderNames.AcceptEncoding : v)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        headers.Vary = string.Join(", ", values);
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

        string page;
        using (var reader = new StreamReader(index.CreateReadStream()))
        {
            page = reader.ReadToEnd();
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
            await context.Response.WriteAsync(WithBase(page, context.Request.PathBase), context.RequestAborted).ConfigureAwait(false);
        });

        // The web client's files are sent compressed as the browser accepts (Brotli, gzip), but
        // marked "Vary: Content-Encoding" instead of "Vary: Accept-Encoding". A browser's cache then
        // hands a Brotli copy to a request that did not accept Brotli, which fails to decode, and
        // the client never starts (seen behind a proxy after an update). The answer is marked
        // with what it depends on.
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                FixVary(context.Response.Headers);
                return Task.CompletedTask;
            });

            return next(context);
        });

        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();
    }
}