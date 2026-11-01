namespace AtcSoft.Video.Surveillance.Benchmarks;

/// <summary>
/// The index of a recording in progress grows a line a second, so a timeline reads it again
/// each time; an hour of it is 3,600 lines.
/// </summary>
[MemoryDiagnoser]
public class ActivityLogReaderBenchmarks
{
    private Archive archive = null!;
    private string index = null!;

    [GlobalSetup]
    public void Setup()
    {
        archive = Archive.Create(cameraCount: 1, videoCount: 1, secondsPerIndex: 3_600);
        index = RecordingPathHelper.BuildActivityPath(archive.Videos[0]);
    }

    [GlobalCleanup]
    public void Cleanup()
        => archive.Dispose();

    [Benchmark]
    public int ReadAnHour()
        => ActivityLogReader.Read(index)?.Seconds.Count ?? 0;
}