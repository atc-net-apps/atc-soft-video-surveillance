namespace Atc.Video.Engine.Recording;

/// <summary>
/// The last packets read from the camera, from a key frame on, with the time each arrived. A
/// recording can then start from a key frame already passed: a few seconds before the motion that
/// started it, and with its first picture's time known when the file is named.
/// </summary>
/// <remarks>
/// Not thread-safe: the player adds and reads under its encoder lock.
/// </remarks>
[SuppressMessage("", "CA1806:calls av_*", Justification = "OK")]
internal sealed unsafe class PacketHistory : IDisposable
{
    // Well under the recorder's write queue (1,000), which takes the history in one go.
    public const int MaxPackets = 600;
    public const long MaxBytes = 24L * 1024 * 1024;

    // Kept beyond the time asked for, so the key frame found for a file's name is still there when
    // the recording starts a moment later.
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(2);

    private readonly List<Entry> entries = [];
    private long bytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketHistory"/> class.
    /// </summary>
    /// <param name="keep">How far back a recording may start (0: from the last key frame).</param>
    public PacketHistory(TimeSpan keep)
        => Keep = keep < TimeSpan.Zero ? TimeSpan.Zero : keep;

    public TimeSpan Keep { get; }

    public int Count => entries.Count;

    public long Bytes => bytes;

    /// <summary>
    /// Keeps a copy of a packet that arrived at <paramref name="receivedUtc"/>. Packets before the
    /// first key frame are not kept: a file cannot start with them.
    /// </summary>
    public void Add(
        AVPacket* packet,
        DateTime receivedUtc)
    {
        var isKey = (packet->flags & PktFlags.Key) != PktFlags.None;
        if (entries.Count == 0 && !isKey)
        {
            return;
        }

        var copy = av_packet_clone(packet);
        if (copy is null)
        {
            return;
        }

        entries.Add(new Entry((nint)copy, receivedUtc, isKey, copy->size));
        bytes += copy->size;
        Trim(receivedUtc);
    }

    /// <summary>
    /// Finds where a recording started now would begin: the latest key frame at least
    /// <paramref name="preRoll"/> before <paramref name="nowUtc"/>, or the oldest one kept.
    /// </summary>
    /// <returns>The time that key frame arrived, or <see langword="null"/> when none is kept.</returns>
    public DateTime? FindStart(
        TimeSpan preRoll,
        DateTime nowUtc)
    {
        var index = FindStartIndex(nowUtc - preRoll);
        return index < 0 ? null : entries[index].ReceivedUtc;
    }

    /// <summary>
    /// Hands every packet from the key frame that arrived at <paramref name="fromUtc"/> on (or the
    /// first key frame after it, when that one is gone) to <paramref name="write"/>, oldest first.
    /// </summary>
    /// <returns>The time of the first packet handed on, or <see langword="null"/> when none was.</returns>
    public DateTime? WriteFrom(
        DateTime fromUtc,
        Action<nint> write)
    {
        ArgumentNullException.ThrowIfNull(write);

        var start = entries.FindIndex(e => e.IsKey && e.ReceivedUtc >= fromUtc);
        if (start < 0)
        {
            return null;
        }

        for (var i = start; i < entries.Count; i++)
        {
            write(entries[i].Packet);
        }

        return entries[start].ReceivedUtc;
    }

    public void Clear()
    {
        foreach (var entry in entries)
        {
            Free(entry.Packet);
        }

        entries.Clear();
        bytes = 0;
    }

    public void Dispose()
        => Clear();

    private static void Free(nint ptr)
    {
        var packet = (AVPacket*)ptr;
        av_packet_free(ref packet);
    }

    // The latest key frame that arrived at or before the time, or the oldest key frame.
    private int FindStartIndex(DateTime latestUtc)
    {
        var found = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (!entries[i].IsKey)
            {
                continue;
            }

            if (found < 0 || entries[i].ReceivedUtc <= latestUtc)
            {
                found = i;
            }

            if (entries[i].ReceivedUtc > latestUtc)
            {
                break;
            }
        }

        return found;
    }

    // Drops what no recording can start from any more: everything before the latest key frame that
    // is older than what is kept. Over the limits, whole groups are dropped from the front; a group
    // that alone is over them is dropped too, and the history starts again at the next key frame.
    private void Trim(DateTime nowUtc)
    {
        var keepFrom = FindStartIndex(nowUtc - Keep - Margin);
        RemoveFront(keepFrom);

        while (entries.Count > MaxPackets || bytes > MaxBytes)
        {
            var next = entries.FindIndex(1, e => e.IsKey);
            RemoveFront(next < 0 ? entries.Count : next);
        }
    }

    private void RemoveFront(int count)
    {
        if (count <= 0)
        {
            return;
        }

        for (var i = 0; i < count; i++)
        {
            bytes -= entries[i].Size;
            Free(entries[i].Packet);
        }

        entries.RemoveRange(0, count);
    }

    private readonly record struct Entry(
        nint Packet,
        DateTime ReceivedUtc,
        bool IsKey,
        int Size);
}