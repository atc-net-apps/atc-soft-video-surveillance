namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// A picture is analysed at the size it was captured at, even when the camera's display size
/// changes while it is captured (a tile made anew when the desktop session comes back).
/// </summary>
public sealed class MotionDetectionServiceSizeTests : IDisposable
{
    private readonly Guid camera = Guid.NewGuid();
    private readonly MotionDetectionService sut = new(NullLogger<MotionDetectionService>.Instance, new NoPictures());

    public void Dispose()
        => sut.Dispose();

    [Fact]
    public async Task A_Display_Size_That_Changes_During_A_Capture_Does_Not_Break_The_Analysis()
    {
        using var pipeline = ResizingPipeline.New();
        var analysed = 0;
        sut.PictureAnalyzed += (_, _) => analysed++;
        sut.StartIndexing(camera, pipeline);

        await sut.AnalyzeOnceAsync(camera);
        await sut.AnalyzeOnceAsync(camera);

        analysed.Should().Be(2, "each picture is analysed at the size it was captured at");
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