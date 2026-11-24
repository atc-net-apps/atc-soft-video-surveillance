namespace AtcSoft.Video.Surveillance.Services;

/// <summary>
/// Factory for creating <see cref="IMediaPipeline"/> instances.
/// </summary>
public interface IMediaPipelineFactory
{
    /// <summary>
    /// Creates a new media pipeline for the specified camera. A camera with a sub-stream may get a
    /// pipeline that analyses and shows its sub-stream and records its main stream.
    /// </summary>
    IMediaPipeline Create(CameraConfiguration camera);

    /// <summary>
    /// Creates a pipeline of one of the camera's streams only, decoded: a snapshot or a recording of
    /// the main stream of a camera whose sub-stream is what the server watches. A camera without a
    /// sub-stream gets its one stream.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="role">The stream to open.</param>
    /// <returns>The opened pipeline.</returns>
    IMediaPipeline Create(
        CameraConfiguration camera,
        CameraStreamRole role)
        => Create(camera);
}