namespace AtcSoft.Video.Surveillance.Benchmarks;

/// <summary>
/// A recording archive on disk shaped like a server's: a folder per camera and per day, each
/// recording with its thumbnail and activity index beside it. The videos hold one byte each; the
/// benchmarks measure how the archive is walked and read, not how big it is.
/// </summary>
public sealed class Archive : IDisposable
{
    private Archive(
        string root,
        IReadOnlyList<CameraConfiguration> cameras,
        IReadOnlyList<string> videos)
    {
        Root = root;
        Cameras = cameras;
        Videos = videos;
    }

    public string Root { get; }

    public IReadOnlyList<CameraConfiguration> Cameras { get; }

    public IReadOnlyList<string> Videos { get; }

    /// <summary>
    /// Writes an archive of hourly recordings, oldest first.
    /// </summary>
    /// <param name="cameraCount">How many cameras.</param>
    /// <param name="videoCount">How many recordings, spread over the cameras.</param>
    /// <param name="secondsPerIndex">How many seconds each activity index holds.</param>
    public static Archive Create(
        int cameraCount,
        int videoCount,
        int secondsPerIndex = 60)
    {
        var root = Path.Combine(Path.GetTempPath(), "atcsoft-bench-" + Guid.NewGuid().ToString("N"));
        var cameras = Enumerable.Range(1, cameraCount)
            .Select(i =>
            {
                var camera = new CameraConfiguration { Id = Guid.NewGuid() };
                camera.Display.DisplayName = $"Camera {i:00}";
                return camera;
            })
            .ToList();

        var index = WriteIndexTemplate(secondsPerIndex);
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var videos = new List<string>(videoCount);
        for (var i = 0; i < videoCount; i++)
        {
            var camera = cameras[i % cameraCount];
            var hour = i / cameraCount;
            var time = start.AddHours(hour);
            var name = RecordingPathHelper.GetCameraFolderName(camera.Display.DisplayName, camera.Id);
            var folder = Path.Combine(root, name, time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);

            var video = Path.Combine(folder, $"{name}_{time:yyyyMMdd_HHmmss}_000.mp4");
            File.WriteAllBytes(video, [0]);
            File.SetLastWriteTimeUtc(video, time.AddHours(1));
            File.WriteAllBytes(Path.ChangeExtension(video, ".png"), [0]);
            File.WriteAllText(RecordingPathHelper.BuildActivityPath(video), index);
            videos.Add(video);
        }

        return new Archive(root, cameras, videos);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    // One index, written by the app's own writer: a person walks through for a while every minute.
    private static string WriteIndexTemplate(int seconds)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using (var writer = new ActivityLogWriter(text, start))
        {
            for (var s = 0; s < seconds; s++)
            {
                var active = s % 60 < 10;
                writer.Add(
                    start.AddSeconds(s),
                    new MotionDetectorResult(active, MotionHoldChange.None, active ? 40 : 1, []));
            }
        }

        return text.ToString();
    }
}