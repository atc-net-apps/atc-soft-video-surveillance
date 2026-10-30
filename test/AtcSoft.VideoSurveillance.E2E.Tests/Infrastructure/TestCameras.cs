namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// Cameras without cameras: an HTTP camera is a live-paced FFmpeg test pattern on a local
/// port (when the FFmpeg command-line tool is installed), and a dead camera is a port
/// nothing listens on, so the failure states get opened too.
/// </summary>
internal static class TestCameras
{
    public const string LiveCameraName = "E2E Live";
    public const string DeadCameraName = "E2E Offline";
    public const string StreamPath = "live";

    /// <summary>
    /// Gets the FFmpeg command-line tool, or <see langword="null"/> when it is not installed.
    /// </summary>
    public static string? FFmpegTool { get; } = FindFFmpegTool();

    /// <summary>
    /// Gets a port nothing listens on (taken and released again).
    /// </summary>
    public static int DeadPort { get; } = E2ESettings.GetFreePort();

    /// <summary>
    /// Starts a live test camera on the port, or returns <see langword="null"/> without FFmpeg.
    /// </summary>
    public static LiveTestCamera? StartLiveCamera(
        int port,
        string outputPath)
        => FFmpegTool is null
            ? null
            : new LiveTestCamera(FFmpegTool, port, outputPath);

    /// <summary>
    /// Writes a short recording (a test pattern) and its thumbnail, so the recordings views have
    /// a row to show. Does nothing without FFmpeg.
    /// </summary>
    /// <returns><see langword="true"/> when the files were written.</returns>
    public static bool WriteRecording(
        string videoPath,
        string thumbnailPath)
    {
        if (FFmpegTool is null)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(videoPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        return RunFFmpeg("-f", "lavfi", "-i", "testsrc=duration=3:size=640x360:rate=15", "-c:v", "libx264", "-pix_fmt", "yuv420p", videoPath)
               && RunFFmpeg("-i", videoPath, "-frames:v", "1", thumbnailPath);
    }

    /// <summary>
    /// Writes an activity index beside a recording: something moves in its second second, so
    /// the player shows a timeline with an event to jump to.
    /// </summary>
    public static void WriteActivity(
        string videoPath,
        DateTimeOffset start)
    {
        using var index = ActivityLogWriter.Create(RecordingPathHelper.BuildActivityPath(videoPath), start);
        for (var second = 0; second < 3; second++)
        {
            var moving = second == 1;
            index.Add(
                start.AddSeconds(second),
                new AtcSoft.VideoSurveillance.Models.MotionDetectorResult(
                    moving,
                    moving ? MotionHoldChange.Continues : MotionHoldChange.None,
                    moving ? 20 : 1,
                    moving ? [new AtcSoft.VideoSurveillance.Models.BoundingBox { X = 10, Y = 10, Width = 40, Height = 80, TrackId = 1 }] : []));
        }
    }

    private static bool RunFFmpeg(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(FFmpegTool!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };

        foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-y" }.Concat(arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        _ = process.StandardError.ReadToEnd();
        return process.WaitForExit(60_000) && process.ExitCode == 0;
    }

    private static string? FindFFmpegTool()
    {
        var folder = AtcSoft.VideoEngine.VideoEngineBootstrap.FindFFmpegPath();
        if (folder is null)
        {
            return null;
        }

        var tool = Path.Combine(folder, "ffmpeg.exe");
        return File.Exists(tool) ? tool : null;
    }
}