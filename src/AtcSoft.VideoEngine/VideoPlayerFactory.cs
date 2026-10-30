namespace AtcSoft.VideoEngine;

/// <summary>
/// Factory for creating <see cref="VideoPlayer"/> instances with dependency-injected logging
/// and optional GPU acceleration.
/// </summary>
public sealed class VideoPlayerFactory : IVideoPlayerFactory
{
    private readonly ILoggerFactory loggerFactory;
    private readonly IGpuAcceleratorFactory? gpuFactory;

    public VideoPlayerFactory(ILoggerFactory loggerFactory)
        : this(loggerFactory, gpuFactory: null)
    {
    }

    public VideoPlayerFactory(
        ILoggerFactory loggerFactory,
        IGpuAcceleratorFactory? gpuFactory)
    {
        this.loggerFactory = loggerFactory;
        this.gpuFactory = gpuFactory;
    }

    public IVideoPlayer Create()
    {
        var playerLogger = loggerFactory.CreateLogger<VideoPlayer>();
        IGpuAccelerator? gpuAccelerator = null;

        if (gpuFactory is not null)
        {
            gpuAccelerator = gpuFactory.TryCreate(
                loggerFactory.CreateLogger("GpuAccelerator"));
        }

        return new VideoPlayer(playerLogger, gpuAccelerator);
    }
}