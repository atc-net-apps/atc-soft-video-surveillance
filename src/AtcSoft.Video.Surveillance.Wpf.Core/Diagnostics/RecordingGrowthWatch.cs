namespace AtcSoft.Video.Surveillance.Wpf.Core.Diagnostics;

/// <summary>
/// Watches the files of the running recordings grow. A recording whose file has not grown for
/// <c>stallAfter</c> is stalled: it still counts as recording, but nothing reaches the disk, which is
/// what a long test run must catch. A new file (the next segment, a restart) starts over.
/// </summary>
public sealed class RecordingGrowthWatch
{
    private readonly TimeSpan stallAfter;
    private readonly Dictionary<Guid, Seen> seen = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingGrowthWatch"/> class.
    /// </summary>
    /// <param name="stallAfter">How long a file may stay the same size before it counts as stalled.</param>
    public RecordingGrowthWatch(TimeSpan stallAfter)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(stallAfter, TimeSpan.Zero);

        this.stallAfter = stallAfter;
    }

    /// <summary>
    /// Looks at the recordings as they are now.
    /// </summary>
    /// <param name="now">The time of the look.</param>
    /// <param name="recordings">Each running recording: its camera, file and the file's length now.</param>
    /// <returns>Each recording with how it changed since the last look.</returns>
    public IReadOnlyList<RecordingGrowth> Observe(
        DateTimeOffset now,
        IEnumerable<RecordingFile> recordings)
    {
        ArgumentNullException.ThrowIfNull(recordings);

        var result = new List<RecordingGrowth>();
        var running = new HashSet<Guid>();
        foreach (var recording in recordings)
        {
            running.Add(recording.CameraId);

            if (!seen.TryGetValue(recording.CameraId, out var before) ||
                !string.Equals(before.FilePath, recording.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                seen[recording.CameraId] = new Seen(recording.FilePath, recording.Length, now, IsStalled: false);
                result.Add(new RecordingGrowth(recording, 0, RecordingGrowthState.New, TimeSpan.Zero));
                continue;
            }

            var growth = recording.Length - before.Length;
            if (growth > 0)
            {
                seen[recording.CameraId] = new Seen(recording.FilePath, recording.Length, now, IsStalled: false);
                result.Add(new RecordingGrowth(
                    recording,
                    growth,
                    before.IsStalled ? RecordingGrowthState.Resumed : RecordingGrowthState.Growing,
                    TimeSpan.Zero));
                continue;
            }

            var stalledFor = now - before.LastGrowth;
            var isStalled = stalledFor >= stallAfter;
            seen[recording.CameraId] = before with { Length = recording.Length, IsStalled = isStalled };
            var state = isStalled
                ? before.IsStalled ? RecordingGrowthState.StillStalled : RecordingGrowthState.Stalled
                : RecordingGrowthState.Waiting;
            result.Add(new RecordingGrowth(recording, growth, state, stalledFor));
        }

        // A recording that ended is forgotten: when the camera records again, it starts over.
        foreach (var gone in seen.Keys.Where(id => !running.Contains(id)).ToList())
        {
            seen.Remove(gone);
        }

        return result;
    }

    private sealed record Seen(
        string FilePath,
        long Length,
        DateTimeOffset LastGrowth,
        bool IsStalled);
}