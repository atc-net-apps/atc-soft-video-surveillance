namespace AtcSoft.Video.Surveillance.Benchmarks;

/// <summary>
/// The walk behind <c>GET /recordings</c> and <c>GET /timeline</c>: every call lists the archive.
/// </summary>
[MemoryDiagnoser]
public class RecordingCatalogBenchmarks
{
    private Archive archive = null!;

    [Params(1_000, 10_000)]
    public int Videos { get; set; }

    [GlobalSetup]
    public void Setup()
        => archive = Archive.Create(cameraCount: 16, Videos);

    [GlobalCleanup]
    public void Cleanup()
        => archive.Dispose();

    [Benchmark(Baseline = true)]
    public int All()
        => RecordingCatalog.Find(archive.Root, archive.Cameras, [], cameraId: null, DateTimeOffset.UtcNow).Count;

    [Benchmark]
    public int OneCamera()
        => RecordingCatalog.Find(archive.Root, archive.Cameras, [], archive.Cameras[0].Id, DateTimeOffset.UtcNow).Count;
}