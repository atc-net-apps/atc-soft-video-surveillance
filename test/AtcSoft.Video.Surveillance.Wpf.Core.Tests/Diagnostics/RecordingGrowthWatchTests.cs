namespace AtcSoft.Video.Surveillance.Wpf.Core.Diagnostics;

/// <summary>
/// A running recording whose file stops growing is caught after a while, once; a new file starts over.
/// </summary>
public sealed class RecordingGrowthWatchTests
{
    private static readonly Guid Camera = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_File_That_Grows_Is_Growing()
    {
        var sut = new RecordingGrowthWatch(TimeSpan.FromMinutes(3));

        sut.Observe(Start, [File("a.mkv", 100)]).Single().State.Should().Be(RecordingGrowthState.New);
        var second = sut.Observe(Start.AddMinutes(1), [File("a.mkv", 900)]).Single();

        second.State.Should().Be(RecordingGrowthState.Growing);
        second.GrowthBytes.Should().Be(800);
    }

    [Fact]
    public void A_File_That_Stops_Growing_Is_Stalled_Once_After_The_Stall_Time_And_Resumes_When_It_Grows()
    {
        var sut = new RecordingGrowthWatch(TimeSpan.FromMinutes(3));
        sut.Observe(Start, [File("a.mkv", 100)]);
        sut.Observe(Start.AddMinutes(1), [File("a.mkv", 500)]);

        sut.Observe(Start.AddMinutes(2), [File("a.mkv", 500)]).Single().State.Should().Be(RecordingGrowthState.Waiting);
        sut.Observe(Start.AddMinutes(3), [File("a.mkv", 500)]).Single().State.Should().Be(RecordingGrowthState.Waiting);
        var stalled = sut.Observe(Start.AddMinutes(4), [File("a.mkv", 500)]).Single();
        stalled.State.Should().Be(RecordingGrowthState.Stalled);
        stalled.StalledFor.Should().Be(TimeSpan.FromMinutes(3));
        sut.Observe(Start.AddMinutes(5), [File("a.mkv", 500)]).Single().State.Should().Be(RecordingGrowthState.StillStalled);

        sut.Observe(Start.AddMinutes(6), [File("a.mkv", 600)]).Single().State.Should().Be(RecordingGrowthState.Resumed);
        sut.Observe(Start.AddMinutes(7), [File("a.mkv", 700)]).Single().State.Should().Be(RecordingGrowthState.Growing);
    }

    [Fact]
    public void A_New_File_Starts_Over()
    {
        var sut = new RecordingGrowthWatch(TimeSpan.FromMinutes(3));
        sut.Observe(Start, [File("a.mkv", 100)]);
        sut.Observe(Start.AddMinutes(5), [File("a.mkv", 100)]).Single().State.Should().Be(RecordingGrowthState.Stalled);

        sut.Observe(Start.AddMinutes(6), [File("b.mkv", 10)]).Single().State.Should().Be(RecordingGrowthState.New);
        sut.Observe(Start.AddMinutes(7), [File("b.mkv", 10)]).Single().State.Should().Be(RecordingGrowthState.Waiting);
    }

    [Fact]
    public void A_Recording_That_Ended_Is_Forgotten()
    {
        var sut = new RecordingGrowthWatch(TimeSpan.FromMinutes(3));
        sut.Observe(Start, [File("a.mkv", 100)]);

        sut.Observe(Start.AddMinutes(1), []).Should().BeEmpty();

        sut.Observe(Start.AddMinutes(10), [File("a.mkv", 100)]).Single().State.Should().Be(RecordingGrowthState.New, "the camera records again");
    }

    private static RecordingFile File(
        string name,
        long length)
        => new(Camera, "Front", @"C:\recordings\Front\" + name, length);
}