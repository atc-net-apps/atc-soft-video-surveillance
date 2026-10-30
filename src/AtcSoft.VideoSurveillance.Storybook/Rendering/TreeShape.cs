namespace AtcSoft.VideoSurveillance.Storybook.Rendering;

/// <summary>
/// A tree in the wind: a trunk and a crown of leaf clusters, each swaying with a phase of its
/// own, more at the top. It moves on the spot and must never get a box.
/// </summary>
internal sealed class TreeShape
{
    private const int ClusterCount = 30;

    private readonly List<(double X, double Y, double Radius, double Phase, double Reach)> clusters = [];
    private readonly double trunkX;
    private readonly double trunkY;
    private readonly double trunkW;
    private readonly double trunkH;
    private readonly double swayPixels;
    private readonly double periodSeconds;
    private readonly int seed;

    private TreeShape(
        string id,
        StorybookDistractor tree,
        int seed,
        int width,
        int height)
    {
        Id = id;
        this.seed = seed;
        var footX = (tree.At?[0] ?? 0.15) * width;
        var footY = (tree.At?[1] ?? 0.6) * height;
        var crownR = tree.Size * height / 2;
        trunkW = crownR * 0.25;
        trunkH = crownR * 1.1;
        trunkX = footX - (trunkW / 2);
        trunkY = footY - trunkH;
        var crownX = footX;
        var crownY = trunkY - (crownR * 0.6);
        swayPixels = tree.Amplitude * width;
        periodSeconds = Math.Max(0.1, tree.PeriodSeconds);

        var prng = new ScenePrng(seed, 0, 99);
        for (var i = 0; i < ClusterCount; i++)
        {
            var angle = prng.NextDouble() * Math.PI * 2;
            var distance = Math.Sqrt(prng.NextDouble()) * crownR * 0.75;
            var x = crownX + (Math.Cos(angle) * distance);
            var y = crownY + (Math.Sin(angle) * distance);
            var radius = crownR * (0.22 + (prng.NextDouble() * 0.13));

            // Higher clusters sway more.
            var reach = 0.4 + (0.6 * Math.Clamp((crownY + crownR - y) / (2 * crownR), 0, 1));
            clusters.Add((x, y, radius, prng.NextDouble() * Math.PI * 2, reach));
        }

        var margin = swayPixels + 1;
        var left = Math.Min(trunkX, clusters.Min(c => c.X - c.Radius)) - margin;
        var top = clusters.Min(c => c.Y - c.Radius) - margin;
        var right = Math.Max(trunkX + trunkW, clusters.Max(c => c.X + c.Radius)) + margin;
        var bottom = footY;
        Region = Clip(new PixelBox(left, top, right - left, bottom - top), width, height);
    }

    public string Id { get; }

    /// <summary>
    /// Gets the box the tree can reach while swaying.
    /// </summary>
    public PixelBox Region { get; }

    public static TreeShape Create(
        StorybookDistractor tree,
        int seed,
        int width,
        int height)
        => new(tree.Id, tree, seed, width, height);

    public void Draw(
        Canvas canvas,
        double time)
    {
        var bark = (Func<int, int, int>)((x, y) => 50 + (int)((ScenePrng.Hash(seed + 1, x / 2, y / 3) - 0.5) * 14));
        canvas.FillRect(trunkX, trunkY, trunkW, trunkH, bark);

        // Coarse leaf speckle: as clusters sway, the speckle moves and lights scattered pixels.
        var leaves = (Func<int, int, int>)((x, y) => 45 + (int)(ScenePrng.Hash(seed + 2, x / 3, y / 3) * 55));
        foreach (var (x, y, radius, phase, reach) in clusters)
        {
            var dx = swayPixels * reach * Math.Sin((2 * Math.PI * time / periodSeconds) + phase);
            canvas.FillEllipse(x + dx, y, radius, radius * 0.85, (px, py) => leaves(px - (int)Math.Round(dx), py));
        }
    }

    private static PixelBox Clip(
        PixelBox box,
        int width,
        int height)
    {
        var left = Math.Max(0, box.X);
        var top = Math.Max(0, box.Y);
        var right = Math.Min(width, box.X + box.Width);
        var bottom = Math.Min(height, box.Y + box.Height);
        return new PixelBox(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}