namespace AtcSoft.Video.Surveillance.Wpf.Core.Helpers;

/// <summary>
/// A recording's length is read from the file once, and again only when the file changed.
/// </summary>
public sealed class RecordingDurationCacheTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "duration-cache-tests-" + Guid.NewGuid().ToString("N"));
    private int probes;

    public RecordingDurationCacheTests()
        => Directory.CreateDirectory(folder);

    public void Dispose()
        => TestFolders.Delete(folder);

    [Fact]
    public void A_Length_Is_Read_Once()
    {
        var file = Write("a.mkv", 10);
        var sut = new RecordingDurationCache(Probe);

        sut.TryGet(file, out _).Should().BeFalse("nothing is read yet");
        sut.Get(file).Should().Be(TimeSpan.FromSeconds(10));
        sut.Get(file).Should().Be(TimeSpan.FromSeconds(10));
        sut.TryGet(file, out var known).Should().BeTrue();

        known.Should().Be(TimeSpan.FromSeconds(10));
        probes.Should().Be(1);
    }

    [Fact]
    public void A_File_That_Changed_Is_Read_Again()
    {
        var file = Write("a.mkv", 10);
        var sut = new RecordingDurationCache(Probe);
        sut.Get(file);

        Write("a.mkv", 25);

        sut.TryGet(file, out _).Should().BeFalse("the file is not the one that was read");
        sut.Get(file).Should().Be(TimeSpan.FromSeconds(25));
        probes.Should().Be(2);
    }

    [Fact]
    public void A_File_That_Is_Gone_Has_No_Length()
    {
        var file = Write("a.mkv", 10);
        var sut = new RecordingDurationCache(Probe);
        sut.Get(file);

        File.Delete(file);

        sut.TryGet(file, out _).Should().BeFalse();
    }

    // The fake recording's length in seconds is its size in bytes.
    private TimeSpan Probe(string path)
    {
        probes++;
        return TimeSpan.FromSeconds(new FileInfo(path).Length);
    }

    private string Write(
        string name,
        int bytes)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }
}