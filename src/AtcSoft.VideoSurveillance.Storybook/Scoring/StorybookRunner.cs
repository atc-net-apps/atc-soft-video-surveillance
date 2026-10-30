namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// Runs a scene through a detector and scores it. On the fast path the frames go straight to the
/// detector, without encoding; on the encoded path they are read back from the scene's video, the
/// way the apps analyse a recording. The boxes scored are the boxes the apps show: a detector's
/// boxes stay on screen until it reports new ones or motion ends, as in the tiles.
/// </summary>
public static class StorybookRunner
{
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Runs a scene on the fast path.
    /// </summary>
    /// <param name="scene">The storybook.</param>
    /// <param name="detector">A fresh detector.</param>
    /// <param name="options">How the scene is fed to it.</param>
    /// <returns>The score and the frames.</returns>
    public static StorybookRun Run(
        StorybookScene scene,
        IMotionDetector detector,
        StorybookRunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(detector);

        options ??= new StorybookRunOptions();
        var renderer = new SceneRenderer(scene);
        var (analysisWidth, analysisHeight) = AnalysisSize(scene, options);
        return Score(scene.Expect, scene.Video.Width, scene.Video.Height, detector, DrawnPictures(scene, renderer, options.AnalysesPerSecond, analysisWidth, analysisHeight), analysisWidth, analysisHeight, options.KeepPictures);
    }

    /// <summary>
    /// Draws a scene's pictures on the fast path once, at the analysis size, to run several
    /// detectors over (<see cref="RunDrawn"/>): a
    /// sweep of settings draws each scene once.
    /// </summary>
    /// <param name="scene">The storybook.</param>
    /// <param name="options">How the scene is fed to a detector.</param>
    /// <returns>The pictures.</returns>
    public static StorybookPictures Draw(
        StorybookScene scene,
        StorybookRunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);

