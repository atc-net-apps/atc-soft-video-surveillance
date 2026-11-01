namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// A stream the viewer asked for stays wanted through failures and is started
/// again a little later each time, until the viewer stops it.
/// </summary>
public sealed class WantedStreamsTests
{
    private static readonly Guid Front = Guid.NewGuid();

    [Fact]
    public void A_Stream_That_Keeps_Failing_Waits_Longer_Each_Time_Up_To_A_Minute()
    {
        var sut = new WantedStreams();
        sut.Want(Front);

        var waits = Enumerable.Range(0, 7).Select(_ => sut.Failed(Front)).ToList();

        waits.Should().Equal(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(40),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void A_Stream_That_Plays_Again_Waits_The_Shortest_Time_After_Its_Next_Failure()
    {
        var sut = new WantedStreams();
        sut.Want(Front);
        sut.Failed(Front);
        sut.Failed(Front);

        sut.Started(Front);

        sut.Failed(Front).Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void A_Stream_The_Viewer_Stopped_Is_Not_Started_Again()
    {
        var sut = new WantedStreams();
        sut.Want(Front);

        sut.Forget(Front);

        sut.IsWanted(Front).Should().BeFalse();
        sut.Failed(Front).Should().BeNull();
        sut.Cameras.Should().BeEmpty();
    }

    [Fact]
    public void Asking_Again_Keeps_The_Count()
    {
        var sut = new WantedStreams();
        sut.Want(Front);
        sut.Failed(Front);

        sut.Want(Front);

        sut.Failed(Front).Should().Be(TimeSpan.FromSeconds(10));
    }
}