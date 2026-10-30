namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// A camera that records is analysed for its activity index even when its tile does not watch
/// it for motion. Such a camera reports no motion, and the tile starting, restarting or
/// stopping its motion watch never ends the index of a recording in progress.
/// </summary>
public sealed class MotionDetectionServiceIndexTests : IDisposable
{
    private readonly Guid camera = Guid.NewGuid();
    private readonly IMediaPipeline pipeline = NullPipeline.New();
    private readonly MotionDetectionService sut = new(NullLogger<MotionDetectionService>.Instance, new NoPictures());

    public void Dispose()
    {
        sut.Dispose();
        pipeline.Dispose();
    }

    [Fact]
    public void A_Camera_Analysed_Only_For_Its_Index_Is_Not_Watched_For_Motion()
    {
        sut.StartIndexing(camera, pipeline);

        sut.IsIndexing(camera).Should().BeTrue();
        sut.IsDetectionActive(camera).Should().BeFalse();
    }

    [Fact]
    public void The_Tile_Watching_And_Letting_Go_Leaves_The_Index_Running()
    {
        sut.StartIndexing(camera, pipeline);

        sut.StartDetection(camera, pipeline);
        sut.IsDetectionActive(camera).Should().BeTrue();
        sut.IsIndexing(camera).Should().BeTrue();

        // A changed setting makes the tile start again.
        sut.StartDetection(camera, pipeline, new MotionDetectionSettings { Sensitivity = 80 });
        sut.IsIndexing(camera).Should().BeTrue();

        sut.StopDetection(camera);
        sut.IsDetectionActive(camera).Should().BeFalse();
        sut.IsIndexing(camera).Should().BeTrue();
    }

    [Fact]
    public void When_The_Index_Ends_A_Watched_Camera_Stays_Watched_And_Another_Is_Let_Go()
    {
        var other = Guid.NewGuid();
        sut.StartDetection(camera, pipeline);
        sut.StartIndexing(camera, pipeline);
        sut.StartIndexing(other, pipeline);

        sut.StopIndexing(camera);
        sut.StopIndexing(other);

        sut.IsDetectionActive(camera).Should().BeTrue();
        sut.IsIndexing(camera).Should().BeFalse();
        sut.IsIndexing(other).Should().BeFalse();
        sut.IsDetectionActive(other).Should().BeFalse();
    }

    private sealed class NoPictures : IMotionFrameReader
    {
        public byte[]? ReadGrayscale(
            byte[] encodedImage,
            int width,
            int height)
            => null;
    }
}