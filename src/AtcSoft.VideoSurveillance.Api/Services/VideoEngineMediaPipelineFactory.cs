namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Factory that creates <see cref="VideoEngineMediaPipeline"/> instances for the server edition.
/// Each pipeline wraps an <see cref="IVideoPlayer"/> and is opened immediately upon creation.
/// </summary>
public sealed class VideoEngineMediaPipelineFactory : IMediaPipelineFactory
{
    private readonly IVideoPlayerFactory videoPlayerFactory;
    private readonly IApplicationSettingsService? settingsService;

    public VideoEngineMediaPipelineFactory(
        IVideoPlayerFactory videoPlayerFactory,
        IApplicationSettingsService? settingsService = null)
    {
        this.videoPlayerFactory = videoPlayerFactory ?? throw new ArgumentNullException(nameof(videoPlayerFactory));
        this.settingsService = settingsService;
    }

    /// <inheritdoc />
    public IMediaPipeline Create(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        // The wrapping pipeline owns the player and disposes it transitively.
        // Pre-pipeline construction failures (or pipeline.Open throwing) used
        // to leak the half-constructed player/pipeline because the caller's
        // `using var` never received the reference.
        var player = videoPlayerFactory.Create();
        VideoEngineMediaPipeline? pipeline = null;
        try
        {
            pipeline = new VideoEngineMediaPipeline(player, camera.Display.DisplayName);

            // Recorded as H.264 or as it is sent: the application's choice,
            // or the camera's own.
            pipeline.TranscodeRecordingToH264 = RecordingPolicyHelper.ResolveTranscodeCodec(
                camera,
                settingsService?.Recording.TranscodeVideoCodec ?? VideoTranscodeCodec.None) == VideoTranscodeCodec.H264;

            // BuildSourceLocator handles network and USB cameras
            // uniformly. For network cameras it's equivalent to the
            // old camera.BuildUri() path; for USB it injects the
            // dshow input format + device spec + capture-format
            // options the demuxer needs.
            pipeline.Open(CameraUriHelper.BuildSourceLocator(camera), camera.Stream);

            // Apply the camera's configured rotation so live snapshots and
            // recordings come out the right way up. Without this, server-side
            // recordings ignore camera.Display.Rotation entirely.
            pipeline.SetRotation(camera.Display.Rotation);

            return pipeline;
        }
        catch
        {
            if (pipeline is not null)
            {
                pipeline.Dispose();
            }
            else
            {
                (player as IDisposable)?.Dispose();
            }

            throw;
        }
    }
}