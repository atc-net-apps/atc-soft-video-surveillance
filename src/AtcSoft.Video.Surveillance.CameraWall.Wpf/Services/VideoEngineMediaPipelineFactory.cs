namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

using CoreCameraConfiguration = AtcSoft.Video.Surveillance.Models.CameraConfiguration;
using IMediaPipelineFactory = AtcSoft.Video.Surveillance.Services.IMediaPipelineFactory;

/// <summary>
/// Factory that creates <see cref="VideoEngineMediaPipeline"/> instances
/// using <see cref="IVideoPlayerFactory"/>.
/// </summary>
[Registration(Lifetime.Singleton)]
public class VideoEngineMediaPipelineFactory : IMediaPipelineFactory
{
    private readonly IVideoPlayerFactory videoPlayerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoEngineMediaPipelineFactory"/> class.
    /// </summary>
    /// <param name="videoPlayerFactory">The video player factory.</param>
    public VideoEngineMediaPipelineFactory(
        IVideoPlayerFactory videoPlayerFactory)
    {
        this.videoPlayerFactory = videoPlayerFactory ?? throw new ArgumentNullException(nameof(videoPlayerFactory));
    }

    /// <inheritdoc />
    public IMediaPipeline Create(CoreCameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        // Pipeline takes ownership of player on success; on construction
        // failure dispose the player so it doesn't leak.
        var player = videoPlayerFactory.Create();
        try
        {
            return new VideoEngineMediaPipeline(player, camera.Display.DisplayName);
        }
        catch
        {
            (player as IDisposable)?.Dispose();
            throw;
        }
    }
}