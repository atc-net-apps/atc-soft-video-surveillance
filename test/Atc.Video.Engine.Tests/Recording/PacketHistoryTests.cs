namespace Atc.Video.Engine.Recording;

/// <summary>
/// The player keeps its last packets so a recording can start from a key frame already passed: a
/// motion recording a few seconds before the motion, any recording with its first picture's time
/// known when the file is named.
/// </summary>
public sealed unsafe class PacketHistoryTests
{
    private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_Start_With_A_Pre_Roll_Goes_Back_To_The_Last_Key_Frame_Before_It()
    {
        // A picture every 0.1 s, a key frame every 2 s (0, 2, 4, 6, 8 s), the last at 9.9 s.
        TestEngine.Require();
        using var history = new PacketHistory(TimeSpan.FromSeconds(5));
        Add(history, count: 100, keyEvery: 20);

        history.FindStart(TimeSpan.FromSeconds(3), At(9.9)).Should().Be(At(6), "the last key frame at least 3 s back");
        history.FindStart(TimeSpan.Zero, At(9.9)).Should().Be(At(8), "without a pre-roll, the last key frame");
    }

    [Fact]
    public void A_Start_Further_Back_Than_What_Is_Kept_Is_The_Oldest_Key_Frame()
    {
        TestEngine.Require();
        using var history = new PacketHistory(TimeSpan.FromSeconds(2));
        Add(history, count: 100, keyEvery: 20);

        var start = history.FindStart(TimeSpan.FromSeconds(30), At(9.9));

        start.Should().NotBeNull();
        start!.Value.Should().BeOnOrAfter(At(2), "what is older than the pre-roll (and its margin) is let go");
    }

    [Fact]
    public void Packets_From_The_Start_Are_Handed_On_In_Order_Beginning_With_The_Key_Frame()
    {
        TestEngine.Require();
        using var history = new PacketHistory(TimeSpan.FromSeconds(5));
        Add(history, count: 100, keyEvery: 20);
        var start = history.FindStart(TimeSpan.FromSeconds(3), At(9.9))!.Value;

        var written = new List<(long Pts, bool Key)>();
        var first = history.WriteFrom(start, p =>
        {
            var packet = (AVPacket*)p;
            written.Add((packet->pts, (packet->flags & PktFlags.Key) != PktFlags.None));
        });

        first.Should().Be(At(6));
        written.Should().HaveCount(40, "from 6.0 s to 9.9 s");
        written[0].Should().Be((60L, true));
        written.Select(w => w.Pts).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Nothing_Before_The_First_Key_Frame_Is_Kept()
    {
        TestEngine.Require();
        using var history = new PacketHistory(TimeSpan.FromSeconds(5));
        Add(history, count: 10, keyEvery: 1000, firstIndex: 1);

        history.Count.Should().Be(0);
        history.FindStart(TimeSpan.Zero, At(1)).Should().BeNull();
    }

    [Fact]
    public void A_Long_Group_Of_Pictures_Is_Kept_Within_The_Limits()
    {
        // A "smart" codec that sends a key frame once a minute: the history must not grow with it,
        // and stays well under what the recorder's queue takes in one go.
        TestEngine.Require();
        using var history = new PacketHistory(TimeSpan.FromSeconds(5));
        Add(history, count: 3000, keyEvery: 1500, interval: 0.04);

        history.Count.Should().BeLessThanOrEqualTo(PacketHistory.MaxPackets);
    }

    [Fact]
    public void Large_Packets_Are_Kept_Within_The_Size_Limit()
    {
        TestEngine.Require();
        using var history = new PacketHistory(TimeSpan.FromSeconds(5));
        Add(history, count: 200, keyEvery: 20, size: 512 * 1024);

        history.Bytes.Should().BeLessThanOrEqualTo(PacketHistory.MaxBytes);
        history.Count.Should().BeGreaterThan(0, "whole groups are dropped from the front, not everything");
    }

    [Fact]
    public void Clear_Lets_Go_Of_Every_Packet()
    {
        TestEngine.Require();
        using var history = new PacketHistory(TimeSpan.FromSeconds(5));
        Add(history, count: 50, keyEvery: 10);

        history.Clear();

        history.Count.Should().Be(0);
        history.Bytes.Should().Be(0);
    }

    private static DateTime At(double seconds)
        => T0.AddSeconds(seconds);

    private static void Add(
        PacketHistory history,
        int count,
        int keyEvery,
        double interval = 0.1,
        int size = 64,
        int firstIndex = 0)
    {
        var packet = av_packet_alloc();
        try
        {
            av_new_packet(packet, size).Should().Be(0);
            for (var i = firstIndex; i < firstIndex + count; i++)
            {
                packet->flags = i % keyEvery == 0 ? PktFlags.Key : PktFlags.None;
                packet->pts = i;
                packet->dts = i;
                history.Add(packet, At(Math.Round(i * interval, 3)));
            }
        }
        finally
        {
            av_packet_free(ref packet);
        }
    }
}