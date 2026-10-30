namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed class CachedRecordingDurationProviderTests : IDisposable
{
    private static readonly TimeSpan NoLimit = TimeSpan.FromHours(1);

    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-durations-" + Guid.NewGuid().ToString("N"));
    private readonly string file;
    private int probes;

    public CachedRecordingDurationProviderTests()
    {
        Directory.CreateDirectory(directory);
        file = Touch("a.mp4");
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void A_File_Is_Probed_Once_However_Often_It_Is_Asked_For()
    {
        using var sut = Create(TimeSpan.FromSeconds(30));

        sut.GetDuration(file).Should().Be(TimeSpan.FromSeconds(30));
        sut.GetDuration(file).Should().Be(TimeSpan.FromSeconds(30));

        probes.Should().Be(1);
    }

    [Fact]
    public void A_File_That_Changed_Is_Probed_Again()
    {
        using var sut = Create(TimeSpan.FromSeconds(30));
        sut.GetDuration(file);

        File.WriteAllBytes(file, [1, 2, 3, 4, 5]);
        sut.GetDuration(file);

        probes.Should().Be(2);
    }

    [Fact]
    public void A_Length_That_Cannot_Be_Read_Is_Null_And_Not_Remembered()
    {
        // A file that is still being finished may be readable a moment later.
        using var sut = Create(TimeSpan.Zero);

        sut.GetDuration(file).Should().BeNull();
        sut.GetDuration(file).Should().BeNull();

        probes.Should().Be(2);
    }

    [Fact]
    public void A_File_That_Is_Not_There_Has_No_Length()
    {
        using var sut = Create(TimeSpan.FromSeconds(30));

        sut.GetDuration(Path.Combine(directory, "gone.mp4")).Should().BeNull();

        probes.Should().Be(0);
    }

    [Fact]
    public void A_Probe_That_Throws_Gives_No_Length()
    {
        using var sut = new CachedRecordingDurationProvider(_ => throw new InvalidOperationException("broken file"), NoLimit);

        sut.GetDuration(file).Should().BeNull();
    }

    [Fact]
    public async Task A_Listing_Of_Many_Files_Does_Not_Wait_For_Them_All()
    {
        // Opening a file takes tens of milliseconds, and an archive holds
        // tens of thousands. Only what fits a small budget is read on the
        // spot; the rest is read in the background and is there next time.
        string[] files = [.. Enumerable.Range(0, 40).Select(i => Touch($"many-{i}.mp4"))];
        using var sut = new CachedRecordingDurationProvider(
            _ =>
            {
                Interlocked.Increment(ref probes);
                using var pause = new ManualResetEventSlim();
                pause.Wait(TimeSpan.FromMilliseconds(10));
                return TimeSpan.FromMinutes(15);
            },
            synchronousBudget: TimeSpan.FromMilliseconds(30));

        var started = Stopwatch.StartNew();
        TimeSpan?[] first = [.. files.Select(sut.GetDuration)];
        started.Stop();

        first.Count(d => d is null).Should().BeGreaterThan(20, "most of them did not fit the budget");
        started.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(300), "the listing did not wait for forty probes");

        await WaitUntilAsync(() => sut.PendingCount == 0);

        files.Select(sut.GetDuration).Should().OnlyContain(d => d == TimeSpan.FromMinutes(15));
        Volatile.Read(ref probes).Should().Be(40, "each file was read once, in the foreground or in the background");
    }

    [Fact]
    public async Task A_File_Waiting_To_Be_Read_Is_Not_Queued_Twice()
    {
        using var gate = new ManualResetEventSlim();
        using var sut = new CachedRecordingDurationProvider(
            _ =>
            {
                Interlocked.Increment(ref probes);
                gate.Wait(TimeSpan.FromSeconds(10));
                return TimeSpan.FromMinutes(1);
            },
            synchronousBudget: TimeSpan.Zero);

        sut.GetDuration(file).Should().BeNull();
        sut.GetDuration(file).Should().BeNull();
        sut.GetDuration(file).Should().BeNull();
        gate.Set();

        await WaitUntilAsync(() => sut.PendingCount == 0);

        Volatile.Read(ref probes).Should().Be(1);
        sut.GetDuration(file).Should().Be(TimeSpan.FromMinutes(1));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "the background reading should have finished");
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    private string Touch(string name)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    private CachedRecordingDurationProvider Create(TimeSpan result)
        => new(
            _ =>
            {
                probes++;
                return result;
            },
            NoLimit);
}