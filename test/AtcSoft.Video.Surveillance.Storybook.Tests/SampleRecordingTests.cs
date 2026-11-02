namespace AtcSoft.Video.Surveillance.Storybook;

/// <summary>
/// Real recordings without a review, measured against what is known of them: in the sample
/// recordings of the first long-running test (one person and at most three cats), more than
/// <see cref="MaxThings"/> boxes at once is wrong, and a box should sit close around what moves.
/// Every <c>*.mkv</c> under <c>ATCSOFT_BBOX_SAMPLES</c> is run as CameraWall watches a camera (15
/// pictures a second), with the settings of that test; the report and the worst pictures with their
/// boxes (<c>bbox-report</c> in that folder) are for a person tuning the detector. Skipped without
/// the folder; it judges nothing.
/// </summary>
public sealed class SampleRecordingTests
{
    private const string SamplesVariable = "ATCSOFT_BBOX_SAMPLES";
    private const string FilterVariable = "ATCSOFT_BBOX_SAMPLES_FILTER";
    private const string BaselineVariable = "ATCSOFT_BBOX_SAMPLES_BEFORE";
    private const string SensitivityVariable = "ATCSOFT_BBOX_SAMPLES_SENSITIVITY";
    private const int MaxThings = 4;

    // A box over more than this share of the picture is far bigger than a person in these rooms.
    private const double BigShare = 0.2;

    [Fact]
    public void Report_How_Many_Boxes_And_How_Big_On_The_Sample_Recordings()
    {
        var folder = Environment.GetEnvironmentVariable(SamplesVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder), $"No samples: set {SamplesVariable} to a folder of recordings.");
        EncodedScenes.RequireTool();

        var filter = Environment.GetEnvironmentVariable(FilterVariable);
        var output = Path.Combine(folder!, "bbox-report");
        Directory.CreateDirectory(output);

        // The settings of the first long-running test's CameraWall.
        var settings = new MotionDetectionSettings
        {
            Sensitivity = int.TryParse(Environment.GetEnvironmentVariable(SensitivityVariable), out var sensitivity) ? sensitivity : 100,
            SmallestObjectPercent = 0.02,
            StoppedObjectSeconds = 60,
            IgnoreLightingChanges = true,
            PostMotionDurationSeconds = 1,
        };

        var lines = new List<string>
        {
            $"# Bounding boxes on the sample recordings ({DateTime.Now:yyyy-MM-dd HH:mm})",
            string.Empty,
            "| Recording | Pictures | With boxes | Over " + MaxThings + " boxes | Most boxes | Mean boxes | Over " + (BigShare * 100) + " % of the picture | Biggest box | Tracks |",
            "|---|---|---|---|---|---|---|---|---|",
        };
        var totals = (Pictures: 0, WithBoxes: 0, TooMany: 0, Big: 0, Boxes: 0L);

