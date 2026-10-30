namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// Runs the real server in-process against a data folder of its own, so the
/// tests go through HTTP, routing, serialization and the real services
/// without touching the machine's own server data.
/// </summary>
public sealed class ApiHostFixture : IDisposable
{
    /// <summary>
    /// The secret the server signs its webhooks with in the host tests.
    /// </summary>
    public const string WebhookSecret = "host-test-secret";

    /// <summary>
    /// The first admin the host tests create with the setup code.
    /// </summary>
    public const string AdminName = "admin";

    /// <summary>
    /// The first admin's password.
    /// </summary>
    public const string AdminPassword = "host-test-password";

    /// <summary>
    /// A request header the host tests set the caller's address with (for the rate limits).
    /// </summary>
    public const string TestAddressHeader = "X-Test-Remote-Address";

    private readonly Lazy<WebApplicationFactory<Program>> factory;
    private readonly SemaphoreSlim setup = new(1, 1);
    private string? adminAccessToken;
    private int signInAddress;

    public ApiHostFixture()
    {
        DataPath = Path.Combine(Path.GetTempPath(), "atcsoft-api-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataPath);

        // Read by the server before its host is built (logging set-up) and
        // again by its configuration.
        Environment.SetEnvironmentVariable("Storage__DataPath", DataPath);

        // The server posts its camera events here.
        Webhooks = new WebhookReceiver();
        Environment.SetEnvironmentVariable("Webhooks__Endpoints__0__Url", Webhooks.Address);
        Environment.SetEnvironmentVariable("Webhooks__Endpoints__0__Secret", WebhookSecret);

        factory = new Lazy<WebApplicationFactory<Program>>(() => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IStartupFilter, TestAddressStartupFilter>())));
    }

    /// <summary>
    /// Gets the folder the server keeps its settings, cameras and media in.
    /// </summary>
    public string DataPath { get; }

    /// <summary>
    /// Gets the receiver of the server's webhooks.
    /// </summary>
    public WebhookReceiver Webhooks { get; }

    /// <summary>
    /// Gets the folder recordings are stored in when the settings are untouched.
    /// </summary>
    public string DefaultRecordingPath => Path.Combine(DataPath, "recordings");

    /// <summary>
    /// Gets a client for the running server, signed in as its first admin.
    /// </summary>
    public HttpClient CreateClient()
    {
        var token = GetAdminAccessToken();
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Gets a client for the running server that is not signed in. The server
    /// needs the FFmpeg shared libraries to start, so a machine without them
    /// skips the test.
    /// </summary>
    public HttpClient CreateAnonymousClient()
    {
        Assert.SkipWhen(
            AtcSoft.VideoEngine.VideoEngineBootstrap.FindFFmpegPath() is null,
            "FFmpeg shared libraries are not installed.");

        var client = factory.Value.CreateClient();

        // Never let a test write into the machine's own server data: if the
        // server did not take the data folder, stop here.
        if (!File.Exists(Path.Combine(DataPath, "settings.json")))
        {
            client.Dispose();
            throw new InvalidOperationException(
                $"The server did not start in the test data folder '{DataPath}'; refusing to run host tests against its real data.");
        }

        return client;
    }

    /// <summary>
    /// Makes sure the server has its first admin (<see cref="AdminName"/>),
    /// made with the setup code the server wrote into its data folder.
    /// </summary>
    public async Task EnsureAdminAsync()
    {
        using var client = CreateAnonymousClient();
        var acquired = false;
        try
        {
            await setup.WaitAsync(TestContext.Current.CancellationToken);
            acquired = true;

            var status = await client.GetFromJsonAsync<JsonElement>("/auth/setup", TestContext.Current.CancellationToken);
            if (!status.GetProperty("setupRequired").GetBoolean())
            {
                return;
            }

            var code = await File.ReadAllTextAsync(Path.Combine(DataPath, "setup-code.txt"), TestContext.Current.CancellationToken);
            var created = await client.PostAsJsonAsync(
                "/auth/setup",
                new { setupCode = code.Trim(), userName = AdminName, password = AdminPassword },
                TestContext.Current.CancellationToken);
            created.EnsureSuccessStatusCode();
        }
        finally
        {
            if (acquired)
            {
                setup.Release();
            }
        }
    }

    /// <summary>
    /// Signs in through the running server, from an address of its own each
    /// time, so the sign-ins of the tests do not use up each other's limit.
    /// </summary>
    /// <returns>The status and the answer's body.</returns>
    public async Task<(int Status, JsonElement Body)> SignInAsync(
        string userName,
        string password)
    {
        var address = "198.51.100." + ((Interlocked.Increment(ref signInAddress) % 250) + 1).ToString(CultureInfo.InvariantCulture);
        using var client = CreateAnonymousClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/token")
        {
            Content = JsonContent.Create(new { userName, password }),
        };
        request.Headers.Add(TestAddressHeader, address);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return ((int)response.StatusCode, body);
    }

    /// <summary>
    /// Gets a client for the running server that is signed in as its first admin.
    /// </summary>
    public Task<HttpClient> CreateAdminClientAsync()
        => Task.FromResult(CreateClient());

    /// <summary>
    /// Gets the first admin's access token (signed in once, then kept).
    /// </summary>
    public string GetAdminAccessToken()
    {
        if (adminAccessToken is null)
        {
            EnsureAdminAsync().GetAwaiter().GetResult();
            var (status, session) = SignInAsync(AdminName, AdminPassword).GetAwaiter().GetResult();
            status.Should().Be(StatusCodes.Status200OK, session.ToString());
            adminAccessToken = session.GetProperty("accessToken").GetString();
        }

        return adminAccessToken!;
    }

    /// <summary>
    /// Gets a service of the running server.
    /// </summary>
    /// <typeparam name="T">The service.</typeparam>
    public T GetService<T>()
        where T : notnull
    {
        // Makes sure the server is running in the test data folder.
        using var client = CreateAnonymousClient();

        return factory.Value.Services.GetRequiredService<T>();
    }

    /// <summary>
    /// Sends one request to the running server, shaped on its context: for
    /// what a client cannot set, such as the address it calls from.
    /// </summary>
    public async Task<HttpContext> SendAsync(Action<HttpContext> configure)
    {
        // Makes sure the server is running in the test data folder.
        using var client = CreateAnonymousClient();

        return await factory.Value.Server.SendAsync(configure);
    }

    public void Dispose()
    {
        if (factory.IsValueCreated)
        {
            factory.Value.Dispose();
        }

        Environment.SetEnvironmentVariable("Storage__DataPath", null);
        Environment.SetEnvironmentVariable("Webhooks__Endpoints__0__Url", null);
        Environment.SetEnvironmentVariable("Webhooks__Endpoints__0__Secret", null);
        Webhooks.DisposeAsync().AsTask().GetAwaiter().GetResult();
        setup.Dispose();

        try
        {
            Directory.Delete(DataPath, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a temp folder left behind is harmless.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }

    // Ahead of the server's own middleware: a request that names an address
    // comes from it, as a rate limit sees it.
    private sealed class TestAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(
            Action<IApplicationBuilder> next)
            => app =>
            {
                app.Use((context, nextMiddleware) =>
                {
                    if (context.Request.Headers.TryGetValue(TestAddressHeader, out var value) &&
                        IPAddress.TryParse(value.ToString(), out var address))
                    {
                        context.Connection.RemoteIpAddress = address;
                    }

                    return nextMiddleware(context);
                });
                next(app);
            };
    }
}