namespace AtcSoft.VideoSurveillance.Storybook.Rendering;

/// <summary>
/// Draws a storybook's frames as grey pictures, with the truth of each: where every object and
/// distractor was actually drawn and what a detector should do about it. Deterministic: frame
/// <c>n</c> of a scene is the same picture on every machine and every run, and any frame can be
/// drawn on its own.
/// </summary>
public sealed class SceneRenderer
{
    private const int PurposeNoise = 1;
    private const int PurposeRain = 2;
    private const int PurposeSnow = 3;
    private const double MovingWindowSeconds = 0.5;
    private const double MinVisibleShare = 0.5;

    private readonly StorybookScene scene;
    private readonly int width;
    private readonly int height;
    private readonly int[] background;
    private readonly List<TreeShape> trees = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SceneRenderer"/> class.
    /// </summary>
    /// <param name="scene">The storybook.</param>
    public SceneRenderer(StorybookScene scene)
    {
        this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
        width = scene.Video.Width;
        height = scene.Video.Height;
        background = DrawBackground();
        for (var i = 0; i < scene.Background.Distractors.Count; i++)
        {
            var distractor = scene.Background.Distractors[i];
            if (distractor.Kind == "swayingTree")
            {
                trees.Add(TreeShape.Create(distractor, scene.Background.Seed + (i * 101), width, height));
            }
        }
    }

    /// <summary>
    /// Gets the number of frames in the scene.
    /// </summary>
    public int FrameCount
        => scene.Video.FrameCount;

    /// <summary>
    /// Draws a frame.
    /// </summary>
    /// <param name="frameIndex">The frame, from 0.</param>
    /// <returns>Its grey pixels and its truth.</returns>
    public RenderedFrame Render(int frameIndex)
    {
        var time = frameIndex / (double)scene.Video.Fps;
        var canvas = new Canvas(width, height);
        Array.Copy(background, canvas.Pixels, background.Length);
        var truth = new List<TruthBox>();

        foreach (var tree in trees)
        {
            tree.Draw(canvas, time);
            truth.Add(new TruthBox(tree.Id, tree.Region, DistractorState(tree.Id, time), IsDistractor: true));
        }

        foreach (var item in scene.Objects)
        {
            if (DrawObject(canvas, item, time) is { } box)
            {
                truth.Add(new TruthBox(item.Id, box, ObjectState(item, time), IsDistractor: false));
            }
        }

        foreach (var distractor in scene.Background.Distractors)
        {
            switch (distractor.Kind)
            {
                case "clockOverlay":
                    truth.Add(new TruthBox(distractor.Id, ClockShape.Draw(canvas, distractor, time), DistractorState(distractor.Id, time), IsDistractor: true));
                    break;
                case "rain":
                    DrawRain(canvas, distractor, frameIndex);
                    break;
                case "snow":
                    DrawSnow(canvas, distractor, time);
                    break;
                case "brightnessStep" when time >= distractor.AtSeconds:
                    canvas.AddAll(distractor.Delta);
                    break;
            }
        }

        AddNoise(canvas, frameIndex);

        return new RenderedFrame(frameIndex, time, canvas.ToBytes(), width, height, truth, IgnoreAll(time));
    }

    /// <summary>
    /// Gets where an object's centre is at a time, as fractions; <see langword="null"/> before its
    /// first and after its last key point.
    /// </summary>
    /// <param name="item">The object.</param>
    /// <param name="time">The time in seconds.</param>
    public static (double X, double Y)? PositionAt(
        StorybookObject item,
        double time)
    {
        ArgumentNullException.ThrowIfNull(item);

        var path = item.Path;
        if (path.Count == 0 || time < path[0].T || time > path[^1].T)
        {
            return null;
        }

        for (var k = 1; k < path.Count; k++)
        {
            if (time <= path[k].T)
            {
                var a = path[k - 1];
                var b = path[k];
                var f = (time - a.T) / (b.T - a.T);
                return (a.At[0] + ((b.At[0] - a.At[0]) * f), a.At[1] + ((b.At[1] - a.At[1]) * f));
            }
        }

        return (path[^1].At[0], path[^1].At[1]);
    }

