namespace AtcSoft.VideoSurveillance.Wpf.Core.Windows;

/// <summary>
/// *Play all from here* in the desktop apps: each tile plays its recordings as the shared clock
/// reaches them, holds when it runs ahead, says why it shows no picture, and lets go of its player.
/// </summary>
public sealed class PlayAllWindowTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_Tile_Opens_The_Recording_Covering_The_Moment_That_Far_In_And_Moves_On_To_The_Next_When_The_Clock_Gets_There()
    {
        var media = new FakeMedia();
        var tile = Tile(
            media,
            new PlayAllSegment(new Uri("http://server/a.mkv"), Nine.AddMinutes(-10), Nine.AddMinutes(-10)),
            new PlayAllSegment(new Uri("http://server/b.mkv"), Nine.AddMinutes(5), Nine.AddMinutes(5)));
        using var window = new PlayAllWindowViewModel(Nine, [tile]);

        window.Step(TimeSpan.Zero);
        media.Opens.Should().Equal((new Uri("http://server/a.mkv"), TimeSpan.FromMinutes(10)));
        tile.State.Should().Be(PlayAllTileState.Loading);

        media.RaiseOpened();
        tile.Report();
        tile.State.Should().Be(PlayAllTileState.Playing);
        tile.StatusText.Should().BeEmpty();

        media.RaiseEnded();
        tile.State.Should().Be(PlayAllTileState.Waiting);
        tile.StatusText.Should().StartWith("Next recording at ");

        // Nothing plays: the clock jumps over the gap.
        window.Step(TimeSpan.FromSeconds(1));
        media.Opens.Should().HaveCount(2);
        media.Opens[1].Should().Be((new Uri("http://server/b.mkv"), TimeSpan.Zero));
    }

    [Fact]
    public void A_Tile_Ahead_Of_The_Others_Is_Held_And_Plays_Again_When_They_Catch_Up()
    {
        var fast = new FakeMedia();
        var slow = new FakeMedia();
        var segment = new PlayAllSegment(new Uri("http://server/a.mkv"), Nine, Nine);
        var fastTile = Tile(fast, segment);
        var slowTile = Tile(slow, segment);
        using var window = new PlayAllWindowViewModel(Nine, [fastTile, slowTile]);
        window.Step(TimeSpan.Zero);
        fast.RaiseOpened();
        slow.RaiseOpened();

        fast.Position = TimeSpan.FromSeconds(3);
        slow.Position = TimeSpan.FromSeconds(1);
        window.Step(TimeSpan.FromSeconds(1));
        fastTile.State.Should().Be(PlayAllTileState.Held);
        fast.IsPlaying.Should().BeFalse();

        slow.Position = TimeSpan.FromSeconds(2.9);
        window.Step(TimeSpan.FromSeconds(1));
        fastTile.State.Should().Be(PlayAllTileState.Playing);
        fast.IsPlaying.Should().BeTrue();
    }

    [Fact]
    public void One_Pause_Stops_Every_Tile_And_Closing_Lets_Go_Of_Every_Player()
    {
        var media = new FakeMedia();
        var tile = Tile(media, new PlayAllSegment(new Uri("http://server/a.mkv"), Nine, Nine));
        var window = new PlayAllWindowViewModel(Nine, [tile]);
        window.Step(TimeSpan.Zero);
        media.RaiseOpened();
        tile.Report();

        window.PlayPauseCommand.Execute(parameter: null);
        media.IsPlaying.Should().BeFalse();
        window.PlayPauseText.Should().Be("Play");

        window.PlayPauseCommand.Execute(parameter: null);
        media.IsPlaying.Should().BeTrue();

        window.Dispose();
        media.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void A_Recording_That_Cannot_Be_Played_Says_So()
    {
        var media = new FakeMedia();
        var tile = Tile(media, new PlayAllSegment(new Uri("http://server/a.mkv"), Nine, Nine));
        using var window = new PlayAllWindowViewModel(Nine, [tile]);
        window.Step(TimeSpan.Zero);

        media.RaiseFailed();

        tile.State.Should().Be(PlayAllTileState.Failed);
        tile.StatusText.Should().Be("Could not be played");
    }

    [Fact]
    public void A_Recording_That_Opens_Far_From_Where_It_Was_Asked_Does_Not_Count_As_Playing()
    {
        var media = new FakeMedia();
        var tile = Tile(media, new PlayAllSegment(new Uri("http://server/a.mkv"), Nine.AddMinutes(-10), Nine.AddMinutes(-10)));
        using var window = new PlayAllWindowViewModel(Nine, [tile]);
        window.Step(TimeSpan.Zero);

        // Asked for 10 minutes in, it starts at the beginning: it cannot seek.
        media.Position = TimeSpan.Zero;
        media.RaiseOpened();

        tile.Report().State.Should().Be(PlayAllTileState.Loading, "it would pull every other tile ten minutes back");
        media.Position = TimeSpan.FromMinutes(10);
        tile.Report().State.Should().Be(PlayAllTileState.Playing);
    }

    [Fact]
    public void After_A_Recording_That_Cannot_Be_Played_The_Tile_Moves_On_To_The_Next()
    {
        var media = new FakeMedia();
        var tile = Tile(
            media,
            new PlayAllSegment(new Uri("http://server/a.mkv"), Nine, Nine),
            new PlayAllSegment(new Uri("http://server/b.mkv"), Nine.AddMinutes(5), Nine.AddMinutes(5)));
        using var window = new PlayAllWindowViewModel(Nine, [tile]);
        window.Step(TimeSpan.Zero);

        media.RaiseFailed();

        tile.State.Should().Be(PlayAllTileState.Waiting);
        tile.StatusText.Should().Be("Could not be played");
        tile.Report().NextStart.Should().Be(Nine.AddMinutes(5));
    }

    [Fact]
    public void More_Cameras_Than_Fit_Are_Left_Out_And_The_Window_Says_So()
    {
        var tiles = Enumerable.Range(0, 12).Select(_ => new PlayAllTileViewModel("Cam", [])).ToList();

        using var window = new PlayAllWindowViewModel(Nine, tiles);

        window.Tiles.Should().HaveCount(PlayAllWindowViewModel.MaxTiles);
        window.Columns.Should().Be(3);
        window.LimitText.Should().Be("Shows the first 9 cameras");
        window.Tiles[0].StatusText.Should().Be("No more recordings");
    }

    [Fact]
    public void The_Window_Can_Be_Created()
    {
        var error = WpfTestThread.Run(() =>
        {
            var viewModel = new PlayAllWindowViewModel(
                Nine,
                [new PlayAllTileViewModel("Garden", [new PlayAllSegment(new Uri(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "no-such-recording.mkv")), Nine, Nine)])]);
            var window = new PlayAllWindow(viewModel);
            window.Close();
        });

        error.Should().BeNull();
    }

    private static PlayAllTileViewModel Tile(
        FakeMedia media,
        params PlayAllSegment[] segments)
    {
        var tile = new PlayAllTileViewModel("Garden", segments);
        tile.Attach(media);
        return tile;
    }

    private sealed class FakeMedia : IPlayAllMedia
    {
        public event EventHandler? Opened;

        public event EventHandler? Ended;

        public event EventHandler? Failed;

        public List<(Uri Address, TimeSpan Position)> Opens { get; } = [];

        public TimeSpan Position { get; set; }

        public bool IsPlaying { get; private set; }

        public bool IsClosed { get; private set; }

        public void Open(
            Uri address,
            TimeSpan position)
        {
            Opens.Add((address, position));
            Position = position;
            IsPlaying = true;
        }

        public void Play()
            => IsPlaying = true;

        public void Pause()
            => IsPlaying = false;

        public void Close()
        {
            IsPlaying = false;
            IsClosed = true;
        }

        public void RaiseOpened()
            => Opened?.Invoke(this, EventArgs.Empty);

        public void RaiseEnded()
            => Ended?.Invoke(this, EventArgs.Empty);

        public void RaiseFailed()
            => Failed?.Invoke(this, EventArgs.Empty);
    }
}