namespace BoundingBoxReview;

/// <summary>
/// Compares the detector's boxes (yellow) with the person's (red) in a review folder: how well they
/// overlap, how much bigger or smaller the detector's box is and on which side, what the detector
/// missed, and its boxes where the person drew none. The person's boxes are those drawn in this
/// tool; a picture not reviewed here but drawn on in red in another program is read from its red
/// pixels. Writes <c>compare.md</c> beside the pictures.
/// </summary>
public static class ReviewComparer
{
    public const string ReportName = "compare.md";

    // Below this overlap a yellow box is not the detector's version of a red one.
    private const double MatchIoU = 0.05;

    /// <returns>The summary lines.</returns>
    public static IReadOnlyList<string> Compare(string folder)
    {
        var review = ReviewFile.Read(folder);
        var lines = new List<string>
        {
            $"# Bounding box review: {Path.GetFileName(review.Recording)}",
            string.Empty,
            string.Create(
                CultureInfo.InvariantCulture,
                $"Compared {DateTimeOffset.Now:yyyy-MM-dd HH:mm}. Video {review.Width}×{review.Height}, analysed at {review.AnalysisWidth}×{review.AnalysisHeight}, {review.AnalysesPerSecond} a second, sensitivity {review.Settings.Sensitivity}, smallest object {review.Settings.SmallestObjectPercent} %, ignore lighting changes {review.Settings.IgnoreLightingChanges}."),
            string.Empty,
            "Yellow is the detector, red is the person. *Size* is the yellow box's area over the red one's. *Left, top, right, bottom* are how far the yellow edge lies outside the red one, in pixels (negative: inside).",
            string.Empty,
            "| Picture | Red (person) | Yellow (detector) | Overlap (IoU) | Size | Left | Top | Right | Bottom |",
            "|---|---|---|---|---|---|---|---|---|",
        };

        var matches = new List<(ReviewBox Red, ReviewBox Yellow)>();
        var (missed, extra, reviewed, notReviewed) = (0, 0, 0, 0);
        foreach (var picture in review.Pictures)
        {
            var label = string.Create(CultureInfo.InvariantCulture, $"{picture.Seconds:0} s");
            if (PersonBoxes(folder, picture) is not { } red)
            {
                notReviewed++;
                lines.Add($"| {label} | not reviewed | {picture.Detector.Count} box(es) | | | | | | |");
                continue;
            }

            reviewed++;
            var yellow = picture.Detector;
            if (red.Count == 0 && yellow.Count == 0)
            {
                lines.Add($"| {label} | none | none | | | | | | |");
                continue;
            }

            var paired = Pair(red, yellow);
            foreach (var (r, y, iou) in paired)
            {
                matches.Add((r, y));
                lines.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"| {label} | {r} | {y} | {iou:0.00} | {y.Area / (double)r.Area:0.0}× | {r.X - y.X:+0;-0;0} | {r.Y - y.Y:+0;-0;0} | {y.Right - r.Right:+0;-0;0} | {y.Bottom - r.Bottom:+0;-0;0} |"));
            }

            foreach (var r in red.Where(r => paired.TrueForAll(p => p.Red != r)))
            {
                missed++;
                lines.Add($"| {label} | {r} | **missed** | | | | | | |");
            }

            foreach (var y in yellow.Where(y => paired.TrueForAll(p => p.Yellow != y)))
            {
                extra++;
                lines.Add($"| {label} | **none** | {y} | | | | | | |");
            }
        }

        var summary = Summarise(matches, missed, extra, reviewed, notReviewed);
        lines.Add(string.Empty);
        lines.Add("## Summary");
        lines.Add(string.Empty);
        lines.AddRange(summary.Select(s => "- " + s));
        File.WriteAllLines(Path.Combine(folder, ReportName), lines, Encoding.UTF8);
        return summary;
    }

    private static IList<ReviewBox>? PersonBoxes(
        string folder,
        ReviewPicture picture)
    {
        if (picture.Person is { } drawnHere)
        {
            return drawnHere;
        }

        var drawnPath = Path.Combine(folder, picture.File);
        var originalPath = Path.Combine(folder, ReviewFile.OriginalFolder, picture.File);
        if (!File.Exists(drawnPath) || !File.Exists(originalPath))
        {
            return null;
        }

        var (drawn, width, height) = PicturePixels.Load(drawnPath);
        var (original, originalWidth, originalHeight) = PicturePixels.Load(originalPath);
        if (width != originalWidth || height != originalHeight)
        {
            return null;
        }

        var red = Bgra.FindRedBoxes(drawn, original, width, height);
        return red.Count == 0 ? null : red;
    }

    // Each red box with the yellow box that overlaps it most, best pairs first.
    private static List<(ReviewBox Red, ReviewBox Yellow, double IoU)> Pair(
        IList<ReviewBox> red,
        IList<ReviewBox> yellow)
    {
        var paired = new List<(ReviewBox Red, ReviewBox Yellow, double IoU)>();
        var candidates = red
            .SelectMany(r => yellow.Select(y => (Red: r, Yellow: y, IoU: r.IntersectionOverUnion(y))))
            .Where(p => p.IoU >= MatchIoU)
            .OrderByDescending(p => p.IoU);
        foreach (var pair in candidates)
        {
            if (paired.TrueForAll(p => p.Red != pair.Red && p.Yellow != pair.Yellow))
            {
                paired.Add(pair);
            }
        }

        return paired;
    }

    private static List<string> Summarise(
        List<(ReviewBox Red, ReviewBox Yellow)> matches,
        int missed,
        int extra,
        int reviewed,
        int notReviewed)
    {
        var summary = new List<string>
        {
            $"Pictures reviewed: {reviewed} (not reviewed: {notReviewed}). Boxes matched: {matches.Count}; missed by the detector: {missed}; detector boxes where the person drew none: {extra}.",
        };
        if (matches.Count == 0)
        {
            return summary;
        }

        var iou = matches.Average(m => m.Red.IntersectionOverUnion(m.Yellow));
        var size = Median(matches.Select(m => m.Yellow.Area / (double)m.Red.Area));
        var left = Median(matches.Select(m => (double)(m.Red.X - m.Yellow.X)));
        var top = Median(matches.Select(m => (double)(m.Red.Y - m.Yellow.Y)));
        var right = Median(matches.Select(m => (double)(m.Yellow.Right - m.Red.Right)));
        var bottom = Median(matches.Select(m => (double)(m.Yellow.Bottom - m.Red.Bottom)));
        summary.Add(string.Create(CultureInfo.InvariantCulture, $"Mean overlap (IoU): {iou:0.00} (1 is the same box; 0.5 is usually called a hit)."));
        summary.Add(string.Create(CultureInfo.InvariantCulture, $"Median size: the detector's box is {size:0.0}× the person's."));
        summary.Add(string.Create(CultureInfo.InvariantCulture, $"Median edges outside the person's box (px): left {left:+0;-0;0}, top {top:+0;-0;0}, right {right:+0;-0;0}, bottom {bottom:+0;-0;0}."));
        return summary;
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}