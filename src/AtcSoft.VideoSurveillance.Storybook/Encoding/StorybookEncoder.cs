namespace AtcSoft.VideoSurveillance.Storybook.Encoding;

/// <summary>
/// Encodes a scene to H.264 with the FFmpeg command-line tool, as a camera would send it: every
/// frame is drawn and piped to the encoder as it is, and the truth of every frame is written next
/// to the video (<see cref="TruthFile"/>). The encoder's settings are pinned (one thread, no
/// B-frames, a key frame every two seconds), so a scene gives the same video on every run.
/// </summary>
public static class StorybookEncoder
{
    private const int HighQualityCrf = 18;

    /// <summary>
    /// Encodes a scene.
    /// </summary>
    /// <param name="scene">The storybook; encoded at its <see cref="StorybookVideo.BitrateKbps"/>, or at a high quality without one.</param>
    /// <param name="ffmpegTool">The FFmpeg command-line tool.</param>
    /// <param name="videoPath">The video to write (Matroska, <c>.mkv</c>).</param>
    /// <param name="cancellationToken">Stops the encoding.</param>
    /// <returns>The truth of every frame, as also written to <see cref="TruthFile.PathFor"/>.</returns>
    /// <exception cref="StorybookException">The encoder failed.</exception>
    public static IReadOnlyList<FrameTruth> Encode(
        StorybookScene scene,
        string ffmpegTool,
        string videoPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegTool);
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);

        var video = scene.Video;
        var renderer = new SceneRenderer(scene);
        var truth = new List<FrameTruth>(renderer.FrameCount);
        var rate = video.BitrateKbps is { } kbps
            ? $"-b:v {kbps}k -maxrate {kbps}k -bufsize {kbps}k"
            : $"-crf {HighQualityCrf}";
        var rawInput = string.Create(CultureInfo.InvariantCulture, $"-f rawvideo -pix_fmt gray -s {video.Width}x{video.Height} -r {video.Fps} -i -");
        var output = string.Create(CultureInfo.InvariantCulture, $"-c:v libx264 -preset veryfast -threads 1 -bf 0 -g {video.Fps * 2} {rate} -pix_fmt yuv420p");
        var arguments = $"-hide_banner -loglevel error {rawInput} {output} -y \"{videoPath}\"";
        var start = new ProcessStartInfo
        {
            FileName = ffmpegTool,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(start) ?? throw new StorybookException($"{ffmpegTool} could not be started");
        var errors = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (errors)
                {
                    errors.AppendLine(e.Data);
                }
            }
        };
        process.BeginErrorReadLine();
        try
        {
            using var input = process.StandardInput.BaseStream;
            for (var i = 0; i < renderer.FrameCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var frame = renderer.Render(i);
                input.Write(frame.Luma);
                truth.Add(new FrameTruth(frame.Index, frame.Time, frame.Truth, frame.IgnoreAll));
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            // The encoder stopped reading: its own error says why, unless the caller stopped.
            if (!process.HasExited)
            {
                process.Kill();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new StorybookException($"{scene.Name}: the encoder failed ({process.ExitCode}): {errors.ToString().Trim()}");
        }

        TruthFile.Write(TruthFile.PathFor(videoPath), truth);
        return truth;
    }
}