        foreach (var video in Directory.GetFiles(folder!, "*.mkv", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (!string.IsNullOrEmpty(filter) && !video.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            StorybookRun run;
            try
            {
                // ATCSOFT_BBOX_SAMPLES_BEFORE=1: without following the camera's exposure, to compare.
                var options = string.Equals(Environment.GetEnvironmentVariable(BaselineVariable), "1", StringComparison.Ordinal)
                    ? MotionDetectorOptions.From(settings) with { FollowExposure = false }
                    : MotionDetectorOptions.From(settings);
                var detector = new BuiltInMotionDetector(settings, options) { WarmUpSeconds = RecordingAnalysis.WarmUpSeconds, KeepDiagnostics = true };
                run = StorybookRunner.RunRecording(
                    video,
                    detector,
                    new StorybookRunOptions(AnalysesPerSecond: 15, KeepPictures: true),
                    TestContext.Current.CancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or Atc.Video.Engine.FFmpeg.FFmpegException)
            {
                lines.Add($"| {Path.GetFileName(video)} | could not be read: {ex.Message} |");
                continue;
            }

            var (displayWidth, displayHeight) = MediaProbe.GetDisplaySize(video);
            var pictureArea = (double)displayWidth * displayHeight;
            var frames = run.Frames;
            var withBoxes = frames.Count(f => f.Detected.Count > 0);
            var tooMany = frames.Count(f => f.Detected.Count > MaxThings);
            var big = frames.Count(f => f.Detected.Any(d => d.Box.Area / pictureArea > BigShare));
            var boxes = frames.Sum(f => (long)f.Detected.Count);
            var most = frames.Count == 0 ? 0 : frames.Max(f => f.Detected.Count);
            var biggest = frames.SelectMany(f => f.Detected).Select(d => d.Box.Area / pictureArea).DefaultIfEmpty(0).Max();
            var tracks = frames.SelectMany(f => f.Detected).Select(d => d.TrackId).Distinct().Count();
            lines.Add(
                $"| {Path.GetFileNameWithoutExtension(video)} | {frames.Count} | {withBoxes} | {tooMany} | {most} | " +
                $"{(withBoxes == 0 ? 0 : boxes / (double)withBoxes):N1} | {big} | {biggest * 100:N0} % | {tracks} |");
            totals = (totals.Pictures + frames.Count, totals.WithBoxes + withBoxes, totals.TooMany + tooMany, totals.Big + big, totals.Boxes + boxes);

            // The picture with the most boxes, and the one with the biggest box, with their boxes drawn.
            var name = Path.GetFileNameWithoutExtension(video);
            if (frames.Where(f => f.Detected.Count > 0).MaxBy(f => f.Detected.Count) is { } crowded)
            {
                Save(Path.Combine(output, name + "-most.pgm"), crowded, run, displayWidth, displayHeight);
            }

            if (frames.Where(f => f.Detected.Count > 0).MaxBy(f => f.Detected.Max(d => d.Box.Area)) is { } largest)
            {
                Save(Path.Combine(output, name + "-biggest.pgm"), largest, run, displayWidth, displayHeight);
            }

            // And a picture every 30 seconds, for what is usual rather than the worst.
            for (var i = 15 * 15; i < frames.Count; i += 15 * 30)
            {
                Save(Path.Combine(output, $"{name}-at{frames[i].Time:000}.pgm"), frames[i], run, displayWidth, displayHeight);
            }
        }

        lines.Add(string.Empty);
        lines.Add(
            $"All: {totals.Pictures} pictures, {totals.WithBoxes} with boxes, {totals.TooMany} with over {MaxThings} boxes " +
            $"({(totals.WithBoxes == 0 ? 0 : totals.TooMany * 100.0 / totals.WithBoxes):N1} %), {totals.Big} with a box over {BigShare * 100} % " +
            $"({(totals.WithBoxes == 0 ? 0 : totals.Big * 100.0 / totals.WithBoxes):N1} %), {(totals.WithBoxes == 0 ? 0 : totals.Boxes / (double)totals.WithBoxes):N2} boxes a picture with boxes.");
        File.WriteAllLines(Path.Combine(output, "report.md"), lines);
        TestContext.Current.TestOutputHelper?.WriteLine(string.Join(Environment.NewLine, lines));
    }

    // A grey picture (PGM) at the analysis size, each box drawn white with a black edge.
    private static void Save(
        string path,
        ScoredFrame frame,
        StorybookRun run,
        int displayWidth,
        int displayHeight)
    {
        if (frame.Pixels is not { } source)
        {
            return;
        }

        var width = run.AnalysisWidth;
        var height = run.AnalysisHeight;
        var pixels = (byte[])source.Clone();
        var sx = width / (double)displayWidth;
        var sy = height / (double)displayHeight;
        foreach (var detected in frame.Detected)
        {
            var minX = Math.Clamp((int)(detected.Box.X * sx), 0, width - 1);
            var minY = Math.Clamp((int)(detected.Box.Y * sy), 0, height - 1);
            var maxX = Math.Clamp((int)((detected.Box.X + detected.Box.Width) * sx), 0, width - 1);
            var maxY = Math.Clamp((int)((detected.Box.Y + detected.Box.Height) * sy), 0, height - 1);
            for (var x = minX; x <= maxX; x++)
            {
                pixels[(minY * width) + x] = pixels[(maxY * width) + x] = 255;
            }

            for (var y = minY; y <= maxY; y++)
            {
                pixels[(y * width) + minX] = pixels[(y * width) + maxX] = 255;
            }
        }

        // Beside it, what the detector saw: the foreground white over the picture, darkened.
        var both = new byte[width * 2 * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                both[(y * width * 2) + x] = pixels[(y * width) + x];
                var foreground = frame.Diagnostics is { } seen && seen.Width == width && seen.IsForeground(x, y);
                both[(y * width * 2) + width + x] = foreground ? (byte)255 : (byte)(source[(y * width) + x] / 4);
            }
        }

        using var stream = File.Create(path);
        var header = System.Text.Encoding.ASCII.GetBytes($"P5\n{width * 2} {height}\n255\n");
        stream.Write(header);
        stream.Write(both);
    }
}