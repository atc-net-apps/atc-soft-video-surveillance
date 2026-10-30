namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// Wind: a patch that keeps going back to the background (a branch or a net swaying) is taken in
/// within seconds, while something that stops keeps the variance of the background it covers and
/// stays foreground. Inside the box of an object being followed nothing is taken in as wind, so a
/// walker's swinging legs stay part of the walker.
/// </summary>
public sealed class SwayTests
{
    private const int Width = 64;
    private const int Height = 48;
    private const int PerSecond = 4;

    // The background's rate at 4 pictures a second with its 4 s time constant, and the foreground's
    // at the default stopped-object time.
    private static readonly double BackgroundRate = 1 - Math.Exp(-1.0 / PerSecond / 4);
    private static readonly double ForegroundRate = 1 - Math.Exp(-1.0 / PerSecond / 20);

    [Fact]
    public void A_Patch_That_Keeps_Swaying_Is_Taken_In()
    {
        var model = Learnt();

        var shares = Enumerable.Range(0, 10 * PerSecond).Select(k => model.Apply(Picture(swayOut: k % 2 == 1), new byte[Width * Height], 3, 16, BackgroundRate, ForegroundRate)).ToList();

        shares[1].Should().BeGreaterThan(0.1, "the first swing differs from the background");
        shares.TakeLast(PerSecond).Should().OnlyContain(s => s < 0.01, "after seconds of swaying the patch is background");
    }

    [Fact]
    public void A_Swaying_Patch_Held_By_An_Object_Is_Not_Taken_In()
    {
        var model = Learnt();
        var held = Patch();

        var shares = Enumerable.Range(0, 10 * PerSecond).Select(k => model.Apply(Picture(swayOut: k % 2 == 1), new byte[Width * Height], 3, 16, BackgroundRate, ForegroundRate, held: held)).ToList();

        shares.TakeLast(PerSecond).Where((_, k) => k % 2 == 1).Should().OnlyContain(s => s > 0.1, "an object's own swinging parts stay foreground");
    }

    [Fact]
    public void Something_That_Stops_Keeps_The_Variance_Of_What_It_Covers()
    {
        var model = Learnt();
        var patch = Patch();
        var before = Covered(model.Variance, patch);

        // It comes and stays for ten seconds.
        var shares = Enumerable.Range(0, 10 * PerSecond).Select(_ => model.Apply(Picture(swayOut: true), new byte[Width * Height], 3, 16, BackgroundRate, ForegroundRate)).ToList();

        shares[^1].Should().BeGreaterThan(0.1, "ten seconds is well within the stopped-object time");
        Covered(model.Variance, patch).Should().Equal(before, "a covered pixel that never went back learns no variance");
        model.Flicker.ToArray().Should().OnlyContain(f => f == 0);
    }

    // The background, learnt from the still picture until every pixel has matched it.
    private static BackgroundModel Learnt()
    {
        var model = new BackgroundModel();
        model.Reset(Picture(swayOut: false), Width, Height);
        for (var k = 0; k < 2; k++)
        {
            model.Apply(Picture(swayOut: false), new byte[Width * Height], 3, 16, BackgroundRate, ForegroundRate);
        }

        return model;
    }

    // Grey 100 with a patch that is 180 while swayed out (a quarter of the picture).
    private static byte[] Picture(bool swayOut)
    {
        var picture = Enumerable.Repeat((byte)100, Width * Height).ToArray();
        if (swayOut)
        {
            var patch = Patch();
            for (var i = 0; i < picture.Length; i++)
            {
                picture[i] = patch[i] != 0 ? (byte)180 : picture[i];
            }
        }

        return picture;
    }

    private static float[] Covered(
        ReadOnlySpan<float> values,
        byte[] patch)
    {
        var covered = new List<float>();
        for (var i = 0; i < values.Length; i++)
        {
            if (patch[i] != 0)
            {
                covered.Add(values[i]);
            }
        }

        return [.. covered];
    }

    private static byte[] Patch()
    {
        var patch = new byte[Width * Height];
        for (var y = Height / 4; y < Height * 3 / 4; y++)
        {
            for (var x = Width / 4; x < Width * 3 / 4; x++)
            {
                patch[(y * Width) + x] = 1;
            }
        }

        return patch;
    }
}