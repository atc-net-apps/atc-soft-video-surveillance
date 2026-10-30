namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// The whole way through with a real stream: the server opens a camera,
/// records it, makes the thumbnail and serves both. The stream comes from
/// the FFmpeg command-line tool; a machine without it skips the test.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostLiveStreamTests(ApiHostFixture host)
{
    private static readonly TimeSpan ThumbnailTimeout = TimeSpan.FromSeconds(40);

    [Fact]
    public async Task A_Recorded_Camera_Gets_A_Thumbnail_That_Is_Listed_And_Served()
    {
        var ffmpeg = FindFFmpegTool();
        Assert.SkipWhen(ffmpeg is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = host.CreateClient();
        var port = GetFreePort();
        using var stream = StartTestStream(ffmpeg!, port);
        var cameraId = Guid.Empty;

        try
        {
            // Arrange — a camera that is the test stream, rotated a quarter turn
            var name = "Live " + Guid.NewGuid().ToString("N")[..8];
            var created = await client.PostAsJsonAsync(
                "/cameras",
                new { displayName = name, ipAddress = "127.0.0.1", port, protocol = "http", path = "live", rotation = "rotate90" },
                cancellationToken);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            cameraId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

            // Act
            var started = await client.PostAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), content: null, cancellationToken);
            started.StatusCode.Should().Be(HttpStatusCode.OK, await started.Content.ReadAsStringAsync(cancellationToken));

            var listed = await WaitForThumbnailAsync(client, name, cancellationToken);

            // Assert — the thumbnail is where the layout puts it, and is a picture
            var thumbnailPath = listed.GetProperty("thumbnailPath").GetString()!;
            thumbnailPath.Should().StartWith($"thumbnails/{name}/").And.EndWith(".png");
            listed.GetProperty("filePath").GetString().Should().StartWith($"{name}/").And.NotContain(":", "clients get a path below the recordings root");

            var thumbnail = await client.GetAsync(
                new Uri("/recordings-files/" + RecordingPathHelper.EscapePathSegments(thumbnailPath), UriKind.Relative),
                cancellationToken);
            thumbnail.StatusCode.Should().Be(HttpStatusCode.OK);
            var png = await thumbnail.Content.ReadAsByteArrayAsync(cancellationToken);
            png.Take(8).Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
            png.Length.Should().BeGreaterThan(1000, "four tiles of a test pattern are not an empty picture");

            // The recording stops cleanly and stays listed with its thumbnail
            var stopped = await client.DeleteAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), cancellationToken);
            stopped.StatusCode.Should().Be(HttpStatusCode.OK);

            var afterStop = await FindRecordingAsync(client, name, cancellationToken);
            afterStop!.Value.GetProperty("thumbnailPath").GetString().Should().Be(thumbnailPath);
            afterStop.Value.GetProperty("fileSizeBytes").GetInt64().Should().BeGreaterThan(0);
            afterStop.Value.GetProperty("inProgress").GetBoolean().Should().BeFalse();
            afterStop.Value.GetProperty("duration").GetString().Should().StartWith("PT", "a finished recording has its length");

            // The start and the stop were posted to the webhook receiver, signed
            var startedHook = await WaitForWebhookAsync("recording.started", cameraId, cancellationToken);
            var stoppedHook = await WaitForWebhookAsync("recording.stopped", cameraId, cancellationToken);
            startedHook.Signature.Should().Be(AtcSoft.VideoSurveillance.Api.Webhooks.WebhookSignature.Compute(ApiHostFixture.WebhookSecret, System.Text.Encoding.UTF8.GetBytes(startedHook.Body)));
            using var stoppedBody = JsonDocument.Parse(stoppedHook.Body);
            stoppedBody.RootElement.GetProperty("cameraName").GetString().Should().Be(name);
            stoppedBody.RootElement.GetProperty("data").GetProperty("filePath").GetString()
                .Should().Be(afterStop.Value.GetProperty("filePath").GetString(), "a receiver can fetch the file by the path it was told");
        }
        finally
        {
            if (cameraId != Guid.Empty)
            {
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), CancellationToken.None);
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}", UriKind.Relative), CancellationToken.None);
            }

            Stop(stream);
        }
    }

    [Fact]
    public async Task A_Snapshot_Of_A_Camera_Being_Recorded_Comes_From_The_Recording_Turned_As_Shown()
    {
        var ffmpeg = FindFFmpegTool();
        Assert.SkipWhen(ffmpeg is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = host.CreateClient();
        var port = GetFreePort();
        using var stream = StartTestStream(ffmpeg!, port);
        var cameraId = Guid.Empty;

        try
        {
            // Arrange — a 320 x 240 camera turned a quarter, recorded. The test stream serves one
            // client, the recording, so a picture can only come from the recording.
            var name = "Snapshot " + Guid.NewGuid().ToString("N")[..8];
            var created = await client.PostAsJsonAsync(
                "/cameras",
                new { displayName = name, ipAddress = "127.0.0.1", port, protocol = "http", path = "live", rotation = "rotate90" },
                cancellationToken);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            cameraId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
            var started = await client.PostAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), content: null, cancellationToken);
            started.StatusCode.Should().Be(HttpStatusCode.OK, await started.Content.ReadAsStringAsync(cancellationToken));
            await WaitForActivityAsync(client, name, cancellationToken);

            // Act
            using var snapshot = await client.GetAsync(new Uri($"/cameras/{cameraId}/snapshot", UriKind.Relative), cancellationToken);

            // Assert — a PNG of the picture as shown: 240 wide, 320 high
            snapshot.StatusCode.Should().Be(HttpStatusCode.OK, await snapshot.Content.ReadAsStringAsync(cancellationToken));
            var png = await snapshot.Content.ReadAsByteArrayAsync(cancellationToken);
            png.Take(8).Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)).Should().Be(240);
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)).Should().Be(320);
        }
        finally
        {
            if (cameraId != Guid.Empty)
            {
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), CancellationToken.None);
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}", UriKind.Relative), CancellationToken.None);
            }

            Stop(stream);
        }
    }

    [Fact]
    public async Task A_Recording_Gets_An_Activity_Index_That_Is_Served_While_It_Grows()
    {
        var ffmpeg = FindFFmpegTool();
        Assert.SkipWhen(ffmpeg is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = host.CreateClient();
        var port = GetFreePort();
        using var stream = StartTestStream(ffmpeg!, port);
        var cameraId = Guid.Empty;

        try
        {
            // Arrange — a camera that is recorded but not watched for motion
            var name = "Activity " + Guid.NewGuid().ToString("N")[..8];
            var created = await client.PostAsJsonAsync(
                "/cameras",
                new { displayName = name, ipAddress = "127.0.0.1", port, protocol = "http", path = "live" },
                cancellationToken);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            cameraId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

            // Act
            var started = await client.PostAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), content: null, cancellationToken);
            started.StatusCode.Should().Be(HttpStatusCode.OK, await started.Content.ReadAsStringAsync(cancellationToken));

            // Assert — the index is there while recording, and analysed seconds show up in it
            var activity = await WaitForActivityAsync(client, name, cancellationToken);
            activity.GetProperty("hasIndex").GetBoolean().Should().BeTrue();
            activity.GetProperty("startedAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
            activity.GetProperty("buckets").GetArrayLength().Should().Be(10);

            // The index is a companion of the recording: it goes with it
            await client.DeleteAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), cancellationToken);
            var filePath = (await FindRecordingAsync(client, name, cancellationToken))!.Value.GetProperty("filePath").GetString()!;
            var recordingPath = (await client.GetFromJsonAsync<JsonElement>("/settings", cancellationToken)).GetProperty("recordingPath").GetString()!;
            var activityFile = RecordingPathHelper.BuildActivityPath(Path.Combine(recordingPath, filePath));
            File.Exists(activityFile).Should().BeTrue();

            var deleted = await client.DeleteAsync(new Uri("/recordings?filePath=" + Uri.EscapeDataString(filePath), UriKind.Relative), cancellationToken);
            deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
            File.Exists(activityFile).Should().BeFalse();
        }
        finally
        {
            if (cameraId != Guid.Empty)
            {
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), CancellationToken.None);
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}", UriKind.Relative), CancellationToken.None);
            }

            Stop(stream);
        }
    }

    [Fact]
    public async Task A_Recording_That_Ends_Gets_Previews_That_Are_Served_And_Go_With_It()
    {
        var ffmpeg = FindFFmpegTool();
        Assert.SkipWhen(ffmpeg is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = host.CreateClient();
        var port = GetFreePort();
        using var stream = StartTestStream(ffmpeg!, port);
        var cameraId = Guid.Empty;

        try
        {
            // Arrange — a recording of a few seconds
            var name = "Previews " + Guid.NewGuid().ToString("N")[..8];
            var created = await client.PostAsJsonAsync(
                "/cameras",
                new { displayName = name, ipAddress = "127.0.0.1", port, protocol = "http", path = "live" },
                cancellationToken);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            cameraId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
            var started = await client.PostAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), content: null, cancellationToken);
            started.StatusCode.Should().Be(HttpStatusCode.OK, await started.Content.ReadAsStringAsync(cancellationToken));
            await WaitForActivityAsync(client, name, cancellationToken);
            var filePath = (await FindRecordingAsync(client, name, cancellationToken))!.Value.GetProperty("filePath").GetString()!;
            var query = "/recordings/previews?filePath=" + Uri.EscapeDataString(filePath);
            var whileRecording = await client.GetFromJsonAsync<JsonElement>(query, cancellationToken);

            // Act — the recording ends
            await client.DeleteAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), cancellationToken);

            // Assert — none while it is written; made in the background after it ends, and served
            whileRecording.GetProperty("hasPreviews").GetBoolean().Should().BeFalse();
            whileRecording.GetProperty("isBeingMade").GetBoolean().Should().BeFalse("a recording still being written is not queued");

            var sheet = await WaitForPreviewsAsync(client, query, cancellationToken);
            sheet.GetProperty("gridCount").GetInt32().Should().BePositive();
            sheet.GetProperty("tileWidth").GetInt32().Should().Be(160);
            var image = await client.GetAsync(
                new Uri("/recordings-files/" + RecordingPathHelper.EscapePathSegments(sheet.GetProperty("imagePath").GetString()!), UriKind.Relative),
                cancellationToken);
            image.StatusCode.Should().Be(HttpStatusCode.OK);
            image.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");

            // The previews are companions of the recording: they go with it
            var recordingPath = (await client.GetFromJsonAsync<JsonElement>("/settings", cancellationToken)).GetProperty("recordingPath").GetString()!;
            var previewImage = RecordingPathHelper.BuildPreviewImagePath(Path.Combine(recordingPath, filePath));
            File.Exists(previewImage).Should().BeTrue();
            var deleted = await client.DeleteAsync(new Uri("/recordings?filePath=" + Uri.EscapeDataString(filePath), UriKind.Relative), cancellationToken);
            deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
            File.Exists(previewImage).Should().BeFalse();
        }
        finally
        {
            if (cameraId != Guid.Empty)
            {
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), CancellationToken.None);
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}", UriKind.Relative), CancellationToken.None);
            }

            Stop(stream);
        }
    }

    [Fact]
    public async Task The_Timeline_Covers_A_Recording_Across_Its_Segments_Without_A_Gap()
    {
        var ffmpeg = FindFFmpegTool();
        Assert.SkipWhen(ffmpeg is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = host.CreateClient();
        var port = GetFreePort();
        using var stream = StartTestStream(ffmpeg!, port);
        var cameraId = Guid.Empty;

        try
        {
            // Arrange — a camera recorded into two segments
            var name = "Timeline " + Guid.NewGuid().ToString("N")[..8];
            var created = await client.PostAsJsonAsync(
                "/cameras",
                new { displayName = name, ipAddress = "127.0.0.1", port, protocol = "http", path = "live" },
                cancellationToken);
            cameraId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
            var from = DateTimeOffset.UtcNow.AddMinutes(-1);

            var started = await client.PostAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), content: null, cancellationToken);
            started.StatusCode.Should().Be(HttpStatusCode.OK, await started.Content.ReadAsStringAsync(cancellationToken));
            await WaitForActivityAsync(client, name, cancellationToken);
            host.GetService<IRecordingService>().SegmentRecording(cameraId).Should().BeTrue("the recording goes on in a new file");
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            await client.DeleteAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), cancellationToken);
            var to = DateTimeOffset.UtcNow.AddMinutes(1);

            // Act
            var timeline = await client.GetFromJsonAsync<JsonElement>(
                $"/timeline?cameraId={cameraId}&buckets=120&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}",
                cancellationToken);

            // Assert — one lane, both files, recorded from the first file's start to the second's end
            var lane = timeline.GetProperty("lanes").EnumerateArray().Should().ContainSingle().Subject;
            List<JsonElement> coverage = [.. lane.GetProperty("coverage").EnumerateArray()];
            coverage.Should().HaveCount(2);
            coverage.Select(c => c.GetProperty("filePath").GetString()).Should().OnlyContain(p => p!.StartsWith(name + "/", StringComparison.Ordinal));

            List<bool> buckets = [.. lane.GetProperty("buckets").EnumerateArray().Select(b => b.GetProperty("isRecorded").GetBoolean())];
            var first = buckets.IndexOf(true);
            var last = buckets.LastIndexOf(true);
            buckets.Skip(first).Take(last - first + 1).Should().OnlyContain(r => r, "the two segments follow each other: one second a stretch, no gap between them");
            lane.GetProperty("buckets").EnumerateArray().Should().Contain(b => b.GetProperty("isAnalysed").GetBoolean());

            var next = await client.GetAsync(new Uri($"/timeline/events?cameraId={cameraId}&after={Uri.EscapeDataString(from.ToString("O"))}", UriKind.Relative), cancellationToken);
            next.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            if (cameraId != Guid.Empty)
            {
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}/recording", UriKind.Relative), CancellationToken.None);
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}", UriKind.Relative), CancellationToken.None);
            }

            Stop(stream);
        }
    }

    [Fact]
    public async Task A_Camera_Can_Be_Watched_Live_Over_Hls()
    {
        var ffmpeg = FindFFmpegTool();
        Assert.SkipWhen(ffmpeg is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = host.CreateClient();
        var port = GetFreePort();
        using var stream = StartTestStream(ffmpeg!, port);
        Guid? cameraId = null;

        try
        {
            // Arrange — an HTTP camera with a password
            var created = await client.PostAsJsonAsync(
                "/cameras",
                new
                {
                    displayName = "Hls " + Guid.NewGuid().ToString("N")[..8],
                    ipAddress = "127.0.0.1",
                    port,
                    protocol = "http",
                    path = "live",
                    username = "viewer",
                    password = "hls-secret",
                },
                cancellationToken);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            cameraId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

            // Act — a viewer takes a lease on the stream; the answer comes when it can be played
            var started = await client.PostAsync(new Uri($"/cameras/{cameraId}/stream", UriKind.Relative), content: null, cancellationToken);
            started.StatusCode.Should().Be(HttpStatusCode.OK, await started.Content.ReadAsStringAsync(cancellationToken));
            var lease = await started.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var leaseId = lease.GetProperty("leaseId").GetGuid();
            var playlistUrl = lease.GetProperty("playlistUrl").GetString()!;
            lease.GetProperty("renewSeconds").GetInt32().Should().BePositive();

            // Assert — the playlist and its first segment can be fetched
            var playlist = await client.GetAsync(new Uri(playlistUrl, UriKind.Relative), cancellationToken);
            playlist.StatusCode.Should().Be(HttpStatusCode.OK);
            var text = await playlist.Content.ReadAsStringAsync(cancellationToken);
            text.Should().StartWith("#EXTM3U");

            var segment = text.Split('\n').Select(l => l.Trim()).First(l => l.EndsWith(".ts", StringComparison.Ordinal));
            var segmentUrl = playlistUrl[..(playlistUrl.LastIndexOf('/') + 1)] + segment;
            (await client.GetAsync(new Uri(segmentUrl, UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);

            // The viewer renews its lease while it watches, and gives it back when it is done
            var leaseUri = new Uri($"/cameras/{cameraId}/stream/{leaseId}", UriKind.Relative);
            (await client.PutAsync(leaseUri, content: null, cancellationToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await client.DeleteAsync(leaseUri, cancellationToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            // A lease that is gone cannot be renewed: the viewer starts again
            var renewedLate = await client.PutAsync(leaseUri, content: null, cancellationToken);
            renewedLate.StatusCode.Should().Be(HttpStatusCode.NotFound);
            renewedLate.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        }
        finally
        {
            if (cameraId is not null)
            {
                await client.DeleteAsync(new Uri($"/cameras/{cameraId}", UriKind.Relative), CancellationToken.None);
            }

            Stop(stream);
        }
    }

    private async Task<ReceivedWebhook> WaitForWebhookAsync(
        string eventType,
        Guid cameraId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var match = host.Webhooks.Received.FirstOrDefault(w =>
                w.EventType == eventType &&
                w.Body.Contains(cameraId.ToString(), StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(250, cancellationToken);
        }

        Assert.Fail($"No '{eventType}' webhook arrived for camera {cameraId} within 15 seconds.");
        return null!;
    }

    private static async Task<JsonElement> WaitForThumbnailAsync(
        HttpClient client,
        string cameraName,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ThumbnailTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var recording = await FindRecordingAsync(client, cameraName, cancellationToken);
            if (recording is { } found &&
                found.TryGetProperty("thumbnailPath", out var path) &&
                path.ValueKind == JsonValueKind.String)
            {
                return found;
            }

            await Task.Delay(500, cancellationToken);
        }

        Assert.Fail($"No thumbnail was listed for '{cameraName}' within {ThumbnailTimeout.TotalSeconds:N0} seconds.");
        return default;
    }

    private static async Task<JsonElement> WaitForActivityAsync(
        HttpClient client,
        string cameraName,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ThumbnailTimeout;
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
                    return activity;
                }
            }

            await Task.Delay(500, cancellationToken);
        }

        Assert.Fail($"No analysed second showed up in the activity of '{cameraName}' within {ThumbnailTimeout.TotalSeconds:N0} seconds.");
        return default;
    }

    // The previews are made a few seconds after the recording ends, by a worker that starts late.
    private static async Task<JsonElement> WaitForPreviewsAsync(
        HttpClient client,
        string query,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var sheet = await client.GetFromJsonAsync<JsonElement>(query, cancellationToken);
            if (sheet.GetProperty("hasPreviews").GetBoolean())
            {
                return sheet;
            }

            await Task.Delay(500, cancellationToken);
        }

        Assert.Fail($"No previews were made within a minute: {query}");
        return default;
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

    /// <summary>
    /// Serves a live-paced test pattern over HTTP, the way an HTTP camera
    /// does. It waits for the one client, the server, and ends by itself
    /// after a minute if nobody stops it.
    /// </summary>
    private static Process StartTestStream(
        string ffmpeg,
        int port)
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
                     "-re", "-f", "lavfi", "-i", "testsrc=size=320x240:rate=15",
                     "-c:v", "libx264", "-preset", "ultrafast", "-tune", "zerolatency",
                     "-g", "15", "-pix_fmt", "yuv420p",
                     "-t", "60",
                     "-f", "mpegts", "-listen", "1", $"http://127.0.0.1:{port}/live",
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo)!;
        process.ErrorDataReceived += (_, _) => { };
        process.BeginErrorReadLine();
        return process;
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

    private static string? FindFFmpegTool()
    {
        var folder = AtcSoft.VideoEngine.VideoEngineBootstrap.FindFFmpegPath();
        if (folder is null)
        {
            return null;
        }

        var tool = Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        return File.Exists(tool) ? tool : null;
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}