    /// <summary>
    /// Gets an object's size at a time, as fractions: from its key points' sizes where given,
    /// straight lines between them, else its own size.
    /// </summary>
    /// <param name="item">The object.</param>
    /// <param name="time">The time in seconds.</param>
    public static (double Width, double Height) SizeAt(
        StorybookObject item,
        double time)
    {
        ArgumentNullException.ThrowIfNull(item);

        var sized = item.Path.Where(k => k.Size is { Count: 2 }).ToList();
        if (sized.Count == 0)
        {
            return (item.Size[0], item.Size[1]);
        }

        if (time <= sized[0].T)
        {
            return (sized[0].Size![0], sized[0].Size![1]);
        }

        for (var k = 1; k < sized.Count; k++)
        {
            if (time <= sized[k].T)
            {
                var a = sized[k - 1];
                var b = sized[k];
                var f = (time - a.T) / (b.T - a.T);
                return (a.Size![0] + ((b.Size![0] - a.Size![0]) * f), a.Size![1] + ((b.Size![1] - a.Size![1]) * f));
            }
        }

        return (sized[^1].Size![0], sized[^1].Size![1]);
    }

    private PixelBox? DrawObject(
        Canvas canvas,
        StorybookObject item,
        double time)
    {
        if (PositionAt(item, time) is not { } at)
        {
            return null;
        }

        var (sw, sh) = SizeAt(item, time);
        var w = sw * width;
        var h = sh * height;
        var left = (at.X * width) - (w / 2);
        var top = (at.Y * height) - (h / 2);

        canvas.ResetTrace();
        ObjectShapes.Draw(canvas, item.Kind, left, top, w, h, time, IsMoving(item, time), scene.Background.Seed, item.Shade);
        return canvas.TracedBox();
    }

    private bool IsMoving(
        StorybookObject item,
        double time)
    {
        var before = PositionAt(item, Math.Max(item.Path[0].T, time - MovingWindowSeconds));
        var now = PositionAt(item, time);
        if (before is not { } a || now is not { } b)
        {
            return false;
        }

        // Coming closer moves the object as much as walking past: its size changes.
        var sizeBefore = SizeAt(item, Math.Max(item.Path[0].T, time - MovingWindowSeconds));
        var sizeNow = SizeAt(item, time);
        var dx = (b.X - a.X) * width;
        var dy = (b.Y - a.Y) * height;
        var grown = Math.Max(Math.Abs(sizeNow.Width - sizeBefore.Width) * width, Math.Abs(sizeNow.Height - sizeBefore.Height) * height);
        return (dx * dx) + (dy * dy) >= 1 || grown >= 1;
    }

    private TruthState ObjectState(
        StorybookObject item,
        double time)
    {
        if (SpanState(s => s.ObjectId == item.Id, time) is { } state)
        {
            return state;
        }

        // Standing still is invisible to motion detection, and an object mostly outside the
        // picture is no fair test yet, unless what is in it fills half the picture (close to the lens).
        if (!IsMoving(item, time) || VisibleShare(item, time) is { Object: < MinVisibleShare, Picture: < MinVisibleShare })
        {
            return TruthState.DontCare;
        }

        return TruthState.MustBox;
    }

    // How much of the object is in the picture, and how much of the picture it fills there.
    private (double Object, double Picture) VisibleShare(
        StorybookObject item,
        double time)
    {
        if (PositionAt(item, time) is not { } at)
        {
            return (0, 0);
        }

        var (sw, sh) = SizeAt(item, time);
        var w = sw * width;
        var h = sh * height;
        var left = (at.X * width) - (w / 2);
        var top = (at.Y * height) - (h / 2);
        var visibleW = Math.Min(left + w, width) - Math.Max(left, 0);
        var visibleH = Math.Min(top + h, height) - Math.Max(top, 0);
        return visibleW <= 0 || visibleH <= 0
            ? (0, 0)
            : (visibleW * visibleH / (w * h), visibleW * visibleH / ((double)width * height));
    }

