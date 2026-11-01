namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

using AtcSoft.Video.Surveillance.Events;

/// <summary>
/// A recording camera's player goes on without a tile while it is parked, and is let go when
/// nothing needs it any more.
/// </summary>
public sealed class RecordingPlayerParkingTests
{
    private static readonly Guid Camera = Guid.NewGuid();

    private readonly List<Action> posted = [];
    private readonly IVideoPlayer player = Substitute.For<IVideoPlayer>();
    private readonly IMediaPipeline pipeline = Substitute.For<IMediaPipeline>();
    private readonly IRecordingService recording = Substitute.For<IRecordingService>();

    public RecordingPlayerParkingTests()
    {
        player.State.Returns(PlayerState.Playing);
        recording.IsRecording(Camera).Returns(true);
    }

    [Fact]
    public void A_Parked_Player_Is_Taken_Back_As_It_Is()
    {
        using var sut = new RecordingPlayerParking(posted.Add);
        sut.Park(Camera, player, pipeline, recording);

        sut.TryTake(Camera, out var taken, out var takenPipeline).Should().BeTrue();

        taken.Should().BeSameAs(player);
        takenPipeline.Should().BeSameAs(pipeline);
        sut.IsParked(Camera).Should().BeFalse();
        recording.DidNotReceive().StopRecording(Camera);
        player.DidNotReceive().Dispose();
    }

    [Fact]
    public void A_Camera_Without_A_Parked_Player_Has_Nothing_To_Take()
    {
        using var sut = new RecordingPlayerParking(posted.Add);

        sut.TryTake(Camera, out var taken, out _).Should().BeFalse();
        taken.Should().BeNull();
    }

    [Fact]
    public void A_Parked_Player_Whose_Recording_Stopped_Is_Let_Go()
    {
        using var sut = new RecordingPlayerParking(posted.Add);
        sut.Park(Camera, player, pipeline, recording);

        recording.IsRecording(Camera).Returns(false);
        recording.RecordingStateChanged += Raise.EventWith(new RecordingStateChangedEventArgs(Camera, RecordingState.Recording, RecordingState.Idle));
        RunPosted();

        sut.IsParked(Camera).Should().BeFalse();
        pipeline.Received(1).Dispose();
        player.Received(1).Dispose();
    }

    [Fact]
    public void A_Segment_Switch_Keeps_The_Player_Parked()
    {
        using var sut = new RecordingPlayerParking(posted.Add);
        sut.Park(Camera, player, pipeline, recording);

        // The hourly switch reports idle, then recording again, before the check runs.
        recording.RecordingStateChanged += Raise.EventWith(new RecordingStateChangedEventArgs(Camera, RecordingState.Recording, RecordingState.Idle));
        recording.RecordingStateChanged += Raise.EventWith(new RecordingStateChangedEventArgs(Camera, RecordingState.Idle, RecordingState.Recording));
        RunPosted();

        sut.IsParked(Camera).Should().BeTrue();
        player.DidNotReceive().Dispose();
    }

    [Fact]
    public void A_Parked_Player_Whose_Stream_Ended_Is_Let_Go_And_Its_Recording_Stopped()
    {
        using var sut = new RecordingPlayerParking(posted.Add);
        sut.Park(Camera, player, pipeline, recording);

        player.State.Returns(PlayerState.Error);
        player.StateChanged += Raise.EventWith(new PlayerStateChangedEventArgs(PlayerState.Playing, PlayerState.Error));
        RunPosted();

        sut.IsParked(Camera).Should().BeFalse();
        recording.Received(1).StopRecording(Camera);
        player.Received(1).Dispose();
    }

    [Fact]
    public void Another_Cameras_Recording_Leaves_The_Player_Parked()
    {
        using var sut = new RecordingPlayerParking(posted.Add);
        sut.Park(Camera, player, pipeline, recording);

        recording.RecordingStateChanged += Raise.EventWith(new RecordingStateChangedEventArgs(Guid.NewGuid(), RecordingState.Recording, RecordingState.Idle));
        RunPosted();

        sut.IsParked(Camera).Should().BeTrue();
    }

    [Fact]
    public void A_Player_Taken_Back_Is_No_Longer_Watched()
    {
        using var sut = new RecordingPlayerParking(posted.Add);
        sut.Park(Camera, player, pipeline, recording);
        sut.TryTake(Camera, out _, out _);

        recording.IsRecording(Camera).Returns(false);
        recording.RecordingStateChanged += Raise.EventWith(new RecordingStateChangedEventArgs(Camera, RecordingState.Recording, RecordingState.Idle));
        RunPosted();

        player.DidNotReceive().Dispose();
    }

    [Fact]
    public void Disposing_The_Parking_Stops_And_Closes_What_It_Holds()
    {
        var sut = new RecordingPlayerParking(posted.Add);
        sut.Park(Camera, player, pipeline, recording);

        sut.Dispose();

        recording.Received(1).StopRecording(Camera);
        player.Received(1).Dispose();
        sut.Count.Should().Be(0);
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