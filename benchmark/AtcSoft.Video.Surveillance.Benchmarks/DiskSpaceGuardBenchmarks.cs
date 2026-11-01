namespace AtcSoft.Video.Surveillance.Benchmarks;

/// <summary>
/// A full disk is an NVR's usual state: every recording that starts asks the guard first, and
/// the guard deletes the oldest recording to make room. Each call here frees one byte, so one
/// recording goes per call, as on a server whose disk is just short.
/// </summary>
[MemoryDiagnoser]
public class DiskSpaceGuardBenchmarks
{
    private Archive archive = null!;
    private DiskSpaceGuard guard = null!;
    private DiskSpaceGuardRequest request = null!;

    [Params(1_000, 10_000)]
    public int Videos { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Room for every call of a run: the archive loses one recording per call.
        archive = Archive.Create(cameraCount: 16, Videos * 2);
        guard = new DiskSpaceGuard(new OneByteShort());
        request = new DiskSpaceGuardRequest(
            [archive.Root],
            SnapshotFolder: null,
            MinFreeBytes: 1_000_000,
            ProtectedPaths: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            RecordingExtensions: [".mp4", ".mkv", ".avi"],
            SnapshotExtensions: [".png", ".jpg"]);
    }

    [GlobalCleanup]
    public void Cleanup()
        => archive.Dispose();

    [Benchmark]
    public int RecordingStartsOnAFullDisk()
        => guard.Enforce(request).Volumes.Count;

    private sealed class OneByteShort : IFreeSpaceReader
    {
        public string? GetVolume(string path)
            => "bench";

        public long? GetFreeBytes(string volume)
            => 1_000_000 - 1;
    }
}