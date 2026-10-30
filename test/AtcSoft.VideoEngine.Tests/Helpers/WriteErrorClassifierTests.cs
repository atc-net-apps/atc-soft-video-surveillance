namespace AtcSoft.VideoEngine.Helpers;

/// <summary>
/// A full disk or a broken output ends a recording; a packet FFmpeg refuses for its timestamp
/// does not.
/// </summary>
public sealed class WriteErrorClassifierTests
{
    [Theory]
    [InlineData(-28, 0)] // ENOSPC: no space left
    [InlineData(-5, 0)] // EIO
    [InlineData(-27, 0)] // EFBIG: file too large for the file system
    [InlineData(-32, 0)] // EPIPE: the connection is gone
    [InlineData(-22, -5)] // the I/O context reports an error, whatever the packet's result
    public void A_Write_That_Cannot_Reach_The_File_Ends_The_Recording(
        int writeResult,
        int ioContextError)
        => WriteErrorClassifier.IsFatal(writeResult, ioContextError).Should().BeTrue();

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-22, 0)] // EINVAL: a non-monotonic timestamp
    [InlineData(-1094995529, 0)] // AVERROR_INVALIDDATA
    public void A_Refused_Packet_Does_Not_End_The_Recording(
        int writeResult,
        int ioContextError)
        => WriteErrorClassifier.IsFatal(writeResult, ioContextError).Should().BeFalse();
}