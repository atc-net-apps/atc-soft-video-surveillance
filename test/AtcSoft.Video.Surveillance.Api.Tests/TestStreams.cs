namespace AtcSoft.Video.Surveillance.Api;

/// <summary>
/// The live test streams the FFmpeg tool serves (<c>-listen 1</c>) take a moment to listen.
/// A server that connects before they do is refused at once on Linux and gives up; on Windows a
/// refused connection is tried again for a second or two, which hid it.
/// </summary>
internal static class TestStreams
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Waits until the test stream listens on <paramref name="port"/>, then hands it back.
    /// </summary>
    public static System.Diagnostics.Process Listening(
        System.Diagnostics.Process process,
        int port)
    {
        ArgumentNullException.ThrowIfNull(process);

        WaitUntilListening(port);
        return process;
    }

    /// <summary>
    /// Waits until something listens on <paramref name="port"/> of this machine.
    /// </summary>
    public static void WaitUntilListening(int port)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.Elapsed < StartTimeout)
        {
            var listeners = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            if (listeners.Any(l => l.Port == port))
            {
                return;
            }

            Thread.Sleep(50);
        }

        throw new TimeoutException($"The test stream did not listen on port {port} within {StartTimeout.TotalSeconds:N0} s.");
    }
}