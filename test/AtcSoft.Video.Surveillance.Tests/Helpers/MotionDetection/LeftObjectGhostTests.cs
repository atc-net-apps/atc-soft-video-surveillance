namespace AtcSoft.Video.Surveillance.Helpers.MotionDetection;

/// <summary>
/// Something that lay still long enough to be learnt, and then left, leaves a ghost: the scene
/// behind it is new to the background. Its outline is an edge in the background and not in the
/// picture, so it is learnt as background within a second or two, instead of in the stopped-object
/// time. Something that arrives and stops has its outline in the picture and stays.
/// </summary>
public sealed class LeftObjectGhostTests
{
    private const int Width = 160;
    private const int Height = 120;

    [Fact]
    public void The_Ghost_Of_Something_That_Left_Goes_Within_Seconds()
    {
        var change = ChangeAfter(removeGhosts: true, learntWithSquare: true, squareAfter: false);

        change.Should().BeLessThan(0.5, "the place the square left is the floor again");
    }

    [Fact]
    public void Without_Removing_Ghosts_The_Place_Stays_Foreground()
    {
        var change = ChangeAfter(removeGhosts: false, learntWithSquare: true, squareAfter: false);

        change.Should().BeGreaterThan(3, "this is the ghost the long-running test saw");
    }

    [Fact]
    public void Something_That_Arrives_And_Stops_Stays_Foreground()
    {
        var change = ChangeAfter(removeGhosts: true, learntWithSquare: false, squareAfter: true);

        change.Should().BeGreaterThan(3, "its outline is in the picture: it is there");
    }

    [Fact]
    public void Ghosts_Are_Removed_By_Default()
        => MotionDetectorOptions.From(new MotionDetectionSettings()).RemoveGhosts.Should().BeTrue();

    // The share of the picture that differs (percent) three seconds after the square left (or came),
    // at the highest sensitivity, four pictures a second.
    private static double ChangeAfter(
        bool removeGhosts,
        bool learntWithSquare,
        bool squareAfter)
    {
        var settings = new MotionDetectionSettings { Sensitivity = 100 };
        var detector = new BuiltInMotionDetector(settings, MotionDetectorOptions.From(settings) with { RemoveGhosts = removeGhosts });
        for (var i = 0; i < 40; i++)
        {
            detector.Process(Picture(i, learntWithSquare));
        }

        var change = 0.0;
        for (var i = 40; i < 52; i++)
        {
            change = detector.Process(Picture(i, squareAfter)).ChangePercent;
        }

        return change;
    }

    // An even floor, with or without a dark square on it.
    private static MotionFrame Picture(
        int index,
        bool square)
    {
        var gray = new byte[Width * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var inSquare = square && x is >= 60 and < 90 && y is >= 40 and < 70;
                gray[(y * Width) + x] = inSquare ? (byte)40 : (byte)160;
            }
        }

        return new MotionFrame(gray, Width, Height, DateTimeOffset.UnixEpoch.AddSeconds(index / 4.0));
    }
}