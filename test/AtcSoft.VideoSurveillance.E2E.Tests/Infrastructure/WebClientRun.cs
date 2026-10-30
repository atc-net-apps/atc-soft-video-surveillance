namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// The web client served by its development server on the port the test server allows as a
/// CORS origin. The browser is pointed at the test server by answering the app's
/// appsettings.json request (see <see cref="AppSettingsJson"/>).
/// </summary>
internal sealed class WebClientRun : IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(90);

    private WebClientRun(
        ChildProcess process,
        Uri baseUri)
    {
        Process = process;
        BaseUri = baseUri;
    }

    public ChildProcess Process { get; }

    public Uri BaseUri { get; }

    public static async Task<WebClientRun> StartAsync(ServerFixture server)
    {
        ArgumentNullException.ThrowIfNull(server);

        var baseUri = new Uri($"http://localhost:{server.WebClientPort}/");
        var process = ChildProcess.Start(
            "dotnet",
            ["run", "--no-build", "--no-launch-profile", "--configuration", E2ESettings.Configuration, "--urls", baseUri.ToString().TrimEnd('/')],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
            },
            Path.Combine(E2ESettings.RunFolder, "web-client-output.log"),
            E2ESettings.BlazorProjectFolder);

        var run = new WebClientRun(process, baseUri);
        try
        {
            await run.WaitUntilServedAsync();
        }
        catch
        {
            run.Dispose();
            throw;
        }

        return run;
    }

    /// <summary>
    /// Gets the configuration the web client reads at start: the test server's address.
    /// </summary>
    public static string AppSettingsJson(ServerFixture server)
    {
        ArgumentNullException.ThrowIfNull(server);

        return JsonSerializer.Serialize(new { ApiBaseAddress = server.BaseUri.ToString().TrimEnd('/') });
    }

    public void Dispose()
        => Process.Dispose();

    private async Task WaitUntilServedAsync()
    {
        using var client = new HttpClient { BaseAddress = BaseUri };
        var deadline = DateTime.UtcNow + StartTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Process.HasExited)
            {
                throw new InvalidOperationException($"The web client's server exited (code {Process.Process.ExitCode}). Output: {Process.OutputPath}");
            }

            try
            {
                using var response = await client.GetAsync(new Uri("/", UriKind.Relative));
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"The web client was not served within {StartTimeout.TotalSeconds:N0} s. Output: {Process.OutputPath}");
    }
}