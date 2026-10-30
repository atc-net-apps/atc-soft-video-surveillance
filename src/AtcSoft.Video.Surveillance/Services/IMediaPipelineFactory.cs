namespace AtcSoft.Video.Surveillance.Services;

/// <summary>
/// Factory for creating <see cref="IMediaPipeline"/> instances.
/// </summary>
public interface IMediaPipelineFactory
{
    /// <summary>
    /// Creates a new media pipeline for the specified camera.
    /// </summary>
    IMediaPipeline Create(CameraConfiguration camera);
}