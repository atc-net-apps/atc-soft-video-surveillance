namespace Atc.Video.Engine.Helpers;

/// <summary>
/// FFmpeg on Windows reports a network failure with the C runtime's error number (a connection
/// that timed out is <c>-138</c>), which its own error text does not name: "Error number -138
/// occurred". This names the common ones, so a log says what happened.
/// </summary>
internal static class FFmpegErrorText
{
    private const string UnnamedPrefix = "Error number";

    /// <summary>
    /// The text for an FFmpeg error code.
    /// </summary>
    /// <param name="errorCode">The FFmpeg error code (negative).</param>
    /// <param name="ffmpegText">FFmpeg's own text for it.</param>
    /// <param name="isWindows">Whether the numbers are the Windows C runtime's.</param>
    /// <returns>FFmpeg's text, or a name for a Windows network error it does not name.</returns>
    internal static string Describe(
        int errorCode,
        string ffmpegText,
        bool isWindows)
    {
        if (!isWindows ||
            !ffmpegText.StartsWith(UnnamedPrefix, StringComparison.Ordinal))
        {
            return ffmpegText;
        }

        // The numbers in the Windows C runtime's errno.h; other systems number them differently.
        return -errorCode switch
        {
            100 => "Address already in use",
            101 => "Address not available",
            106 => "Connection aborted",
            107 => "Connection refused",
            108 => "Connection reset by peer",
            110 => "No route to host",
            116 => "Network is down",
            117 => "Network dropped the connection",
            118 => "Network is unreachable",
            126 => "Not connected",
            138 => "Connection timed out",
            _ => ffmpegText,
        };
    }
}