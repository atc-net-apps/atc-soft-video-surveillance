namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// The detector's steps run on vectors; these tests keep the plain loops they replaced and show
/// that both give the same bytes and the same floats to the bit, at sizes that leave tails and
/// edges, with and without ignore and watch zones. The storybook baseline depends on it.
/// </summary>
public sealed class VectorStepsTests
{
    public static TheoryData<int, int> Sizes
        => new()
        {
            { 1, 1 },
            { 2, 3 },
            { 7, 5 },
            { 33, 4 },
            { 34, 3 },
            { 37, 23 },
            { 70, 9 },
            { 800, 600 },
            { 450, 800 },
        };

    [Fact]
    public void The_Blurs_Division_By_Nine_Is_Exact_For_Every_Sum()
    {
        // The largest sum of nine bytes with the 4 for rounding.
        for (uint sum = 0; sum <= (9 * 255) + 4; sum++)
        {
            ((sum * MaskOperations.NinthMultiplier) >> MaskOperations.NinthShift).Should().Be(sum / 9);
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void The_Blur_Is_The_Plain_Loops(
        int width,
        int height)
    {
        var pseudo = new Pseudo((width * 31) + height);
        foreach (var picture in new[] { pseudo.Bytes(width * height), Filled(width * height, 255), Filled(width * height, 0) })
        {
            var result = new byte[picture.Length];
            MaskOperations.Blur3(picture, result, new ushort[picture.Length], width, height);

            result.Should().Equal(PlainBlur(picture, width, height));
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Open_And_Close_Are_The_Plain_Loops(
        int width,
        int height)
    {
        var pseudo = new Pseudo((width * 17) + height);
        foreach (var density in new[] { 0.05, 0.5, 0.95 })
        {
            var mask = pseudo.Mask(width * height, density);
            for (var radius = 0; radius <= 3; radius++)
            {
                var opened = (byte[])mask.Clone();
                MaskOperations.Open(opened, width, height, radius);
                var closed = (byte[])mask.Clone();
                MaskOperations.Close(closed, width, height, radius);

                opened.Should().Equal(PlainMorphology(PlainMorphology(mask, width, height, radius, keepIfAny: false), width, height, radius, keepIfAny: true));
                closed.Should().Equal(PlainMorphology(PlainMorphology(mask, width, height, radius, keepIfAny: true), width, height, radius, keepIfAny: false));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void The_Background_Learns_As_The_Plain_Loop_To_The_Bit(
        int width,
        int height)
    {
        var pseudo = new Pseudo(width + (height * 7));
        var pixels = width * height;
        var ignored = pseudo.Mask(pixels, 0.2);
        var heldPixels = pseudo.Mask(pixels, 0.3);

        // Slow foreground learning, and fast, where the block is taken in while it stays and its
        // pixels stay covered until it leaves; with and without pixels held (no flicker).
        foreach (var (ignore, foregroundRate, held) in new[] { (null, 0.001, null), (ignored, 0.001, heldPixels), (null, 0.6, heldPixels), (ignored, 0.6, null) })
        {
            var model = new BackgroundModel();
            var plain = new PlainBackground();
            var first = pseudo.Bytes(pixels);
            model.Reset(first, width, height);
            plain.Reset(first);
            foreach (var picture in Pictures(pseudo, first, width, height))
            {
                var mask = new byte[pixels];
                var plainMask = new byte[pixels];

                var share = model.Apply(picture, mask, 2.5, 12, 0.02, foregroundRate, ignore, held);
                var plainShare = plain.Apply(picture, plainMask, 2.5, 12, 0.02, foregroundRate, ignore, held);

                share.Should().Be(plainShare);
                mask.Should().Equal(plainMask);
                Bits(model.Mean).Should().Equal(Bits(plain.Mean));
                Bits(model.Variance).Should().Equal(Bits(plain.Variance));
                Bits(model.Flicker).Should().Equal(Bits(plain.Flicker));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void The_Background_With_Watch_Zones_Learns_As_The_Plain_Loop_To_The_Bit(
        int width,
        int height)
    {
        var pseudo = new Pseudo((width * 3) + height);
        var pixels = width * height;
        var watched = pseudo.Bytes(pixels).Select(b => (byte)(b % 3)).ToArray();
        var ignored = pseudo.Mask(pixels, 0.1);
        var held = pseudo.Mask(pixels, 0.2);
        double[] sigmas = [2.5, 1.5, 4];
        double[] mins = [12, 6, 20];
        var model = new BackgroundModel();
        var plain = new PlainBackground();
        var first = pseudo.Bytes(pixels);
        model.Reset(first, width, height);
        plain.Reset(first);
        var turn = 0;
        foreach (var picture in Pictures(pseudo, first, width, height))
        {
            // The zones' values change half-way: the thresholds per pixel are spread again.
            if (turn++ == 6)
            {
                sigmas = [3, 1, 2];
                mins = [10, 4, 30];
            }

            var mask = new byte[pixels];
            var plainMask = new byte[pixels];

            var share = model.ApplyZoned(picture, mask, sigmas, mins, watched, 0.05, 0.002, ignored, held);
            var plainShare = plain.ApplyZoned(picture, plainMask, sigmas, mins, watched, 0.05, 0.002, ignored, held);

            share.Should().Be(plainShare);
            mask.Should().Equal(plainMask);
            Bits(model.Mean).Should().Equal(Bits(plain.Mean));
            Bits(model.Variance).Should().Equal(Bits(plain.Variance));
            Bits(model.Flicker).Should().Equal(Bits(plain.Flicker));
        }
    }

    // A dozen pictures: noise around the first, a block that comes in the third, moves slowly (most of it stays
    // covered) and leaves after seven, then a jump in brightness. The bottom quarter sways: every other picture
    // it is bright, so its pixels keep going back to the background (flicker).
    private static IEnumerable<byte[]> Pictures(
        Pseudo pseudo,
        byte[] first,
        int width,
        int height)
    {
        for (var turn = 0; turn < 12; turn++)
        {
            var picture = first.Select(b => (byte)Math.Clamp(b + pseudo.Next(-6, 7) + (turn >= 9 ? 70 : 0), 0, 255)).ToArray();
            for (var y = height / 4; y < (turn is >= 2 and < 7 ? height / 2 : 0); y++)
            {
                for (var x = turn; x < Math.Min(width, turn + (width / 3) + 1); x++)
                {
                    picture[(y * width) + x] = 250;
                }
            }

            for (var i = height * 3 / 4 * width; turn % 2 == 1 && i < picture.Length; i++)
            {
                picture[i] = 250;
            }

            yield return picture;
        }
    }

    private static int[] Bits(ReadOnlySpan<float> values)
    {
        var bits = new int[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            bits[i] = BitConverter.SingleToInt32Bits(values[i]);
        }

        return bits;
    }

    private static byte[] Filled(
        int length,
        byte value)
        => Enumerable.Repeat(value, length).ToArray();

    // The blur before it ran on vectors.
    private static byte[] PlainBlur(
        byte[] source,
        int width,
        int height)
    {
        var rows = new ushort[source.Length];
        var result = new byte[source.Length];
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                var left = source[row + Math.Max(0, x - 1)];
                var right = source[row + Math.Min(width - 1, x + 1)];
                rows[row + x] = (ushort)(left + source[row + x] + right);
            }
        }

        for (var y = 0; y < height; y++)
        {
            var up = Math.Max(0, y - 1) * width;
            var row = y * width;
            var down = Math.Min(height - 1, y + 1) * width;
            for (var x = 0; x < width; x++)
            {
                result[row + x] = (byte)((rows[up + x] + rows[row + x] + rows[down + x] + 4) / 9);
            }
        }

        return result;
    }

    // One pass of the morphology before it ran on vectors: any (dilate) or all (erode) in the square.
    private static byte[] PlainMorphology(
        byte[] mask,
        int width,
        int height,
        int radius,
        bool keepIfAny)
    {
        if (radius <= 0)
        {
            return (byte[])mask.Clone();
        }

        var result = new byte[mask.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var any = false;
                var all = true;
                for (var yy = Math.Max(0, y - radius); yy <= Math.Min(height - 1, y + radius); yy++)
                {
                    for (var xx = Math.Max(0, x - radius); xx <= Math.Min(width - 1, x + radius); xx++)
                    {
                        any |= mask[(yy * width) + xx] != 0;
                        all &= mask[(yy * width) + xx] != 0;
                    }
                }

                result[(y * width) + x] = (keepIfAny ? any : all) ? (byte)1 : (byte)0;
            }
        }

        return result;
    }

    // The background step before it ran on vectors.
    private sealed class PlainBackground
    {
        public float[] Mean { get; private set; } = [];

        public float[] Variance { get; private set; } = [];

        public float[] Flicker { get; private set; } = [];

        // 0 never background yet, 1 background, 2 covered (with the background from before in Anchor).
        private byte[] State { get; set; } = [];

        private float[] Anchor { get; set; } = [];

        public void Reset(byte[] frame)
        {
            Mean = frame.Select(b => (float)b).ToArray();
            Variance = Enumerable.Repeat(36f, frame.Length).ToArray();
            State = new byte[frame.Length];
            Anchor = new float[frame.Length];
            Flicker = new float[frame.Length];
        }

        public double Apply(
            byte[] frame,
            byte[] mask,
            double sigmas,
            double minThreshold,
            double backgroundRate,
            double foregroundRate,
            byte[]? ignored,
            byte[]? held)
            => ApplyZoned(frame, mask, [sigmas], [minThreshold], new byte[frame.Length], backgroundRate, foregroundRate, ignored, held);

        public double ApplyZoned(
            byte[] frame,
            byte[] mask,
            IReadOnlyList<double> sigmas,
            IReadOnlyList<double> minThresholds,
            byte[] watched,
            double backgroundRate,
            double foregroundRate,
            byte[]? ignored,
            byte[]? held)
        {
            var s2 = sigmas.Select(s => (float)(s * s)).ToArray();
            var min2 = minThresholds.Select(m => (float)(m * m)).ToArray();
            var count = 0;
            var looked = frame.Length;
            var bg = (float)backgroundRate;
            var fg = (float)foregroundRate;
            for (var i = 0; i < frame.Length; i++)
            {
                var zone = watched[i];
                var d = frame[i] - Mean[i];
                var d2 = d * d;
                var isForeground = d2 > min2[zone] && d2 > s2[zone] * Variance[i];
                var state = State[i];
                var restored = false;
                if (state == 2)
                {
                    var back = frame[i] - Anchor[i];
                    var back2 = back * back;
                    if (!(back2 > min2[zone] && back2 > s2[zone] * Variance[i]))
                    {
                        Mean[i] = Anchor[i];
                        d = back;
                        d2 = back2;
                        isForeground = false;
                        restored = true;
                    }
                }

                if (ignored is not null && ignored[i] != 0)
                {
                    isForeground = false;
                    looked--;
                }

                mask[i] = isForeground ? (byte)1 : (byte)0;
                if (isForeground)
                {
                    count++;
                }

                if (isForeground && state == 1)
                {
                    Anchor[i] = Mean[i];
                }

                var stopped = isForeground && state != 0;
                var rate = stopped ? fg : bg;
                State[i] = isForeground ? (state == 0 ? (byte)0 : (byte)2) : (state == 2 && !restored ? (byte)2 : (byte)1);
                var inside = held is not null && held[i] != 0;
                var f = Flicker[i] * (1 - bg);
                if (restored && !inside && bg > 0)
                {
                    f = Math.Min(1f, f + 0.5f);
                }

                Flicker[i] = f;
                Mean[i] += rate * d;
                var swaying = Math.Clamp((f - 0.3f) * (1 / (1 - 0.3f)), 0f, 1f);
                var varianceRate = stopped ? (inside ? 0f : bg * swaying) : rate;
                Variance[i] = Math.Clamp(Variance[i] + (varianceRate * (d2 - Variance[i])), 9f, 2500f);
            }

            return looked > 0 ? count / (double)looked : 0;
        }
    }

    // Repeatable pseudo-random numbers, the same on every run.
    private sealed class Pseudo(int seed)
    {
        private uint state = (uint)seed * 2654435761u;

        public int Next(
            int min,
            int max)
        {
            state = (state * 1664525u) + 1013904223u;
            return min + (int)((state >> 8) % (uint)(max - min));
        }

        public byte[] Bytes(int length)
            => Enumerable.Range(0, length).Select(_ => (byte)Next(0, 256)).ToArray();

        public byte[] Mask(
            int length,
            double density)
            => Enumerable.Range(0, length).Select(_ => Next(0, 1000) < density * 1000 ? (byte)1 : (byte)0).ToArray();
    }
}