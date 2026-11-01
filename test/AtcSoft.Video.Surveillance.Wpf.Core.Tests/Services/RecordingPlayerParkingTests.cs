namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

/// <summary>
/// A recording camera's player goes on without a tile while it is parked, and is let go when
/// nothing needs it any more.
/// </summary>
public sealed class RecordingPlayerParkingTests
{
    private static readonly Guid Camera = Guid.NewGuid();

    private readonly List<Action> posted = [];
    private readonly List<(TimeSpan Delay, Action Action)> scheduled = [];
    private readonly IVideoPlayer player = Substitute.For<IVideoPlayer>();
    private readonly IMediaPipeline pipeline = Substitute.For<IMediaPipeline>();
    private readonly IRecordingService recording = Substitute.For<IRecordingService>();
    private int cancelled;

    public RecordingPlayerParkingTests()
    {
        player.State.Returns(PlayerState.Playing);
        recording.IsRecording(Camera).Returns(true);
        recording.IsRecordingOrResuming(Camera).Returns(true);
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The parking hands back the substitutes the test gave it.")]
    public void A_Parked_Player_Is_Taken_Back_As_It_Is()
    {
        using var sut = new RecordingPlayerParking(posted.Add, Schedule);
        sut.Park(Camera, player, pipeline, recording);

        sut.TryTake(Camera, out var taken, out var takenPipeline).Should().BeTrue();

        taken.Should().BeSameAs(player);
        takenPipeline.Should().BeSameAs(pipeline);
        sut.IsParked(Camera).Should().BeFalse();
        recording.DidNotReceive().StopRecording(Camera);
        player.DidNotReceive().Dispose();
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Nothing is handed back.")]
    public void A_Camera_Without_A_Parked_Player_Has_Nothing_To_Take()
    {
        using var sut = new RecordingPlayerParking(posted.Add, Schedule);

        sut.TryTake(Camera, out var taken, out _).Should().BeFalse();
        taken.Should().BeNull();
    }

    [Fact]
    public void A_Parked_Player_Whose_Recording_Stopped_Is_Let_Go()
    {
        using var sut = new RecordingPlayerParking(posted.Add, Schedule);
        sut.Park(Camera, player, pipeline, recording);

        recording.IsRecording(Camera).Returns(false);
        recording.IsRecordingOrResuming(Camera).Returns(false);
        recording.RecordingStateChanged += Raise.EventWith(new RecordingStateChangedEventArgs(Camera, RecordingState.Recording, RecordingState.Idle));
        RunPosted();

        sut.IsParked(Camera).Should().BeFalse();
        pipeline.Received(1).Dispose();
        player.Received(1).Dispose();
    }

    [Fact]
    public void A_Camera_Watched_For_Motion_Stays_Parked_Between_Its_Recordings_And_Its_Watching_Stops_With_The_Parking()
    {
        var stoppedWatching = 0;
        var sut = new RecordingPlayerParking(posted.Add, Schedule);
        sut.Park(Camera, player, pipeline, recording, keepWhileIdle: true, onRelease: () => stoppedWatching++);

        // A motion recording ends: the camera waits for the next motion, parked.
        recording.IsRecording(Camera).Returns(false);
        recording.IsRecordingOrResuming(Camera).Returns(false);
        recording.RecordingStateChanged += Raise.EventWith(new RecordingStateChangedEventArgs(Camera, RecordingState.RecordingPostMotion, RecordingState.Idle));
        RunPosted();

        sut.IsParked(Camera).Should().BeTrue("it is watched for motion while no tile shows it");
        player.DidNotReceive().Dispose();
        stoppedWatching.Should().Be(0);

        sut.Dispose();

        stoppedWatching.Should().Be(1, "the watching stops before the player is closed");
        player.Received(1).Dispose();
    }

    [Fact]
    public void A_Segment_Switch_Keeps_The_Player_Parked()
    {
        using var sut = new RecordingPlayerParking(posted.Add, Schedule);
        sut.Park(Camera, player, pipeline, recording);

        // The hourly switch reports idle, then recording again, before the check runs.
        recording.RecordingStateChanged += Raise.EventWith(new RecordingStateChangedEventArgs(Camera, RecordingState.Recording, RecordingState.Idle));
        recording.RecordingStateChanged += Raise.EventWith(new RecordingStateChangedEventArgs(Camera, RecordingState.Idle, RecordingState.Recording));
        RunPosted();

        sut.IsParked(Camera).Should().BeTrue();
        player.DidNotReceive().Dispose();
    }

    [Fact]
    public async Task A_Parked_Camera_Whose_Stream_Ended_Is_Opened_Again_And_Its_Recording_Goes_On()
    {
        using var opened = new SemaphoreSlim(0);
        using var sut = new RecordingPlayerParking(posted.Add, Schedule);
        sut.Park(Camera, player, pipeline, recording, _ =>
        {
            player.State.Returns(PlayerState.Playing);
            opened.Release();
        });

        EndStream();

        sut.IsParked(Camera).Should().BeTrue("a blip does not end the recording");
        recording.DidNotReceive().StopRecording(Camera);
        scheduled.Should().ContainSingle().Which.Delay.Should().Be(RecordingPlayerParking.ReopenFirstDelay);

        RunScheduled();
        (await opened.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Should().BeTrue();
        await RunPostedAsync();

        recording.Received().ResumeRecording(Camera);
        player.DidNotReceive().Dispose();
    }

    [Fact]
    public async Task A_Stream_That_Will_Not_Open_Is_Tried_Again_Less_And_Less_Often()
    {
        using var tried = new SemaphoreSlim(0);
        using var sut = new RecordingPlayerParking(posted.Add, Schedule);
        sut.Park(Camera, player, pipeline, recording, _ => tried.Release());

        EndStream();
        var delays = new List<TimeSpan>();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            delays.Add(scheduled.Single().Delay);
            RunScheduled();
            (await tried.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Should().BeTrue();
            await RunPostedAsync();
        }

        delays.Should().Equal(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(40),
            RecordingPlayerParking.ReopenMaxDelay,
            RecordingPlayerParking.ReopenMaxDelay);
        sut.IsParked(Camera).Should().BeTrue();
    }

    [Fact]
    public void A_Recording_The_Service_Stopped_For_Now_Keeps_The_Player_Parked()
    {
        using var sut = new RecordingPlayerParking(posted.Add, Schedule);
        sut.Park(Camera, player, pipeline, recording);

        // A full disk: the session is gone, but the service starts it again by itself.
        recording.IsRecording(Camera).Returns(false);
        recording.RecordingStateChanged += Raise.EventWith(new RecordingStateChangedEventArgs(Camera, RecordingState.Recording, RecordingState.Idle));
        RunPosted();

        sut.IsParked(Camera).Should().BeTrue();
        player.DidNotReceive().Dispose();
    }

    [Fact]
    public void A_Parked_Player_That_Cannot_Be_Opened_Again_Is_Let_Go_When_Its_Stream_Ends()
    {
        using var sut = new RecordingPlayerParking(posted.Add, Schedule);
        sut.Park(Camera, player, pipeline, recording);

        EndStream();

        sut.IsParked(Camera).Should().BeFalse();
        recording.Received(1).StopRecording(Camera);
        player.Received(1).Dispose();
    }

    [Fact]
    public void A_Player_Taken_Back_While_A_Reopen_Waits_Is_Not_Opened_By_The_Parking()
    {
        var reopened = false;
        using var sut = new RecordingPlayerParking(posted.Add, Schedule);
        sut.Park(Camera, player, pipeline, recording, _ => reopened = true);
        EndStream();

        sut.TryTake(Camera, out _, out _);
        RunScheduled();

        reopened.Should().BeFalse();
        cancelled.Should().Be(1);
    }

    [Fact]
    public void Another_Cameras_Recording_Leaves_The_Player_Parked()
    {
        using var sut = new RecordingPlayerParking(posted.Add, Schedule);
        sut.Park(Camera, player, pipeline, recording);

        recording.RecordingStateChanged += Raise.EventWith(new RecordingStateChangedEventArgs(Guid.NewGuid(), RecordingState.Recording, RecordingState.Idle));
        RunPosted();

        sut.IsParked(Camera).Should().BeTrue();
    }

    [Fact]
    public void A_Player_Taken_Back_Is_No_Longer_Watched()
    {
        using var sut = new RecordingPlayerParking(posted.Add, Schedule);
        sut.Park(Camera, player, pipeline, recording);
        sut.TryTake(Camera, out _, out _);

        recording.IsRecording(Camera).Returns(false);
        recording.IsRecordingOrResuming(Camera).Returns(false);
        recording.RecordingStateChanged += Raise.EventWith(new RecordingStateChangedEventArgs(Camera, RecordingState.Recording, RecordingState.Idle));
        RunPosted();

        player.DidNotReceive().Dispose();
    }

    [Fact]
    public void Disposing_The_Parking_Stops_And_Closes_What_It_Holds()
    {
        var sut = new RecordingPlayerParking(posted.Add, Schedule);
        sut.Park(Camera, player, pipeline, recording);

        sut.Dispose();

        recording.Received(1).StopRecording(Camera);
        player.Received(1).Dispose();
        sut.Count.Should().Be(0);
    }

    private Action Schedule(
        TimeSpan delay,
        Action action)
    {
        var item = (delay, action);
        scheduled.Add(item);
        return () =>
        {
            cancelled++;
            scheduled.Remove(item);
        };
    }

    private void RunScheduled()
    {
        foreach (var (_, action) in scheduled.ToList())
        {
            scheduled.RemoveAt(0);
            action();
        }
    }

    private void EndStream()
    {
        player.State.Returns(PlayerState.Error);
        player.StateChanged += Raise.EventWith(new PlayerStateChangedEventArgs(PlayerState.Playing, PlayerState.Error));
        RunPosted();
    }

    // The reopen runs in the background and posts its end; waits for that post.
    private async Task RunPostedAsync()
    {
        var waited = TimeSpan.Zero;
        while (posted.Count == 0 && waited < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken).ConfigureAwait(false);
            waited += TimeSpan.FromMilliseconds(10);
        }

        RunPosted();
    }

    private void RunPosted()
    {
        foreach (var action in posted.ToList())
        {
            action();
        }

        posted.Clear();
    }
}