namespace BoundingBoxReview;

/// <summary>
/// The tool without a window, for scripts: <c>--rerun folder</c> runs today's detector over a
/// review again and compares, writing <c>compare.md</c> and a line per step to <c>rerun.log</c>.
/// </summary>
public static class Headless
{
    public static int Rerun(string folder)
    {
        var log = Path.Combine(folder, "rerun.log");
        var lines = new List<string>();
        try
        {
            var check = FFmpegRuntime.Check();
            if (check.Status != FFmpegStatus.Ready)
            {
                lines.Add(check.Describe());
                return 1;
            }

            VideoEngineBootstrap.Initialize(new VideoEngineConfig { FFmpegPath = check.Folder });
            ReviewMaker.Rerun(folder, new SyncProgress(lines));
            lines.AddRange(ReviewComparer.Compare(folder));
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or JsonException or Atc.Video.Engine.FFmpeg.FFmpegException)
        {
            lines.Add(ex.Message);
            return 1;
        }
        finally
        {
            File.WriteAllLines(log, lines, Encoding.UTF8);
        }
    }

    /// <summary>
    /// Writes what the detector saw at each picture of a review into <c>masks</c>: the grey picture
    /// it was given with its foreground in magenta, and every blob with what became of it.
    /// </summary>
    public static int Masks(
        string folder,
        bool dissolveGhosts)
    {
        var output = Path.Combine(folder, dissolveGhosts ? "masks" : "masks-no-ghosts");
        Directory.CreateDirectory(output);
        var lines = new List<string>();
        try
        {
            var check = FFmpegRuntime.Check();
            if (check.Status != FFmpegStatus.Ready)
            {
                lines.Add(check.Describe());
                return 1;
            }

            VideoEngineBootstrap.Initialize(new VideoEngineConfig { FFmpegPath = check.Folder });
            var review = ReviewFile.Read(folder);
            var settings = review.Settings;
            var detector = new AtcSoft.Video.Surveillance.Helpers.MotionDetection.BuiltInMotionDetector(
                settings,
                AtcSoft.Video.Surveillance.Helpers.MotionDetection.MotionDetectorOptions.From(settings) with { RemoveGhosts = dissolveGhosts })
            {
                WarmUpSeconds = RecordingAnalysis.WarmUpSeconds,
                KeepDiagnostics = true,
            };
            var run = StorybookRunner.RunRecording(
                review.Recording,
                detector,
                new StorybookRunOptions(AnalysisWidth: settings.AnalysisWidth, AnalysesPerSecond: review.AnalysesPerSecond, KeepPictures: true));
            foreach (var picture in review.Pictures)
            {
                var frame = run.Frames.LastOrDefault(f => f.Time <= picture.AnalysedSeconds + 0.001) ?? run.Frames[0];
                if (frame.Diagnostics is not { } seen || frame.Pixels is not { } grey)
                {
                    continue;
                }

                var pixels = new byte[seen.Width * seen.Height * 4];
                for (var y = 0; y < seen.Height; y++)
                {
                    for (var x = 0; x < seen.Width; x++)
                    {
                        var i = (y * seen.Width) + x;
                        var g = grey[i];
                        var fg = seen.IsForeground(x, y);
                        pixels[(i * 4) + 0] = fg ? (byte)255 : g;
                        pixels[(i * 4) + 1] = fg ? (byte)0 : g;
                        pixels[(i * 4) + 2] = fg ? (byte)255 : g;
                        pixels[(i * 4) + 3] = 255;
                    }
                }

                PicturePixels.Save(Path.Combine(output, picture.File), pixels, seen.Width, seen.Height);
                lines.Add(string.Create(CultureInfo.InvariantCulture, $"{picture.Seconds:0} s (analysed {frame.Time:0.00} s, {seen.Stage}):"));
                lines.AddRange(seen.Candidates.Select(c => string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {c.State} track {c.TrackId?.ToString(CultureInfo.InvariantCulture) ?? "-"} travel {c.Travel:0.000} held {c.Held} box {c.Box.X:0},{c.Box.Y:0} {c.Box.Width:0}x{c.Box.Height:0}")));
            }

            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or JsonException or Atc.Video.Engine.FFmpeg.FFmpegException)
        {
            lines.Add(ex.Message);
            return 1;
        }
        finally
        {
            File.WriteAllLines(Path.Combine(output, "candidates.txt"), lines, Encoding.UTF8);
        }
    }

    // Progress<T> posts to a synchronisation context; here the lines are wanted in order, at once.
    private sealed class SyncProgress(List<string> lines) : IProgress<string>
    {
        public void Report(string value)
            => lines.Add(value);
    }
}