namespace AtcSoft.Video.Surveillance.Api.Domain.Streaming;

/// <summary>
/// What the server runs for each camera (its pipeline, recording, motion detection and
/// reconnect schedule), told when a camera is deleted or changed so it does not go on
/// with a camera that is gone, or with the camera as it was.
/// </summary>
public interface ICameraRuntime
{
    /// <summary>
    /// A camera was deleted: its recording is stopped (the file is kept), and its pipeline,
    /// motion detection and reconnect schedule are let go.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    void Forget(Guid cameraId);

    /// <summary>
    /// How a camera is reached or recorded changed (address, sign-in, stream options, codec):
    /// its recording is stopped and its pipeline closed, and both are opened again with the new
    /// settings at the next check, as the server's recording policy and a recording someone
    /// started ask for.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    void Reopen(Guid cameraId);

    /// <summary>
    /// Gets the pipeline the server keeps open for a camera (to watch it for motion), when it
    /// plays: a recording someone starts is made on it rather than on a second connection to
    /// the camera, which many cameras allow only a few of.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <returns>The pipeline; <c>null</c> when the server keeps none open that plays.</returns>
    IMediaPipeline? FindPlayingPipeline(Guid cameraId);
}