namespace AtcSoft.VideoSurveillance.Api.Host;

[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostRecordingsTests(ApiHostFixture host)
{
    // The smallest valid PNG: one transparent pixel.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    [Fact]
    public async Task A_Listed_Recording_And_Its_Thumbnail_Can_Be_Fetched_By_The_Paths_Returned()
    {
        using var client = host.CreateClient();
        var recordingPath = await GetRecordingPathAsync(client);
        var camera = "Fetch " + Guid.NewGuid().ToString("N")[..8];
        var video = Path.Combine(recordingPath, camera, "2026-10-12", camera + "_20261012_101500_000.mp4");
        var thumbnail = Path.Combine(recordingPath, "thumbnails", camera, "2026-10-12", camera + "_20261012_101500_000.png");
        Write(video, [1, 2, 3, 4]);
        Write(thumbnail, Png);

        var recordings = await client.GetFromJsonAsync<JsonElement>("/recordings", TestContext.Current.CancellationToken);

        var listed = recordings.EnumerateArray().Single(r => r.GetProperty("filePath").GetString()!.Contains(camera, StringComparison.Ordinal));
        listed.GetProperty("hasThumbnail").GetBoolean().Should().BeTrue();
        var thumbnailPath = listed.GetProperty("thumbnailPath").GetString();
        thumbnailPath.Should().Be($"thumbnails/{camera}/2026-10-12/{camera}_20261012_101500_000.png");

        var thumbnailResponse = await client.GetAsync(FileUrl(thumbnailPath!), TestContext.Current.CancellationToken);
        thumbnailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        thumbnailResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await thumbnailResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Should().Equal(Png);

        var videoResponse = await client.GetAsync(FileUrl(listed.GetProperty("filePath").GetString()!), TestContext.Current.CancellationToken);
        videoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        videoResponse.Content.Headers.ContentType!.MediaType.Should().Be("video/mp4");
    }

    [Fact]
    public async Task A_Verdict_From_A_Timeline_Is_Written_Beside_The_Recording_By_An_Operator_And_Audited()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = host.CreateClient();
        var recordingPath = await GetRecordingPathAsync(admin);
        var camera = "Review " + Guid.NewGuid().ToString("N")[..8];
        var video = Path.Combine(recordingPath, camera, camera + "_20261012_101500_000.mp4");
        Write(video, [1, 2, 3, 4]);
        var url = new Uri($"/recordings/review/verdicts?filePath={Uri.EscapeDataString($"{camera}/{camera}_20261012_101500_000.mp4")}", UriKind.Relative);
        using var viewer = await CreateUserClientAsync("viewer");
        using var operatorClient = await CreateUserClientAsync("operator");

        var refused = await viewer.PostAsJsonAsync(url, new { fromSeconds = 2.5, toSeconds = 7.5, verdict = "missed" }, cancellationToken);
        var added = await operatorClient.PostAsJsonAsync(url, new { fromSeconds = 10, toSeconds = 14, verdict = "falseBox" }, cancellationToken);
        var none = await operatorClient.PostAsJsonAsync(url, new { fromSeconds = 14, toSeconds = 10, verdict = "correct" }, cancellationToken);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden, "a viewer reads but does not change");
        added.StatusCode.Should().Be(HttpStatusCode.NoContent);
        none.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var review = AtcSoft.VideoSurveillance.Helpers.Review.RecordingReviewFile.Read(RecordingPathHelper.BuildReviewPath(video));
        review.Spans.Should().ContainSingle().Which.Verdict.Should().Be("falseBox");
        review.Spans[0].From.Should().Be(TimeSpan.FromSeconds(10));
        var audit = await admin.GetFromJsonAsync<JsonElement>("/audit?take=200", cancellationToken);
        audit.EnumerateArray().Should().Contain(x => x.GetProperty("action").GetString() == "addRecordingVerdict");
    }

    [Fact]
    public async Task An_Older_Recording_Is_Analysed_In_The_Background_And_Gets_Its_Index()
    {
        var folder = AtcSoft.VideoEngine.VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool is not installed.");

        using var client = host.CreateClient();
        var recordingPath = await GetRecordingPathAsync(client);
        var camera = "Analyse " + Guid.NewGuid().ToString("N")[..8];
        var video = Path.Combine(recordingPath, camera, camera + "_20261012_101500_000.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = tool,
            Arguments = $"-hide_banner -loglevel error -f lavfi -i testsrc=size=320x240:rate=15:duration=6 -c:v mpeg4 -y \"{video}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        })!)
        {
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        }

        var filePath = Uri.EscapeDataString($"{camera}/{camera}_20261012_101500_000.mkv");
        using var started = await client.PostAsync(new Uri($"/recordings/activity?filePath={filePath}", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        started.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // The service looks for queued recordings every two seconds; six seconds decode in a moment.
        JsonElement activity = default;
        for (var i = 0; i < 60; i++)
        {
            activity = await client.GetFromJsonAsync<JsonElement>($"/recordings/activity?filePath={filePath}", TestContext.Current.CancellationToken);
            if (activity.GetProperty("hasIndex").GetBoolean())
            {
                break;
            }

            await Task.Delay(500, TestContext.Current.CancellationToken);
        }

        activity.GetProperty("hasIndex").GetBoolean().Should().BeTrue("the recording was analysed");
        activity.GetProperty("startedAt").GetDateTimeOffset().Should().Be(new DateTimeOffset(2026, 10, 12, 10, 15, 0, TimeSpan.Zero), "the server names its recordings in UTC");
        using var again = await client.PostAsync(new Uri($"/recordings/activity?filePath={filePath}", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict, "it has its index now");
    }

    [Fact]
    public async Task A_Recording_Asked_For_As_A_Download_Is_Sent_As_An_Attachment()
    {
        // A page on another address cannot tell the browser to save a file
        // instead of showing it; only the server's answer can.
        using var client = host.CreateClient();
        var recordingPath = await GetRecordingPathAsync(client);
        var camera = "Save " + Guid.NewGuid().ToString("N")[..8];
        Write(Path.Combine(recordingPath, camera, camera + "_20261012_101500_000.mp4"), [1, 2, 3, 4]);
        var url = "/recordings-files/" + RecordingPathHelper.EscapePathSegments($"{camera}/{camera}_20261012_101500_000.mp4");

        var played = await client.GetAsync(new Uri(url, UriKind.Relative), TestContext.Current.CancellationToken);
        var downloaded = await client.GetAsync(new Uri(url + "?download=1", UriKind.Relative), TestContext.Current.CancellationToken);

        played.Content.Headers.ContentDisposition.Should().BeNull("a player shows the file");
        downloaded.StatusCode.Should().Be(HttpStatusCode.OK);
        downloaded.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        downloaded.Content.Headers.ContentDisposition.FileNameStar.Should().Be($"{camera}_20261012_101500_000.mp4");
    }

    [Fact]
    public async Task Files_That_Are_Not_Recordings_Or_Thumbnails_Are_Not_Served()
    {
        using var client = host.CreateClient();
        var recordingPath = await GetRecordingPathAsync(client);
        Write(Path.Combine(recordingPath, "notes.txt"), [1]);
        Write(Path.Combine(recordingPath, "tool.exe"), [1]);

        (await client.GetAsync(FileUrl("notes.txt"), TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync(FileUrl("tool.exe"), TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync(FileUrl("../settings.json"), TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Recordings_Are_Served_From_The_New_Folder_After_The_Recording_Path_Changes()
    {
        using var client = host.CreateClient();
        var original = await GetRecordingPathAsync(client);
        var moved = Path.Combine(host.DataPath, "moved-" + Guid.NewGuid().ToString("N")[..8]);
        var camera = "Moved " + Guid.NewGuid().ToString("N")[..8];
        Write(Path.Combine(moved, camera, camera + "_20261012_101500_000.mp4"), [9, 9]);

        try
        {
            (await PutRecordingPathAsync(client, moved)).IsSuccessStatusCode.Should().BeTrue();

            var recordings = await client.GetFromJsonAsync<JsonElement>("/recordings", TestContext.Current.CancellationToken);
            var listed = recordings.EnumerateArray().Single(r => r.GetProperty("filePath").GetString()!.Contains(camera, StringComparison.Ordinal));

            var response = await client.GetAsync(FileUrl(listed.GetProperty("filePath").GetString()!), TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.OK, "the server serves the folder it records to, without a restart");
        }
        finally
        {
            await PutRecordingPathAsync(client, original);
        }
    }

    [Fact]
    public async Task A_Recording_Is_Deleted_By_The_Path_The_Listing_Gave_And_Is_Gone_From_The_Listing()
    {
        using var client = host.CreateClient();
        var recordingPath = await GetRecordingPathAsync(client);
        var camera = "Delete " + Guid.NewGuid().ToString("N")[..8];
        var video = Path.Combine(recordingPath, camera, "2026-10-12", camera + "_20261012_101500_000.mp4");
        var thumbnail = Path.Combine(recordingPath, "thumbnails", camera, "2026-10-12", camera + "_20261012_101500_000.png");
        Write(video, [1, 2, 3, 4]);
        Write(thumbnail, Png);
        var listedPath = (await ListAsync(client)).Single(p => p.Contains(camera, StringComparison.Ordinal));

        var deleted = await client.DeleteAsync(DeleteUrl(listedPath), TestContext.Current.CancellationToken);

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        File.Exists(video).Should().BeFalse();
        File.Exists(thumbnail).Should().BeFalse();
        (await ListAsync(client)).Should().NotContain(p => p.Contains(camera, StringComparison.Ordinal));

        var again = await client.DeleteAsync(DeleteUrl(listedPath), TestContext.Current.CancellationToken);
        again.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("../settings.json")]
    [InlineData("../../cameras.json")]
    [InlineData("thumbnails/x/x_20261012_101500_000.png")]
    public async Task Nothing_But_A_Recording_Can_Be_Deleted_Through_The_Api(
        string filePath)
    {
        // The server's own files sit next to the recording folder.
        using var client = host.CreateClient();

        var response = await client.DeleteAsync(DeleteUrl(filePath), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var settings = await client.GetAsync(new Uri("/settings", UriKind.Relative), TestContext.Current.CancellationToken);
        settings.StatusCode.Should().Be(HttpStatusCode.OK, "the server still has its settings");
    }

    private static Uri DeleteUrl(string filePath)
        => new("/recordings?filePath=" + Uri.EscapeDataString(filePath), UriKind.Relative);

    private static async Task<List<string>> ListAsync(HttpClient client)
    {
        var recordings = await client.GetFromJsonAsync<JsonElement>("/recordings", TestContext.Current.CancellationToken);
        return [.. recordings.EnumerateArray().Select(r => r.GetProperty("filePath").GetString()!)];
    }

    private static async Task<string> GetRecordingPathAsync(HttpClient client)
    {
        var settings = await client.GetFromJsonAsync<JsonElement>("/settings", TestContext.Current.CancellationToken);
        return settings.GetProperty("recordingPath").GetString()!;
    }

    private static async Task<HttpResponseMessage> PutRecordingPathAsync(
        HttpClient client,
        string recordingPath)
    {
        var settings = await client.GetFromJsonAsync<JsonObject>("/settings", TestContext.Current.CancellationToken);
        settings!["recordingPath"] = recordingPath;
        return await client.PutAsJsonAsync("/settings", settings, TestContext.Current.CancellationToken);
    }

    private static Uri FileUrl(string relativePath)
        => new("/recordings-files/" + RecordingPathHelper.EscapePathSegments(relativePath), UriKind.Relative);

    private async Task<HttpClient> CreateUserClientAsync(string role)
    {
        using var admin = host.CreateClient();
        var name = role + "-" + Guid.NewGuid().ToString("N")[..8];
        (await admin.PostAsJsonAsync("/users", new { userName = name, password = "a password here", role }, TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var (_, session) = await host.SignInAsync(name, "a password here");

        var client = host.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        return client;
    }

    private static void Write(
        string path,
        byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }
}