namespace AtcSoft.CameraWall.Wpf.Services.Internal;

/// <summary>
/// Context for an active timelapse capture session.
/// </summary>
internal sealed class TimelapseCaptureContext : IDisposable
{
    public TimelapseCaptureContext(
        CameraConfiguration camera,
        IMediaPipeline pipeline,
        TimeSpan interval)
    {
        Camera = camera;
        Pipeline = pipeline;
        Interval = interval;
        Timer = new DispatcherTimer
        {
            Interval = interval,
        };
    }

    public CameraConfiguration Camera { get; }

    public IMediaPipeline Pipeline { get; }

    public TimeSpan Interval { get; }

    public DispatcherTimer Timer { get; }

    public void Dispose()
    {
        Timer.Stop();
    }
}