namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// The previews of a recording are made when it or its segment ends, once however often it is
/// asked for, never while it is still being written, and a failure leaves it to be asked again.
/// </summary>
public sealed class RecordingPreviewMakerTests : IDisposable
{
    private static readonly Guid Camera = Guid.NewGuid();

    private readonly FakeActiveRecordings recordings = new();
    private readonly IRecordingPreviewBuilder builder = Substitute.For<IRecordingPreviewBuilder>();
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-previewmaker-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_Segment_That_Ends_Is_Queued_And_The_One_Being_Written_Is_Not()
    {
        var (first, second) = (Existing("a.mp4"), Existing("b.mp4"));
        var sut = Started();
        recordings.Start(Camera, first, Substitute.For<IMediaPipeline>());

        recordings.Segment(Camera, second);

        sut.Contains(first).Should().BeTrue("its segment ended");
        sut.TryEnqueue(second).Should().BeFalse("it is still being written");
        sut.RunPending(TestContext.Current.CancellationToken);
        builder.Received(1).Build(first, Arg.Any<CancellationToken>());
        sut.Contains(first).Should().BeFalse();
    }

    [Fact]
    public void A_Recording_Asked_For_Twice_Is_Made_Once()
    {
        var video = Existing("c.mp4");
        var sut = Started();

        sut.TryEnqueue(video).Should().BeTrue();
        sut.TryEnqueue(video).Should().BeFalse("it is queued already");
        sut.RunPending(TestContext.Current.CancellationToken);

        builder.Received(1).Build(video, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void A_Failure_Is_Logged_And_The_Recording_Can_Be_Asked_For_Again()
    {
        var video = Existing("d.mp4");
        builder.Build(video, Arg.Any<CancellationToken>()).Returns(_ => throw new IOException("disk full"));
        var sut = Started();

        sut.TryEnqueue(video);
        var made = sut.RunPending(TestContext.Current.CancellationToken);

        made.Should().Be(0);
        sut.Contains(video).Should().BeFalse();
        sut.TryEnqueue(video).Should().BeTrue();
    }

    [Fact]
    public void Without_A_Builder_Nothing_Is_Queued()
    {
        var sut = new RecordingPreviewMaker(NullLogger.Instance, recordings, builder: null);

        sut.TryEnqueue(Existing("e.mp4")).Should().BeFalse();
    }

    [Fact]
    public void After_Stop_An_Ending_Recording_Is_No_Longer_Followed()
    {
        var video = Existing("f.mp4");
        var sut = Started();
        sut.Stop();
        recordings.Start(Camera, video, Substitute.For<IMediaPipeline>());

        recordings.Stop(Camera);

        sut.Contains(video).Should().BeFalse();
    }

    private RecordingPreviewMaker Started()
    {
        var sut = new RecordingPreviewMaker(NullLogger.Instance, recordings, builder);
        sut.Start();
        return sut;
    }

    // A file that exists, so the maker does not pass it over as gone.
    private string Existing(string name)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, "x");
        return path;
    }
}