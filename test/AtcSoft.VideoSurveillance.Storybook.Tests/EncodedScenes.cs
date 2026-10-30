namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// Runs a scene the way it is meant to be scored: a scene with a bitrate through the encoder and
/// the decoder (its video made once per test run and shared), any other on the fast path. The
/// encoded path needs the FFmpeg libraries and its command-line tool, and is skipped without them.
/// </summary>
internal static class EncodedScenes
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), $"atcsoft-storybook-{Environment.ProcessId}");
    private static readonly ConcurrentDictionary<string, Lazy<(string Video, IReadOnlyList<FrameTruth> Truth)>> Videos = new(StringComparer.Ordinal);

    static EncodedScenes()
        => AppDomain.CurrentDomain.ProcessExit += (_, _) => DeleteFolder();

    public static StorybookRun Run(
        string sceneFile,
        StorybookScene scene,
        IMotionDetector detector,
        StorybookRunOptions options)
    {
        if (scene.Video.BitrateKbps is null)
        {
            return StorybookRunner.Run(scene, detector, options);
        }

        var tool = RequireTool();
        var (video, truth) = Videos.GetOrAdd(
            sceneFile,
            file => new Lazy<(string, IReadOnlyList<FrameTruth>)>(() => Encode(file, scene, tool))).Value;
        return StorybookRunner.RunEncoded(scene, detector, video, truth, options);
    }

    /// <summary>
    /// Starts the engine and finds the FFmpeg command-line tool: skips the test when either is not
    /// installed, and fails it when FFmpeg is installed but does not load.
    /// </summary>
    /// <returns>The tool.</returns>
    public static string RequireTool()
    {
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool is not installed.");
        try
        {
            VideoEngineBootstrap.Initialize();
        }
        catch (Exception ex)
        {
            Assert.Fail($"FFmpeg is installed at '{folder}' but did not load: {ex.Message}");
        }

        return tool!;
    }

    private static void DeleteFolder()
    {
        try
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a temp folder left behind is harmless.
        }
    }

    private static (string Video, IReadOnlyList<FrameTruth> Truth) Encode(
        string sceneFile,
        StorybookScene scene,
        string tool)
    {
        Directory.CreateDirectory(Folder);
        var video = Path.Combine(Folder, Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(sceneFile)) + ".mkv");
        return (video, StorybookEncoder.Encode(scene, tool, video));
    }
}