namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// The truth written next to an encoded scene (<c>*.truth.jsonl</c>) reads back as it was
/// written, so a player of the video knows where every object was in every frame.
/// </summary>
public sealed class TruthFileTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-truth-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a temp folder left behind is harmless.
        }
    }

    [Fact]
    public void The_Truth_Reads_Back_As_It_Was_Written()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "scene.truth.jsonl");
        IReadOnlyList<FrameTruth> written =
        [
            new(0, 0, [], IgnoreAll: false),
            new(
                1,
                1 / 15d,
                [
                    new TruthBox("car", new PixelBox(10.5, 20, 100, 50.25), TruthState.MustBox, IsDistractor: false),
                    new TruthBox("tree", new PixelBox(0, 0, 30, 60), TruthState.MustNotBox, IsDistractor: true),
                ],
                IgnoreAll: true),
        ];

        TruthFile.Write(path, written);
        var read = TruthFile.Read(path);

        read.Should().BeEquivalentTo(written, o => o.WithStrictOrdering());
    }
}