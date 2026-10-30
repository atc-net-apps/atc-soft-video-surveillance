namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// *Play all from here*: one clock for every tile, the slowest setting the pace, gaps crossed, and a
/// tile that will not load given up.
/// </summary>
public sealed class PlayAllPacerTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(250);

    [Fact]
    public void A_Tile_Whose_Recording_Covers_The_Moment_Starts_At_Once_And_One_Later_Waits_For_The_Clock()
    {
        var pacer = new PlayAllPacer(Nine);

        pacer.Tick(TimeSpan.Zero, [Waiting(Nine.AddMinutes(-5)), Waiting(Nine.AddSeconds(1))])
            .Should().Equal(PlayAllAction.Start, PlayAllAction.None);
        pacer.Tick(TimeSpan.FromSeconds(1), [Playing(Nine.AddSeconds(1)), Waiting(Nine.AddSeconds(1))])
            .Should().Equal(PlayAllAction.None, PlayAllAction.Start);
    }

    [Fact]
    public void The_Slowest_Sets_The_Pace_And_One_Ahead_Is_Held_Until_It_Is_Close_Again()
    {
        var pacer = new PlayAllPacer(Nine);

        // The second tile buffers: it stays at 9:00 while the first runs on.
        pacer.Tick(TimeSpan.FromSeconds(1), [Playing(Nine.AddSeconds(1)), Playing(Nine)]).Should().Equal(PlayAllAction.None, PlayAllAction.None);
        pacer.Clock.Should().Be(Nine, "the clock waits for the slowest");
        pacer.Tick(TimeSpan.FromSeconds(1), [Playing(Nine.AddSeconds(2)), Playing(Nine)]).Should().Equal(PlayAllAction.Hold, PlayAllAction.None);

        // Held at 9:00:02; the slow one plays again.
        pacer.Tick(Tick, [Held(Nine.AddSeconds(2)), Playing(Nine.AddSeconds(1))]).Should().Equal(PlayAllAction.None, PlayAllAction.None);
        pacer.Tick(Tick, [Held(Nine.AddSeconds(2)), Playing(Nine.AddSeconds(1.8))]).Should().Equal(PlayAllAction.Resume, PlayAllAction.None);
    }

    [Fact]
    public void With_No_Tile_Playing_The_Clock_Jumps_To_The_Next_Recording_Of_Any_Tile()
    {
        var pacer = new PlayAllPacer(Nine);
        PlayAllTile[] tiles = [Waiting(Nine.AddHours(2)), Waiting(Nine.AddHours(1)), Finished(), Failed()];

        pacer.Tick(TimeSpan.FromSeconds(1), tiles).Should().Equal(PlayAllAction.None, PlayAllAction.Start, PlayAllAction.None, PlayAllAction.None);
        pacer.Clock.Should().Be(Nine.AddHours(1));
    }

    [Fact]
    public void A_Tile_Loading_Or_Held_Keeps_The_Clock_From_Jumping()
    {
        var pacer = new PlayAllPacer(Nine);

        pacer.Tick(TimeSpan.FromSeconds(1), [Waiting(Nine.AddHours(1)), Loading()]).Should().Equal(PlayAllAction.None, PlayAllAction.None);
        pacer.Clock.Should().Be(Nine.AddSeconds(1));
    }

    [Fact]
    public void A_Held_Tile_Plays_Again_When_The_Others_Stop_Playing()
    {
        var pacer = new PlayAllPacer(Nine);

        pacer.Tick(TimeSpan.FromSeconds(1), [Held(Nine.AddSeconds(2)), Finished()]).Should().Equal(PlayAllAction.None, PlayAllAction.None);
        pacer.Tick(TimeSpan.FromSeconds(1), [Held(Nine.AddSeconds(2)), Finished()]).Should().Equal(PlayAllAction.Resume, PlayAllAction.None);
    }

    [Fact]
    public void A_Tile_That_Loads_Too_Long_Is_Given_Up_And_Does_Not_Hold_The_Others()
    {
        var pacer = new PlayAllPacer(Nine);
        var actions = new List<PlayAllAction>();
        for (var second = 1; second <= 16; second++)
        {
            actions.Add(pacer.Tick(TimeSpan.FromSeconds(1), [Playing(Nine.AddSeconds(second)), Loading()])[1]);
        }

        actions[..15].Should().OnlyContain(a => a == PlayAllAction.None);
        actions[15].Should().Be(PlayAllAction.GiveUp);
        pacer.Clock.Should().Be(Nine.AddSeconds(16), "a tile still loading does not set the pace");
    }

    [Fact]
    public void A_Tile_Playing_Whose_Picture_Stops_Moving_Is_Given_Up_And_The_Others_Go_On()
    {
        var pacer = new PlayAllPacer(Nine);
        var actions = new List<PlayAllAction>();
        for (var second = 1; second <= 17; second++)
        {
            actions.Add(pacer.Tick(TimeSpan.FromSeconds(1), [Held(Nine.AddSeconds(5)), Playing(Nine.AddSeconds(3))])[1]);
        }

        // Still from the first tick it was seen at 9:00:03: fifteen seconds later, and then some.
        actions[..16].Should().OnlyContain(a => a == PlayAllAction.None);
        actions[16].Should().Be(PlayAllAction.GiveUp);
    }

    [Fact]
    public void A_Picture_Counts_As_Opened_Near_Where_It_Was_Asked()
    {
        var asked = TimeSpan.FromMinutes(10);

        PlayAllStart.IsNear(asked, asked).Should().BeTrue();
        PlayAllStart.IsNear(asked + TimeSpan.FromSeconds(5), asked).Should().BeTrue("it plays on while it opens");
        PlayAllStart.IsNear(asked - TimeSpan.FromSeconds(2), asked).Should().BeTrue("a seek lands on the key frame before");
        PlayAllStart.IsNear(TimeSpan.Zero, asked).Should().BeFalse("it could not seek");
    }

    [Fact]
    public void Loading_Time_Counts_From_Each_New_Load()
    {
        var pacer = new PlayAllPacer(Nine);
        pacer.Tick(TimeSpan.FromSeconds(10), [Loading()]);
        pacer.Tick(TimeSpan.FromSeconds(10), [Playing(Nine.AddSeconds(10))]);

        pacer.Tick(TimeSpan.FromSeconds(10), [Loading()]).Should().Equal(PlayAllAction.None);
    }

    [Fact]
    public void Paused_The_Clock_Stands_Still_And_Nothing_Is_Asked()
    {
        var pacer = new PlayAllPacer(Nine) { IsPaused = true };

        pacer.Tick(TimeSpan.FromSeconds(5), [Waiting(Nine), Playing(Nine), Playing(Nine.AddSeconds(3))]).Should().Equal(PlayAllAction.None, PlayAllAction.None, PlayAllAction.None);
        pacer.Clock.Should().Be(Nine);

        pacer.IsPaused = false;
        pacer.Tick(TimeSpan.Zero, [Waiting(Nine), Playing(Nine), Playing(Nine.AddSeconds(3))]).Should().Equal(PlayAllAction.Start, PlayAllAction.None, PlayAllAction.Hold);
    }

    private static PlayAllTile Waiting(DateTimeOffset nextStart)
        => new(PlayAllTileState.Waiting, NextStart: nextStart);

    private static PlayAllTile Playing(DateTimeOffset at)
        => new(PlayAllTileState.Playing, At: at);

    private static PlayAllTile Held(DateTimeOffset at)
        => new(PlayAllTileState.Held, At: at);

    private static PlayAllTile Loading()
        => new(PlayAllTileState.Loading);

    private static PlayAllTile Finished()
        => new(PlayAllTileState.Finished);

    private static PlayAllTile Failed()
        => new(PlayAllTileState.Failed);
}