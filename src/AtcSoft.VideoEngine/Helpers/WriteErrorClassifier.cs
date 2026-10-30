namespace AtcSoft.VideoEngine.Helpers;

/// <summary>
/// Decides whether a failed write of a recording ends it. FFmpeg also refuses a single packet
/// with a bad timestamp, which cameras send now and then; that must not stop a recording. A
/// full disk, a broken I/O context or a closed connection must, or the recording would claim to
/// run while nothing reaches the file.
/// </summary>
public static class WriteErrorClassifier
{
    // AVERROR(e) is -e. The numbers are the C runtime's errno values, the same on Windows,
    // Linux and macOS for these.
    private const int Eio = -5;
    private const int Efbig = -27;
    private const int Enospc = -28;
    private const int Epipe = -32;
    private const int EdquotLinux = -122;

    /// <summary>
    /// Determines whether a write error ends the recording.
    /// </summary>
    /// <param name="writeResult">What <c>av_interleaved_write_frame</c> returned.</param>
    /// <param name="ioContextError">The output's I/O context error (<c>pb-&gt;error</c>); 0 when it has none.</param>
    /// <returns><see langword="true"/> when nothing more can be written to the file.</returns>
    public static bool IsFatal(
        int writeResult,
        int ioContextError)
    {
        if (writeResult >= 0)
        {
            return false;
        }

        return ioContextError < 0 ||
               writeResult is Eio or Efbig or Enospc or Epipe or EdquotLinux;
    }
}