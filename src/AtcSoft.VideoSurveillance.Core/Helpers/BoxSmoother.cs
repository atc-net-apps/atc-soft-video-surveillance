namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Smooths motion boxes from one update to the next so they do not jitter. Each new box follows
/// the box it was a moment ago, found by its track id when the detector gives one, else by
/// overlap or nearness, never by its place in the
/// list: the detector sorts its boxes by size, so two boxes swap places when one grows past the
/// other. A new box starts where it is; a box that went away is forgotten.
/// </summary>
public sealed class BoxSmoother
{
    private readonly double smoothingFactor;
    private List<BoundingBox> previous = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="BoxSmoother"/> class.
    /// </summary>
    /// <param name="smoothingFactor">How much of the old position is kept, 0 (none) to below 1.</param>
    public BoxSmoother(double smoothingFactor)
    {
        this.smoothingFactor = Math.Clamp(smoothingFactor, 0, 0.95);
    }

    /// <summary>
    /// Smooths the boxes of an update against those of the previous one.
    /// </summary>
    /// <param name="boxes">The new boxes, in any order.</param>
    /// <returns>The smoothed boxes, in the order given.</returns>
    public IReadOnlyList<BoundingBox> Smooth(IReadOnlyList<BoundingBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        var result = new BoundingBox[boxes.Count];
        var taken = new bool[previous.Count];

        // Pairs best first: overlap, then the distance of the centres relative to the size.
        var pairs = new List<(int New, int Old, double Score)>();
        for (var n = 0; n < boxes.Count; n++)
        {
            for (var o = 0; o < previous.Count; o++)
            {
                var score = MatchScore(boxes[n], previous[o]);
                if (score > 0)
                {
                    pairs.Add((n, o, score));
                }
            }
        }

        foreach (var (n, o, _) in pairs.OrderByDescending(p => p.Score))
        {
            if (result[n] is not null || taken[o])
            {
                continue;
            }

            taken[o] = true;
            result[n] = Blend(previous[o], boxes[n]);
        }

        for (var n = 0; n < boxes.Count; n++)
        {
            result[n] ??= Copy(boxes[n]);
        }

        previous = [.. result.Select(Copy)];
        return result;
    }

    /// <summary>
    /// Forgets the previous boxes, so the next update is shown as it is.
    /// </summary>
    public void Reset()
        => previous = [];

    // The same track scores above everything; overlap scores above 1; a near box without
    // overlap scores between 0 and 1; a far one 0. Boxes of two different tracks never match.
    private static double MatchScore(
        BoundingBox a,
        BoundingBox b)
    {
        if (a.TrackId is { } trackA && b.TrackId is { } trackB)
        {
            return trackA == trackB ? 10 : 0;
        }

        var iou = IntersectionOverUnion(a, b);
        if (iou > 0)
        {
            return 1 + iou;
        }

        var dx = (a.X + (a.Width / 2)) - (b.X + (b.Width / 2));
        var dy = (a.Y + (a.Height / 2)) - (b.Y + (b.Height / 2));
        var reach = Math.Max(Math.Max(a.Width, a.Height), Math.Max(b.Width, b.Height));
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        return reach > 0 && distance < reach ? 1 - (distance / reach) : 0;
    }

    private static double IntersectionOverUnion(
        BoundingBox a,
        BoundingBox b)
    {
        var width = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
        var height = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var intersection = width * height;
        return intersection / (a.Area + b.Area - intersection);
    }

    private BoundingBox Blend(
        BoundingBox old,
        BoundingBox target)
    {
        var keep = smoothingFactor;
        var take = 1 - keep;
        return new BoundingBox
        {
            X = (old.X * keep) + (target.X * take),
            Y = (old.Y * keep) + (target.Y * take),
            Width = (old.Width * keep) + (target.Width * take),
            Height = (old.Height * keep) + (target.Height * take),
            TrackId = target.TrackId,
        };
    }

    private static BoundingBox Copy(BoundingBox box)
        => new() { X = box.X, Y = box.Y, Width = box.Width, Height = box.Height, TrackId = box.TrackId };
}