        options ??= new StorybookRunOptions();
        var renderer = new SceneRenderer(scene);
        var (analysisWidth, analysisHeight) = AnalysisSize(scene, options);
        return new StorybookPictures(
            [.. DrawnPictures(scene, renderer, options.AnalysesPerSecond, analysisWidth, analysisHeight)],
            analysisWidth,
            analysisHeight);
    }

    /// <summary>
    /// Runs a scene over pictures drawn before (<see cref="Draw"/>): the same run as on the fast
    /// path, without drawing again.
    /// </summary>
    /// <param name="scene">The storybook the pictures were drawn from.</param>
    /// <param name="detector">A fresh detector.</param>
    /// <param name="pictures">The pictures.</param>
    /// <returns>The score and the frames, without the pictures.</returns>
    public static StorybookRun RunDrawn(
        StorybookScene scene,
        IMotionDetector detector,
        StorybookPictures pictures)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(pictures);

        return Score(scene.Expect, scene.Video.Width, scene.Video.Height, detector, pictures.Pictures, pictures.AnalysisWidth, pictures.AnalysisHeight, keepPictures: false);
    }

    /// <summary>
    /// Runs a detector again over the pictures a run kept (<see cref="StorybookRunOptions.KeepPictures"/>),
    /// with their truth: the same run as the first with the same detector, without drawing or
    /// reading the video again. For trying other settings on what is open (the sample's tuning).
    /// </summary>
    /// <param name="run">A run that kept its pictures.</param>
    /// <param name="expect">What the scene expects; a recording's is empty.</param>
    /// <param name="detector">A fresh detector.</param>
    /// <returns>The new run, keeping the pictures too.</returns>
    public static StorybookRun Rerun(
        StorybookRun run,
        StorybookExpect expect,
        IMotionDetector detector)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(expect);
        ArgumentNullException.ThrowIfNull(detector);

        if (run.Frames.Any(f => f.Pixels is null))
        {
            throw new ArgumentException("The run did not keep its pictures.", nameof(run));
        }

        var pictures = run.Frames.Select(f => new AnalysisPicture(f.Time, f.Pixels!, f.Truth, f.IgnoreAll));
        return Score(expect, run.VideoWidth, run.VideoHeight, detector, pictures, run.AnalysisWidth, run.AnalysisHeight, keepPictures: true);
    }

    /// <summary>
    /// Runs a scene on the encoded path: its video (see <see cref="Encoding.StorybookEncoder"/>) is
    /// read back as grey pictures at the analysis size, as the apps read a recording, and each
    /// picture is scored with the truth of the frame it came from. Needs the engine started.
    /// </summary>
    /// <param name="scene">The storybook.</param>
    /// <param name="detector">A fresh detector.</param>
    /// <param name="videoPath">The scene's video.</param>
    /// <param name="truth">The truth of every frame of it.</param>
    /// <param name="options">How the scene is fed to the detector.</param>
    /// <returns>The score and the frames.</returns>
    public static StorybookRun RunEncoded(
        StorybookScene scene,
        IMotionDetector detector,
        string videoPath,
        IReadOnlyList<FrameTruth> truth,
        StorybookRunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ArgumentNullException.ThrowIfNull(truth);

        options ??= new StorybookRunOptions();
        var (analysisWidth, analysisHeight) = AnalysisSize(scene, options);
        using var reader = GreyFrameFileReader.Open(videoPath, analysisWidth, analysisHeight);
        var pictures = reader
            .ReadFrames(options.AnalysesPerSecond)
            .Select(grey =>
            {
                var frame = truth[Math.Clamp((int)Math.Round(grey.Position.TotalSeconds * scene.Video.Fps), 0, truth.Count - 1)];
                return new AnalysisPicture(frame.Time, grey.Pixels, frame.Truth, frame.IgnoreAll);
            });
        return Score(scene.Expect, scene.Video.Width, scene.Video.Height, detector, pictures, analysisWidth, analysisHeight, options.KeepPictures);
    }

    /// <summary>
    /// Runs a recording without a storybook, as <i>Analyse recording</i> reads it: its grey
    /// pictures at the analysis size of its shown picture (turned as it is displayed). There is
    /// no truth, so every box counts as false; the run is for looking at the boxes. Needs the
    /// engine started.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <param name="detector">A fresh detector.</param>
    /// <param name="options">How the recording is fed to the detector.</param>
    /// <param name="cancellationToken">Stops the reading; the run then holds the pictures read so far.</param>
    /// <returns>The frames, and a score without truth.</returns>
    public static StorybookRun RunRecording(
        string videoPath,
        IMotionDetector detector,
        StorybookRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ArgumentNullException.ThrowIfNull(detector);

        options ??= new StorybookRunOptions();
        var (displayWidth, displayHeight) = MediaProbe.GetDisplaySize(videoPath);
        var scene = new StorybookScene
        {
            Name = Path.GetFileName(videoPath),
            Video = new StorybookVideo { Width = displayWidth, Height = displayHeight },
        };

        var (analysisWidth, analysisHeight) = AnalysisSize(scene, options);
        using var reader = GreyFrameFileReader.Open(videoPath, analysisWidth, analysisHeight, (VideoRotation)MediaProbe.GetRotation(videoPath));
        var pictures = reader
            .ReadFrames(options.AnalysesPerSecond, cancellationToken)
            .Select(grey => new AnalysisPicture(grey.Position.TotalSeconds, grey.Pixels, [], IgnoreAll: false));
        return Score(scene.Expect, scene.Video.Width, scene.Video.Height, detector, pictures, analysisWidth, analysisHeight, options.KeepPictures);
    }

    /// <summary>
    /// Scales a grey picture bilinearly, as the apps do (the aspect ratio is not kept).
    /// </summary>
    /// <param name="source">The pixels.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="toWidth">The new width.</param>
    /// <param name="toHeight">The new height.</param>
    /// <returns>The scaled pixels.</returns>
    public static byte[] Resize(
        byte[] source,
        int width,
        int height,
        int toWidth,
        int toHeight)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (width == toWidth && height == toHeight)
        {
            return (byte[])source.Clone();
        }

        var result = new byte[toWidth * toHeight];
        var sx = width / (double)toWidth;
        var sy = height / (double)toHeight;
        for (var y = 0; y < toHeight; y++)
        {
            var fy = Math.Clamp(((y + 0.5) * sy) - 0.5, 0, height - 1);
            var y0 = (int)fy;
            var y1 = Math.Min(y0 + 1, height - 1);
            var wy = fy - y0;
            for (var x = 0; x < toWidth; x++)
            {
                var fx = Math.Clamp(((x + 0.5) * sx) - 0.5, 0, width - 1);
                var x0 = (int)fx;
                var x1 = Math.Min(x0 + 1, width - 1);
                var wx = fx - x0;
                var top = (source[(y0 * width) + x0] * (1 - wx)) + (source[(y0 * width) + x1] * wx);
                var bottom = (source[(y1 * width) + x0] * (1 - wx)) + (source[(y1 * width) + x1] * wx);
                result[(y * toWidth) + x] = (byte)Math.Round((top * (1 - wy)) + (bottom * wy));
            }
        }

        return result;
    }

    private static (int Width, int Height) AnalysisSize(
        StorybookScene scene,
        StorybookRunOptions options)
        => options.AnalysisHeight is { } squeezed
            ? (options.AnalysisWidth, squeezed)
            : MotionAnalysisSize.For(options.AnalysisWidth, scene.Video.Width, scene.Video.Height);

    private static IEnumerable<AnalysisPicture> DrawnPictures(
        StorybookScene scene,
        SceneRenderer renderer,
        double analysesPerSecond,
        int analysisWidth,
        int analysisHeight)
    {
        var step = 1 / analysesPerSecond;
        for (var k = 0; k * step <= scene.Video.DurationSeconds; k++)
        {
            var time = k * step;
            var frameIndex = Math.Min((int)Math.Round(time * scene.Video.Fps), renderer.FrameCount - 1);
            var frame = renderer.Render(frameIndex);
            yield return new AnalysisPicture(frame.Time, Resize(frame.Luma, frame.Width, frame.Height, analysisWidth, analysisHeight), frame.Truth, frame.IgnoreAll);
        }
    }

    private static StorybookRun Score(
        StorybookExpect expect,
        int videoWidth,
        int videoHeight,
        IMotionDetector detector,
        IEnumerable<AnalysisPicture> pictures,
        int analysisWidth,
        int analysisHeight,
        bool keepPictures)
    {
        var frames = new List<ScoredFrame>();
        IReadOnlyList<BoundingBox> shown = [];
        var scaleX = videoWidth / (double)analysisWidth;
        var scaleY = videoHeight / (double)analysisHeight;
        var diagnosed = detector as BuiltInMotionDetector;
        foreach (var picture in pictures)
        {
            var started = Stopwatch.GetTimestamp();
            var result = detector.Process(new MotionFrame(picture.Pixels, analysisWidth, analysisHeight, Epoch.AddSeconds(picture.Time)));
            var milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

            shown = result.Change switch
            {
                MotionHoldChange.Started or MotionHoldChange.Continues => result.Boxes,
                MotionHoldChange.Ended => [],
                _ => result.IsActive ? shown : [],
            };

            var detected = shown
                .Select(b => new DetectedBox(new PixelBox(b.X * scaleX, b.Y * scaleY, b.Width * scaleX, b.Height * scaleY), b.TrackId))
                .ToList();

            frames.Add(new ScoredFrame(
                picture.Time,
                picture.Truth,
                picture.IgnoreAll,
                detected,
                keepPictures ? picture.Pixels : null,
                milliseconds,
                diagnosed is { KeepDiagnostics: true } ? diagnosed.LastDiagnostics : null));
        }

        return new StorybookRun(StorybookScorer.Score(frames, expect), frames)
        {
            AnalysisWidth = analysisWidth,
            AnalysisHeight = analysisHeight,
            VideoWidth = videoWidth,
            VideoHeight = videoHeight,
        };
    }

    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "A picture is handed on as it is.")]
    internal sealed record AnalysisPicture(
        double Time,
        byte[] Pixels,
        IReadOnlyList<TruthBox> Truth,
        bool IgnoreAll);
}