namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// Factory that creates <see cref="VideoEngineMediaPipeline"/> instances for the server edition.
/// Each pipeline wraps an <see cref="IVideoPlayer"/> and is opened immediately upon creation.
/// </summary>
public sealed class VideoEngineMediaPipelineFactory : IMediaPipelineFactory
{
    private readonly IVideoPlayerFactory videoPlayerFactory;
    private readonly IApplicationSettingsService? settingsService;
    private readonly ILogger<VideoEngineMediaPipeline>? pipelineLogger;
    private readonly SubStreamAvailability subStreams;

    public VideoEngineMediaPipelineFactory(
        IVideoPlayerFactory videoPlayerFactory,
        IApplicationSettingsService? settingsService = null,
        ILogger<VideoEngineMediaPipeline>? pipelineLogger = null,
        SubStreamAvailability? subStreams = null)
    {
        this.videoPlayerFactory = videoPlayerFactory ?? throw new ArgumentNullException(nameof(videoPlayerFactory));
        this.settingsService = settingsService;
        this.pipelineLogger = pipelineLogger;
        this.subStreams = subStreams ?? new SubStreamAvailability();
    }

    /// <inheritdoc />
    /// <remarks>
    /// A camera with a sub-stream that has not failed lately gets a <see cref="SubStreamMediaPipeline"/>:
    /// its sub-stream decoded for the pictures, its main stream undecoded for the recordings.
    /// </remarks>
    public IMediaPipeline Create(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        if (!subStreams.IsUsable(camera))
        {
            return CreateOne(camera, CameraStreamRole.Main, decode: true);
        }

        var main = CreateOne(camera, CameraStreamRole.Main, decode: false);
        try
        {
            var sub = CreateOne(camera, CameraStreamRole.Sub, decode: true);
            var cameraId = camera.Id;
            var path = camera.Connection.SubStreamPath;
            return new SubStreamMediaPipeline(
                main,
                sub,
                reason => subStreams.Failed(cameraId, path, reason),
                () => subStreams.Played(cameraId),
                camera.Display.DisplayName,
                pipelineLogger);
        }
        catch
        {
            main.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public IMediaPipeline Create(
        CameraConfiguration camera,
        CameraStreamRole role)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return CreateOne(camera, role, decode: true);
    }

    private VideoEngineMediaPipeline CreateOne(
        CameraConfiguration camera,
        CameraStreamRole role,
        bool decode)
    {
        // The wrapping pipeline owns the player and disposes it transitively.
        // Pre-pipeline construction failures (or pipeline.Open throwing) used
        // to leak the half-constructed player/pipeline because the caller's
        // `using var` never received the reference.
        var name = role == CameraStreamRole.Sub && camera.Connection.HasSubStream
            ? camera.Display.DisplayName + " (sub-stream)"
            : camera.Display.DisplayName;
        var player = videoPlayerFactory.Create();
        VideoEngineMediaPipeline pipeline;
        try
        {
            pipeline = new VideoEngineMediaPipeline(player, name, pipelineLogger);
        }
        catch
        {
            (player as IDisposable)?.Dispose();
            throw;
        }

        try
        {
            // Recorded as H.264 or as it is sent: the application's choice,
            // or the camera's own.
            pipeline.TranscodeRecordingToH264 = RecordingPolicyHelper.ResolveTranscodeCodec(
                camera,
                settingsService?.Recording.TranscodeVideoCodec ?? VideoTranscodeCodec.None) == VideoTranscodeCodec.H264;
            pipeline.DecodeVideo = decode;

            // BuildSourceLocator handles network and USB cameras
            // uniformly. For network cameras it's equivalent to the
            // old camera.BuildUri() path; for USB it injects the
            // dshow input format + device spec + capture-format
            // options the demuxer needs.
            pipeline.Open(CameraUriHelper.BuildSourceLocator(camera, role), camera.Stream);

            // Apply the camera's configured rotation so live snapshots and
            // recordings come out the right way up. Without this, server-side
            // recordings ignore camera.Display.Rotation entirely.
            pipeline.SetRotation(camera.Display.Rotation);

            return pipeline;
        }
        catch
        {
            pipeline.Dispose();
            throw;
        }
    }
}