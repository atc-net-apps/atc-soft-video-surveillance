namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

using CoreAction = AtcSoft.VideoSurveillance.Enums.PlayAllAction;
using CorePacer = AtcSoft.VideoSurveillance.Helpers.Activity.PlayAllPacer;
using CoreStart = AtcSoft.VideoSurveillance.Helpers.Activity.PlayAllStart;
using CoreState = AtcSoft.VideoSurveillance.Enums.PlayAllTileState;
using CoreTile = AtcSoft.VideoSurveillance.Models.PlayAllTile;

/// <summary>
/// *Play all from here* on the Timeline page: the same pace as the desktop clients, the commands a
/// tile gives its video element, and which cameras get a tile from which moment.
/// </summary>
public sealed class PlayAllTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_Pace_Is_The_Same_As_The_Desktop_Clients()
    {
        // Two tiles through a start, a slow tile, a hold, a pause, a gap, a tile that will not load.
        (double Elapsed, PlayAllTile[] Tiles, bool Paused)[] script =
        [
            (0, [Waiting(Nine.AddMinutes(-5)), Waiting(Nine.AddSeconds(30))], false),
            (0.25, [Loading(), Waiting(Nine.AddSeconds(30))], false),
            (0.25, [Playing(Nine.AddSeconds(0.25)), Waiting(Nine.AddSeconds(30))], false),
            (1, [Playing(Nine.AddSeconds(3)), Playing(Nine.AddSeconds(1))], false),
            (1, [Held(Nine.AddSeconds(3)), Playing(Nine.AddSeconds(2.9))], false),
            (5, [Playing(Nine.AddSeconds(3)), Playing(Nine.AddSeconds(3))], true),
            (1, [Waiting(Nine.AddHours(1)), Waiting(Nine.AddHours(2))], false),
            (10, [Loading(), Finished()], false),
            (10, [Loading(), Failed()], false),
            (8, [Playing(Nine.AddHours(1)), Failed()], false),
            (8, [Playing(Nine.AddHours(1)), Failed()], false),
        ];

        var web = new PlayAllPacer(Nine);
        var core = new CorePacer(Nine);
        foreach (var (elapsed, tiles, paused) in script)
        {
            web.IsPaused = paused;
            core.IsPaused = paused;

            var webActions = web.Tick(TimeSpan.FromSeconds(elapsed), tiles);
            var coreActions = core.Tick(TimeSpan.FromSeconds(elapsed), [.. tiles.Select(ToCore)]);

            webActions.Select(a => a.ToString()).Should().Equal(coreActions.Select(a => a.ToString()));
            web.Clock.Should().Be(core.Clock);
        }

        Enum.GetNames<PlayAllAction>().Should().Equal(Enum.GetNames<CoreAction>());
        Enum.GetNames<PlayAllTileState>().Should().Equal(Enum.GetNames<CoreState>());
        PlayAllPacer.HoldAhead.Should().Be(CorePacer.HoldAhead);
        PlayAllPacer.ResumeWithin.Should().Be(CorePacer.ResumeWithin);
        PlayAllPacer.GiveUpAfter.Should().Be(CorePacer.GiveUpAfter);
        PlayAllStart.Behind.Should().Be(CoreStart.Behind);
        PlayAllStart.Ahead.Should().Be(CoreStart.Ahead);
    }

    [Fact]
    public void A_Tile_Opens_Its_Recording_That_Far_In_And_Moves_On_To_The_Next()
    {
        var tile = new PlayAllTileLogic(
            "Garden",
            [new PlayAllSegment("a.mp4", Nine.AddMinutes(-10), Nine.AddMinutes(-10)), new PlayAllSegment("b.mp4", Nine.AddMinutes(5), Nine.AddMinutes(5))]);

        tile.Report(0).Should().Be(new PlayAllTile(PlayAllTileState.Waiting, NextStart: Nine.AddMinutes(-10)));
        tile.Apply(PlayAllAction.Start, Nine).Should().Be(new PlayAllCommand(PlayAllCommandKind.Open, "a.mp4", 600));
        tile.StatusText.Should().Be("Loading...");

        tile.Opened().Should().BeNull();
        tile.Report(601).Should().Be(new PlayAllTile(PlayAllTileState.Playing, At: Nine.AddSeconds(1)));
        tile.StatusText.Should().BeEmpty();

        tile.Apply(PlayAllAction.Hold, Nine).Should().Be(new PlayAllCommand(PlayAllCommandKind.Pause));
        tile.Apply(PlayAllAction.Resume, Nine).Should().Be(new PlayAllCommand(PlayAllCommandKind.Play));

        tile.Ended();
        tile.Report(0).NextStart.Should().Be(Nine.AddMinutes(5));
        tile.StatusText.Should().StartWith("Next recording at ");
    }

    [Fact]
    public void Paused_A_Tile_Stays_Paused_When_It_Opens_Or_Catches_Up_And_A_Tile_That_Will_Not_Load_Says_So()
    {
        var tile = new PlayAllTileLogic("Garden", [new PlayAllSegment("a.mp4", Nine, Nine)]);
        tile.Apply(PlayAllAction.Start, Nine);

        tile.SetPaused(true).Should().BeNull("a tile loading has nothing to pause yet");
        tile.Opened().Should().Be(new PlayAllCommand(PlayAllCommandKind.Pause));
        tile.Report(0).State.Should().Be(PlayAllTileState.Playing);
        tile.Apply(PlayAllAction.Hold, Nine);
        tile.Apply(PlayAllAction.Resume, Nine).Should().BeNull();
        tile.SetPaused(false).Should().Be(new PlayAllCommand(PlayAllCommandKind.Play));

        var stuck = new PlayAllTileLogic("Porch", [new PlayAllSegment("b.mp4", Nine, Nine)]);
        stuck.Apply(PlayAllAction.Start, Nine);
        stuck.Apply(PlayAllAction.GiveUp, Nine).Should().Be(new PlayAllCommand(PlayAllCommandKind.Close));
        stuck.StatusText.Should().Be("Could not be played");
    }

    [Fact]
    public void A_Recording_That_Opens_Far_From_Where_It_Was_Asked_Does_Not_Count_As_Playing_And_One_That_Fails_Is_Skipped()
    {
        var tile = new PlayAllTileLogic(
            "Garden",
            [new PlayAllSegment("a.mp4", Nine.AddMinutes(-10), Nine.AddMinutes(-10)), new PlayAllSegment("b.mp4", Nine.AddMinutes(5), Nine.AddMinutes(5))]);
        tile.Apply(PlayAllAction.Start, Nine);
        tile.Opened();

        tile.Report(0).State.Should().Be(PlayAllTileState.Loading, "asked for 600 s, it started at 0");
        tile.Failed().Should().Be(new PlayAllCommand(PlayAllCommandKind.Close));
        tile.Report(0).Should().Be(new PlayAllTile(PlayAllTileState.Waiting, NextStart: Nine.AddMinutes(5)));
        tile.StatusText.Should().Be("Could not be played");
    }

    [Fact]
    public void Play_All_Starts_At_The_Place_Last_Played_Or_The_First_Recording_And_Plays_At_Most_Four_Cameras()
    {
        var span = new TimelineSpan(Nine, Nine.AddHours(4));
        List<TimelineLane> lanes =
        [
            .. Enumerable.Range(0, 5).Select(i => Lane($"Cam{i}", new TimelineCoverage(Nine.AddHours(1), Nine.AddHours(2), $"Cam{i}/a.mp4", Nine.AddHours(1)))),
            Lane("Gate", new TimelineCoverage(Nine.AddMinutes(10), Nine.AddMinutes(20), "Gate/a.mp4", Nine.AddMinutes(10))),
            Lane("Empty"),
        ];

        PlayAllPlan.Moment(lanes, cursor: null, span).Should().Be(Nine.AddMinutes(10));
        PlayAllPlan.Moment(lanes, cursor: Nine.AddHours(1.5), span).Should().Be(Nine.AddHours(1.5));

        var tiles = PlayAllPlan.Tiles(lanes, Nine.AddMinutes(30), path => "http://server/" + path);
        tiles.Select(t => t.CameraName).Should().Equal("Cam0", "Cam1", "Cam2", "Cam3");
        PlayAllPlan.LimitText(lanes, Nine.AddMinutes(30)).Should().Be("Shows the first 4 cameras");
        PlayAllPlan.LimitText(lanes.Take(2), Nine.AddMinutes(30)).Should().BeEmpty();
        PlayAllPlan.GridStyle(4).Should().Contain("repeat(2, 1fr)");
        PlayAllPlan.TileStyle(3).Should().Contain("max-height: calc(70vh / 2)");
        PlayAllPlan.TileStyle(1).Should().Contain("max-height: calc(70vh / 1)");
    }

    private static TimelineLane Lane(
        string name,
        params TimelineCoverage[] coverage)
        => new(Guid.NewGuid(), name, [.. coverage], [], [], []);

    private static CoreTile ToCore(PlayAllTile tile)
        => new(Enum.Parse<CoreState>(tile.State.ToString()), tile.At, tile.NextStart);

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