    private TruthState DistractorState(
        string id,
        double time)
        => SpanState(s => s.DistractorId == id, time) ?? TruthState.MustNotBox;

    private bool IgnoreAll(double time)
        => scene.Expect.Spans.Any(s => s.ObjectId is null && s.DistractorId is null && s.State == "dontCare" && time >= s.From && time <= s.To);

    private TruthState? SpanState(
        Func<StorybookSpan, bool> about,
        double time)
    {
        var span = scene.Expect.Spans.FirstOrDefault(s => about(s) && time >= s.From && time <= s.To);
        return span?.State switch
        {
            "mustBox" => TruthState.MustBox,
            "mustNotBox" => TruthState.MustNotBox,
            "dontCare" => TruthState.DontCare,
            _ => null,
        };
    }

    // A procedural scene: lighter towards the ground, with a coarse and a fine texture.
    private int[] DrawBackground()
    {
        var seed = scene.Background.Seed;
        var pixels = new int[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var gradient = 70 + (60.0 * y / height);
                var coarse = (ValueNoise(seed, x / 48.0, y / 48.0) - 0.5) * 36;
                var fine = (ScenePrng.Hash(seed + 7, x, y) - 0.5) * 10;
                pixels[(y * width) + x] = (int)Math.Round(gradient + coarse + fine);
            }
        }

        return pixels;
    }

    private static double ValueNoise(
        int seed,
        double x,
        double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        var sx = fx * fx * (3 - (2 * fx));
        var sy = fy * fy * (3 - (2 * fy));
        var a = ScenePrng.Hash(seed, x0, y0);
        var b = ScenePrng.Hash(seed, x0 + 1, y0);
        var c = ScenePrng.Hash(seed, x0, y0 + 1);
        var d = ScenePrng.Hash(seed, x0 + 1, y0 + 1);
        return (a + ((b - a) * sx)) + (((c + ((d - c) * sx)) - (a + ((b - a) * sx))) * sy);
    }

    private void DrawRain(
        Canvas canvas,
        StorybookDistractor rain,
        int frameIndex)
    {
        var prng = new ScenePrng(scene.Background.Seed, frameIndex, PurposeRain);
        for (var i = 0; i < rain.Count; i++)
        {
            var x = prng.Next(0, width);
            var y = prng.Next(0, height);
            canvas.FillRect(x, y, 1, 6, (_, _) => 210);
        }
    }

    // Flakes that fall through the whole scene, each at its own depth: most far away (small, slow,
    // dim), a few near the lens (large, fast, bright). A flake comes back in at the top.
    private void DrawSnow(
        Canvas canvas,
        StorybookDistractor snow,
        double time)
    {
        for (var i = 0; i < snow.Count; i++)
        {
            var prng = new ScenePrng(scene.Background.Seed, i, PurposeSnow);
            var near = Math.Pow(prng.NextDouble(), 3);
            var radius = Math.Max(0.8, snow.Size * height * near);
            var fall = 0.12 + (0.6 * near);
            var start = prng.NextDouble();
            var x = (prng.NextDouble() * width) + (snow.Amplitude * width * Math.Sin((2 * Math.PI * time / snow.PeriodSeconds) + (prng.NextDouble() * 2 * Math.PI)));
            var span = 1 + (4 * radius / height);
            var travelled = (start + (fall * time)) % span;
            var y = (travelled * height) - (2 * radius);
            var shade = (int)Math.Round(190 + (50 * near));
            canvas.FillEllipse(x, y, radius, radius, (_, _) => shade);
        }
    }

    private void AddNoise(
        Canvas canvas,
        int frameIndex)
    {
        var noise = scene.Video.Noise;
        if (noise <= 0)
        {
            return;
        }

        var prng = new ScenePrng(scene.Background.Seed, frameIndex, PurposeNoise);
        var pixels = canvas.Pixels;
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] += (int)Math.Round(prng.NextTriangle() * noise * 1.5);
        }
    }
}