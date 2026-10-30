namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// The real server as its own process on a free port, in a scratch data folder, with its
/// first admin, a camera that is offline and (with FFmpeg) a live test camera. The WPF client
/// and the web client tests share it.
/// </summary>
public sealed class ServerFixture : IAsyncLifetime
{
    public const string AdminName = "e2e-admin";
    public const string AdminPassword = "E2E-smoke-test-1";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(90);

    private readonly List<LiveTestCamera> liveCameras = [];
    private ChildProcess? server;

    public Uri BaseUri { get; private set; } = new("http://127.0.0.1/");

    public string DataFolder { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the port the web client is served on; the server allows it as a CORS origin.
    /// </summary>
    public int WebClientPort { get; private set; }

    public bool HasLiveCamera { get; private set; }

    public string ServerOutputPath
        => server?.OutputPath ?? string.Empty;

    public async ValueTask InitializeAsync()
    {
        if (!E2ESettings.IsEnabled)
        {
            return;
        }

        DataFolder = E2ESettings.NewFolder("server");
        var port = E2ESettings.GetFreePort();
        WebClientPort = E2ESettings.GetFreePort();
        BaseUri = new Uri($"http://127.0.0.1:{port}");

        server = ChildProcess.Start(
            E2ESettings.ServerExe,
            ["--urls", BaseUri.ToString().TrimEnd('/')],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Storage__DataPath"] = DataFolder,
                ["Cors__AllowedOrigins__0"] = $"http://localhost:{WebClientPort}",
                ["Cors__AllowedOrigins__1"] = $"http://127.0.0.1:{WebClientPort}",
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
            },
            Path.Combine(E2ESettings.RunFolder, "server-output.log"));

        using var anonymous = new HttpClient { BaseAddress = BaseUri };
        await WaitForServerAsync(anonymous);
        await CreateAdminAsync(anonymous);

        using var admin = await CreateAdminClientAsync();
        await AddCamerasAsync(admin);
    }

    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = new HttpClient { BaseAddress = BaseUri };
        using var response = await client.PostAsJsonAsync("/auth/token", new { userName = AdminName, password = AdminPassword });
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        return client;
    }

    /// <summary>
    /// Gets the server's output lines a smoke test fails on (fatal entries, unhandled exceptions).
    /// </summary>
    public IReadOnlyList<string> ServerFailures()
        => server is null
            ? []
            : LogFindings.Failures(server.ReadOutput().Split('\n'));

    public ValueTask DisposeAsync()
    {
        foreach (var camera in liveCameras)
        {
            camera.Dispose();
        }

        server?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task WaitForServerAsync(HttpClient client)
    {
        var deadline = DateTime.UtcNow + StartTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (server!.HasExited)
            {
                throw new InvalidOperationException($"The server exited (code {server.Process.ExitCode}). Output: {server.OutputPath}");
            }

            try
            {
                using var response = await client.GetAsync(new Uri("/auth/setup", UriKind.Relative));
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

        throw new TimeoutException($"The server did not answer within {StartTimeout.TotalSeconds:N0} s. Output: {server!.OutputPath}");
    }

    private async Task CreateAdminAsync(HttpClient client)
    {
        var code = (await File.ReadAllTextAsync(Path.Combine(DataFolder, "setup-code.txt"))).Trim();
        using var response = await client.PostAsJsonAsync(
            "/auth/setup",
            new { setupCode = code, userName = AdminName, password = AdminPassword });
        response.EnsureSuccessStatusCode();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The camera is kept in the liveCameras list and disposed with the fixture.")]
    private async Task AddCamerasAsync(HttpClient admin)
    {
        await PostCameraAsync(admin, TestCameras.DeadCameraName, TestCameras.DeadPort);

        if (TestCameras.StartLiveCamera(
                E2ESettings.GetFreePort(),
                Path.Combine(E2ESettings.RunFolder, "server-test-camera.log")) is { } camera)
        {
            liveCameras.Add(camera);
            HasLiveCamera = true;
            var liveId = await PostCameraAsync(admin, TestCameras.LiveCameraName, camera.Port);
            await RecordAsync(admin, liveId);
        }

        using var layout = await admin.PostAsJsonAsync("/layouts", new { name = "E2E", rows = 2, columns = 2 });
        layout.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> PostCameraAsync(
        HttpClient admin,
        string name,
        int port)
    {
        using var response = await admin.PostAsJsonAsync(
            "/cameras",
            new { displayName = name, ipAddress = "127.0.0.1", port, protocol = "http", path = TestCameras.StreamPath });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Records a few seconds of the live camera, so the recordings views have a row.
    /// </summary>
    private static async Task RecordAsync(
        HttpClient admin,
        Guid cameraId)
    {
        using var started = await admin.PostAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), content: null);
        if (!started.IsSuccessStatusCode)
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(6));
        using var stopped = await admin.DeleteAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative));
    }
}