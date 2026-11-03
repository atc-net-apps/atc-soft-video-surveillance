namespace BoundingBoxReview;

/// <summary>
/// Makes a review folder for a recording: runs it through the detector as CameraWall does live
/// (the same detector, settings and analyses a second) and writes a picture every few seconds,
/// with the detector's boxes in yellow, its original without boxes, and <c>boxes.json</c>.
/// </summary>
public static class ReviewMaker
{
    public const int Thickness = 3;

    public static readonly (byte R, byte G, byte B) Yellow = (255, 255, 0);

    public static readonly (byte R, byte G, byte B) Red = (255, 0, 0);

    /// <summary>
    /// Finds the CameraWall settings a recording was made with: the first <c>settings.json</c> in
    /// its folder or above it (a copied CameraWall data folder).
    /// </summary>
    public static string? FindSettings(string video)
    {
        for (var folder = Path.GetDirectoryName(video); folder is not null; folder = Path.GetDirectoryName(folder))
        {
            var candidate = Path.Combine(folder, "settings.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static MotionDetectionSettings LoadSettings(string? path)
    {
        if (path is null)
        {
            return new MotionDetectionSettings();
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var section = document.RootElement.TryGetProperty("motionDetection", out var motion) ? motion : document.RootElement;
        return section.Deserialize<MotionDetectionSettings>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? new MotionDetectionSettings();
    }

    /// <summary>
    /// Makes the review folder (<c>{recording}-review</c> beside the recording). The engine must be
    /// started. Runs for as long as the recording takes to analyse: call it off the UI thread.
    /// </summary>
    /// <returns>The folder.</returns>
    public static string Make(
        string video,
        MotionDetectionSettings settings,
        double everySeconds,
        IProgress<string> progress)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(progress);

        var name = Path.GetFileNameWithoutExtension(video);
        var folder = Path.Combine(Path.GetDirectoryName(video)!, name + "-review");
        Directory.CreateDirectory(Path.Combine(folder, ReviewFile.OriginalFolder));

        var rotation = MediaProbe.GetRotation(video);
        var (width, height) = MediaProbe.GetDisplaySize(video);
        var perSecond = Math.Max(1, settings.AnalysisFrameRate);
        progress.Report(string.Create(CultureInfo.InvariantCulture, $"Analysing {Path.GetFileName(video)} ({width}×{height}) at {perSecond} pictures a second, sensitivity {settings.Sensitivity} ..."));

        var run = StorybookRunner.RunRecording(
            video,
            RecordingAnalysis.DetectorFor(camera: null, settings, rotation),
            new StorybookRunOptions(AnalysisWidth: settings.AnalysisWidth, AnalysesPerSecond: perSecond));
        if (run.Frames.Count == 0)
        {
            throw new InvalidDataException($"{video} has no pictures.");
        }

        var review = new ReviewFile
        {
            Recording = video,
            Made = DateTimeOffset.Now,
            Width = width,
            Height = height,
            EverySeconds = everySeconds,
            AnalysesPerSecond = perSecond,
            AnalysisWidth = run.AnalysisWidth,
            AnalysisHeight = run.AnalysisHeight,
            Settings = settings,
        };

        var end = run.Frames[^1].Time;
        using (var reader = PictureFileReader.Open(video, width, height, (VideoRotation)rotation, RecordingAnalysis.DecoderThreads))
        {
            for (var at = everySeconds; at <= end; at += everySeconds)
            {
                if (reader.ReadNext(TimeSpan.FromSeconds(at)) is not { } picture)
                {
                    break;
                }

                // The boxes shown with this picture: those of the last analysis at or before it.
                var seconds = picture.Position.TotalSeconds;
                var analysed = run.Frames.LastOrDefault(f => f.Time <= seconds + 0.001) ?? run.Frames[0];
                var boxes = analysed.Detected.Select(d => ReviewBox.From(d.Box, d.TrackId, width, height)).ToList();

                var file = string.Create(CultureInfo.InvariantCulture, $"{name}-t{at:000}s.png");
                PicturePixels.Save(Path.Combine(folder, ReviewFile.OriginalFolder, file), picture.Pixels, width, height);
                var reviewPicture = new ReviewPicture { File = file, Seconds = at, AnalysedSeconds = analysed.Time, Detector = boxes };
                review.Pictures.Add(reviewPicture);
                Render(folder, review, reviewPicture);
                progress.Report($"{file}: {boxes.Count} box(es)");
            }
        }

        review.Write(folder);
        return folder;
    }

    /// <summary>
    /// Runs the detector over a review's recording again, with the review's settings and today's
    /// detector, and gives each picture the new boxes; the person's boxes and comments stay. For
    /// judging a change to the detector on reviews made before it. The engine must be started.
    /// </summary>
    public static void Rerun(
        string folder,
        IProgress<string> progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var review = ReviewFile.Read(folder);
        var video = review.Recording;
        progress.Report(string.Create(CultureInfo.InvariantCulture, $"Running the detector again over {Path.GetFileName(video)}, sensitivity {review.Settings.Sensitivity} ..."));
        var run = StorybookRunner.RunRecording(
            video,
            RecordingAnalysis.DetectorFor(camera: null, review.Settings, MediaProbe.GetRotation(video)),
            new StorybookRunOptions(AnalysisWidth: review.Settings.AnalysisWidth, AnalysesPerSecond: review.AnalysesPerSecond));
        if (run.Frames.Count == 0)
        {
            throw new InvalidDataException($"{video} has no pictures.");
        }

        foreach (var picture in review.Pictures)
        {
            var analysed = run.Frames.LastOrDefault(f => f.Time <= picture.AnalysedSeconds + 0.001) ?? run.Frames[0];
            picture.AnalysedSeconds = analysed.Time;
            picture.Detector = [.. analysed.Detected.Select(d => ReviewBox.From(d.Box, d.TrackId, review.Width, review.Height))];
            Render(folder, review, picture);
            progress.Report($"{picture.File}: {picture.Detector.Count} box(es)");
        }

        review.Made = DateTimeOffset.Now;
        review.Write(folder);
    }

    /// <summary>
    /// Writes a picture with its boxes drawn on the original: the detector's in yellow, the
    /// person's in red, so they can be seen in any picture viewer too.
    /// </summary>
    public static void Render(
        string folder,
        ReviewFile review,
        ReviewPicture picture)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(picture);

        var (pixels, width, height) = PicturePixels.Load(Path.Combine(folder, ReviewFile.OriginalFolder, picture.File));
        foreach (var box in picture.Detector)
        {
            Bgra.DrawBox(pixels, width, height, box, Yellow, Thickness);
        }

        foreach (var box in picture.Person ?? [])
        {
            Bgra.DrawBox(pixels, width, height, box, Red, Thickness);
        }

        PicturePixels.Save(Path.Combine(folder, picture.File), pixels, width, height);
    }
}