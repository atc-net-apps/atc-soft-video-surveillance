namespace AtcSoft.Video.Surveillance.Helpers.MotionDetection;

/// <summary>
/// A reflection or a patch of light that moves over a plain surface (a white kitchen door as a cat
/// walks by) changes the brightness there, but has no outline of its own: it fades out. It is not
/// motion while lighting changes are ignored. Something that moves the same way has its outline.
/// </summary>
public sealed class LightReflectionTests
{
    private const int Width = 160;
    private const int Height = 120;

    [Fact]
    public void A_Moving_Reflection_On_A_Plain_Wall_Gets_No_Box()
    {
        var boxed = PicturesWithABox(Thing.Glow, removeLight: true);

        boxed.Should().Be(0, "the glow has no edge of its own");
    }

    [Fact]
    public void Without_Removing_Light_The_Reflection_Gets_A_Box()
    {
        var boxed = PicturesWithABox(Thing.Glow, removeLight: false);

        boxed.Should().BeGreaterThan(10, "this is the box the kitchen camera showed on the door");
    }

    [Fact]
    public void Something_Moving_The_Same_Way_Gets_Its_Box()
    {
        var boxed = PicturesWithABox(Thing.Square, removeLight: true);

        boxed.Should().BeGreaterThan(10, "its outline is its own");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Light_Is_Removed_When_Lighting_Changes_Are_Ignored(
        bool ignoreLightingChanges)
        => MotionDetectorOptions.From(new MotionDetectionSettings { IgnoreLightingChanges = ignoreLightingChanges })
            .RemoveLight.Should().Be(ignoreLightingChanges);

    // How many of 40 pictures (ten a second, after four seconds of the plain wall) have a box, at
    // the highest sensitivity, while the thing moves across the wall.
    private static int PicturesWithABox(
        Thing thing,
        bool removeLight)
    {
        var settings = new MotionDetectionSettings { Sensitivity = 100 };
        var detector = new BuiltInMotionDetector(settings, MotionDetectorOptions.From(settings) with { RemoveLight = removeLight });
        for (var i = 0; i < 40; i++)
        {
            detector.Process(Picture(i, thing: null, centerX: 0));
        }

        var boxed = 0;
        for (var i = 40; i < 80; i++)
        {
            var result = detector.Process(Picture(i, thing, centerX: 30 + ((i - 40) * 2)));
            if (result.Boxes.Count > 0)
            {
                boxed++;
            }
        }

        return boxed;
    }

    // A plain light wall; a soft glow brightening it (no outline), or a dark square on it.
    private static MotionFrame Picture(
        int index,
        Thing? thing,
        int centerX)
    {
        const int centerY = 60;
        var gray = new byte[Width * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                double value = 150;
                if (thing == Thing.Glow)
                {
                    var distance2 = ((x - centerX) * (x - centerX)) + ((y - centerY) * (y - centerY));
                    value += 60 * Math.Exp(-distance2 / (2.0 * 14 * 14));
                }
                else if (thing == Thing.Square && Math.Abs(x - centerX) < 12 && Math.Abs(y - centerY) < 12)
                {
                    value = 60;
                }

                gray[(y * Width) + x] = (byte)Math.Round(value);
            }
        }

        return new MotionFrame(gray, Width, Height, DateTimeOffset.UnixEpoch.AddSeconds(index / 10.0));
    }

    private enum Thing
    {
        Glow,
        Square,
    }
}