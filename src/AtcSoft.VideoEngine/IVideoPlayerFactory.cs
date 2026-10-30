namespace AtcSoft.VideoEngine;

/// <summary>
/// Factory for creating <see cref="IVideoPlayer"/> instances.
/// </summary>
public interface IVideoPlayerFactory
{
    /// <summary>
    /// Creates a new video player instance.
    /// </summary>
    /// <returns>A new <see cref="IVideoPlayer"/> ready to open a stream.</returns>
    IVideoPlayer Create();
}