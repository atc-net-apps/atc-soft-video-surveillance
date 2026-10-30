namespace AtcSoft.CameraWall.Wpf;

/// <summary>
/// Engine initialization for the CameraWall library.
/// Must be called before using any CameraWall controls.
/// </summary>
public static class CameraWallEngine
{
    /// <summary>
    /// Gets a value indicating whether the engine has been initialized.
    /// </summary>
    public static bool IsInitialized => VideoEngineBootstrap.IsInitialized;

    /// <summary>
    /// Initializes the CameraWall engine with default settings.
    /// </summary>
    public static void Initialize()
    {
        Initialize(ffmpegPath: null);
    }

    /// <summary>
    /// Initializes the CameraWall engine with a custom FFmpeg path.
    /// </summary>
    /// <param name="ffmpegPath">The path to FFmpeg binaries. If null, auto-discovery is used.</param>
    public static void Initialize(string? ffmpegPath)
    {
        VideoEngineBootstrap.Initialize(new VideoEngineConfig { FFmpegPath = ffmpegPath });
    }
}