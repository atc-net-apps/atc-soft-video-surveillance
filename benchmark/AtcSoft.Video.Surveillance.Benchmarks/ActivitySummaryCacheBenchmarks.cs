namespace AtcSoft.Video.Surveillance.Benchmarks;

/// <summary>
/// The activity summaries a timeline reads: one per recording that touches the span, kept in a
/// cache of 5,000. A week of hourly recordings of 30 cameras is 5,040 of them.
/// </summary>
[MemoryDiagnoser]
public class ActivitySummaryCacheBenchmarks
{
    private const int Capacity = 5_000;

    private Archive archive = null!;
    private string[] indexes = null!;
    private ActivitySummaryCache cache = null!;

    [Params(4_000, 6_000)]
    public int Recordings { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        archive = Archive.Create(cameraCount: 30, Recordings);
        indexes = [.. archive.Videos.Select(RecordingPathHelper.BuildActivityPath)];
        cache = new ActivitySummaryCache(Capacity);
        ReadAll();
    }

    [GlobalCleanup]
    public void Cleanup()
        => archive.Dispose();

    /// <summary>
    /// A warm timeline over every recording, as one built again a few seconds later.
    /// </summary>
    [Benchmark]
    public int WarmTimeline()
        => ReadAll();

    private int ReadAll()
    {
        var found = 0;
        foreach (var index in indexes)
        {
            if (cache.Get(index) is not null)
            {
                found++;
            }
        }

        return found;
    }
}