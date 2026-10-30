namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Keeps the one pipeline of a camera that can only be opened once. Windows
/// gives a USB camera to one program at a time: a second opener connects
/// and then gets no pictures. So whoever wants to watch or record such a
/// camera asks for the pipeline here, and gives it back when done, instead
/// of opening the camera.
/// </summary>
public interface ICameraPipelineHost
{
    /// <summary>
    /// Gets whether every use of a camera has to share one pipeline.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <returns><see langword="true"/> for a USB camera.</returns>
    bool Hosts(CameraConfiguration camera);

    /// <summary>
    /// Gets the camera's pipeline for a use, opening the camera when nobody
    /// has it open yet. The pipeline may still be connecting.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="use">What the pipeline is needed for.</param>
    /// <returns>
    /// The pipeline, which stays the host's: it is never closed by the one
    /// who asked. <see langword="null"/> when the camera cannot be opened
    /// right now (it is unplugged, or the host is reconnecting to it).
    /// </returns>
    IMediaPipeline? Acquire(
        CameraConfiguration camera,
        CameraPipelineUse use);

    /// <summary>
    /// Gives a use of the camera's pipeline back. The camera is closed when
    /// nothing needs it any more.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <param name="use">The use that ended.</param>
    void Release(
        Guid cameraId,
        CameraPipelineUse use);
}