namespace AtcSoft.Video.Surveillance.E2E.Infrastructure;

/// <summary>
/// Waits real time. The smoke tests drive real apps and cameras: what they wait for (a file that
/// grows, a camera that drops out and comes back) happens in the apps' own time, not the test's.
/// </summary>
internal static class RealTime
{
    public static void Wait(TimeSpan time)
        => Thread.Sleep(time);

    public static void Wait(int milliseconds)
        => Thread.Sleep(milliseconds);
}