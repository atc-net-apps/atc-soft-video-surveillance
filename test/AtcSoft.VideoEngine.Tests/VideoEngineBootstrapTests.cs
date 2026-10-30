namespace AtcSoft.VideoEngine;

public sealed class VideoEngineBootstrapTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-bootstrap-" + Guid.NewGuid().ToString("N"));

    public VideoEngineBootstrapTests()
    {
        Directory.CreateDirectory(directory);
    }

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
    public void FindFFmpegPath_Returns_The_First_Folder_That_Holds_The_Libraries()
    {
        var empty = Directory.CreateDirectory(Path.Combine(directory, "empty")).FullName;
        var installed = Directory.CreateDirectory(Path.Combine(directory, "installed")).FullName;
        File.WriteAllText(Path.Combine(installed, "avcodec-62.dll"), string.Empty);

        var result = VideoEngineBootstrap.FindFFmpegPath([Path.Combine(directory, "missing"), empty, installed]);

        result.Should().Be(installed);
    }

    [Fact]
    public void FindFFmpegPath_Returns_Null_When_No_Folder_Holds_The_Libraries()
    {
        var empty = Directory.CreateDirectory(Path.Combine(directory, "empty")).FullName;

        var result = VideoEngineBootstrap.FindFFmpegPath([Path.Combine(directory, "missing"), empty]);

        result.Should().BeNull();
    }
}