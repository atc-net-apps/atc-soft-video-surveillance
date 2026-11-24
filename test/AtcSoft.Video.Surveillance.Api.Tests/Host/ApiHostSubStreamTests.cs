namespace AtcSoft.Video.Surveillance.Api.Host;

/// <summary>
/// A camera with a sub-stream, the whole way through: the server analyses the small stream and
/// records the large one. The two streams come from the FFmpeg command-line tool, each on a port
/// of its own (the sub-stream's port); a machine without it skips the test. Nothing here asserts
/// how long anything took: the waits are generous, for a busy CI runner.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostSubStreamTests(ApiHostFixture host)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task A_Camera_With_A_Sub_Stream_Is_Analysed_On_The_Sub_Stream_And_Recorded_From_The_Main_Stream()
    {
        var ffmpeg = FindFFmpegTool();
        Assert.SkipWhen(ffmpeg is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = host.CreateClient();
        var mainPort = GetFreePort();
        var subPort = GetFreePort();
        using var main = StartTestStream(ffmpeg!, mainPort, "640x360");
        using var sub = StartTestStream(ffmpeg!, subPort, "160x90");
        var cameraId = Guid.Empty;

        try
        {
            // Arrange — the main stream on one port, the sub-stream on another
            var name = "Sub " + Guid.NewGuid().ToString("N")[..8];
            var created = await client.PostAsJsonAsync(
                "/cameras",
                new { displayName = name, ipAddress = "127.0.0.1", port = mainPort, protocol = "http", path = "live", subStreamPath = "live", subStreamPort = subPort },
                cancellationToken);
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(cancellationToken));
            var camera = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            cameraId = camera.GetProperty("id").GetGuid();
            camera.GetProperty("subStreamStatus").GetString().Should().Be("inUse");
            camera.GetProperty("subStreamPort").GetInt32().Should().Be(subPort);

            // Act
            var started = await client.PostAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), content: null, cancellationToken);
            started.StatusCode.Should().Be(HttpStatusCode.OK, await started.Content.ReadAsStringAsync(cancellationToken));
            host.GetService<ServerRecordingService>().FindPipeline(cameraId).Should().BeOfType<SubStreamMediaPipeline>();

            // Assert — the activity index is written from the sub-stream's pictures
            await WaitForActivityAsync(client, name, cancellationToken);

            var stopped = await client.DeleteAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), cancellationToken);
            stopped.StatusCode.Should().Be(HttpStatusCode.OK);

            // ... and the recording is of the main stream
            var recording = await FindRecordingAsync(client, name, cancellationToken);
            recording.Should().NotBeNull();
            var file = Path.Combine(host.DefaultRecordingPath, recording!.Value.GetProperty("filePath").GetString()!);
            Atc.Video.Engine.MediaProbe.GetDisplaySize(file).Should().Be((640, 360), "recordings are of the main stream");
        }
        finally
        {
            if (cameraId != Guid.Empty)
            {
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), CancellationToken.None);
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}", UriKind.Relative), CancellationToken.None);
            }

            Stop(main);
            Stop(sub);
        }
    }

    [Fact]
    public async Task The_Live_Grid_Plays_The_Sub_Stream()
    {
        // Only the sub-stream is served: the grid's stream plays only if it is the one opened.
        var ffmpeg = FindFFmpegTool();
        Assert.SkipWhen(ffmpeg is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = host.CreateClient();
        var mainPort = GetFreePort();
        var subPort = GetFreePort();
        using var sub = StartTestStream(ffmpeg!, subPort, "160x90");
        var cameraId = Guid.Empty;

        try
        {
            var created = await client.PostAsJsonAsync(
                "/cameras",
                new { displayName = "Grid " + Guid.NewGuid().ToString("N")[..8], ipAddress = "127.0.0.1", port = mainPort, protocol = "http", path = "live", subStreamPath = "live", subStreamPort = subPort },
                cancellationToken);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            cameraId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

            var lease = await client.PostAsync(new Uri($"/cameras/{cameraId}/stream?stream=sub", UriKind.Relative), content: null, cancellationToken);

            lease.StatusCode.Should().Be(HttpStatusCode.OK, await lease.Content.ReadAsStringAsync(cancellationToken));
            var leaseId = (await lease.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("leaseId").GetGuid();
            await client.DeleteAsync(new Uri($"/cameras/{cameraId}/stream/{leaseId}", UriKind.Relative), cancellationToken);
        }
        finally
        {
            if (cameraId != Guid.Empty)
            {
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}", UriKind.Relative), CancellationToken.None);
            }

            Stop(sub);
        }
    }

    [Fact]
    public async Task A_Request_With_A_Stream_The_Server_Does_Not_Know_Is_Answered_As_Before()
    {
        // A client from before sub-streams sends no stream; one from after sends one an older
        // server does not know. Here: the server answers a query it does not use as it would without.
        using var client = host.CreateClient();

        var answer = await client.PostAsync(
            new Uri($"/cameras/{Guid.NewGuid()}/stream?stream=sub&shape=wide", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        answer.StatusCode.Should().Be(HttpStatusCode.NotFound, "an unknown camera, not a bad request");
    }

    private static async Task WaitForActivityAsync(
        HttpClient client,
        string cameraName,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await FindRecordingAsync(client, cameraName, cancellationToken) is { } recording)
            {
                var filePath = recording.GetProperty("filePath").GetString()!;
                var activity = await client.GetFromJsonAsync<JsonElement>(
                    "/recordings/activity?buckets=10&filePath=" + Uri.EscapeDataString(filePath),
                    cancellationToken);
                if (activity.GetProperty("buckets").EnumerateArray().Any(b => b.GetProperty("isAnalysed").GetBoolean()))
                {
                    return;
                }
            }

            await Task.Delay(500, cancellationToken);
        }

        Assert.Fail($"No analysed activity for '{cameraName}' within {Timeout.TotalSeconds:N0} s.");
    }

    private static async Task<JsonElement?> FindRecordingAsync(
        HttpClient client,
        string cameraName,
        CancellationToken cancellationToken)
    {
        var recordings = await client.GetFromJsonAsync<JsonElement>("/recordings", cancellationToken);
        foreach (var recording in recordings.EnumerateArray())
        {
            if (recording.GetProperty("filePath").GetString()!.Contains(cameraName, StringComparison.Ordinal))
            {
                return recording;
            }
        }

        return null;
    }

    // A live-paced test pattern over HTTP, as an HTTP camera serves it, to one client.
    private static Process StartTestStream(
        string ffmpeg,
        int port,
        string size)
    {
        var startInfo = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };

        foreach (var argument in new[]
                 {
                     "-hide_banner", "-loglevel", "error",
                     "-re", "-f", "lavfi", "-i", $"testsrc=size={size}:rate=15",
                     "-c:v", "libx264", "-preset", "ultrafast", "-tune", "zerolatency",
                     "-g", "15", "-pix_fmt", "yuv420p",
                     "-t", "90",
                     "-f", "mpegts", "-listen", "1", $"http://127.0.0.1:{port}/live",
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The test stream could not be started.");
        process.ErrorDataReceived += (_, _) => { };
        process.BeginErrorReadLine();
        TestStreams.WaitUntilListening(port);
        return process;
    }

    private static string? FindFFmpegTool()
    {
        var folder = Atc.Video.Engine.VideoEngineBootstrap.FindFFmpegPath();
        if (folder is null)
        {
            return null;
        }

        var tool = Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        return File.Exists(tool) ? tool : null;
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
    }
}
