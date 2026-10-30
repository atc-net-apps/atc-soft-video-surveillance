namespace AtcSoft.VideoSurveillance.Services;

public class NullUsbCameraWatcherTests
{
    [Fact]
    public void Start_And_Stop_Are_NoOps()
    {
        using var watcher = new NullUsbCameraWatcher();

        var act = () =>
        {
            watcher.Start();
            watcher.Start();
            watcher.Stop();
            watcher.Stop();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Subscribers_Are_Never_Notified()
    {
        using var watcher = new NullUsbCameraWatcher();
        var arrived = 0;
        var removed = 0;

        watcher.DeviceArrived += (_, _) => arrived++;
        watcher.DeviceRemoved += (_, _) => removed++;

        watcher.Start();

        arrived.Should().Be(0);
        removed.Should().Be(0);
    }
}