// ReSharper disable StringLiteralTypo
namespace AtcSoft.VideoEngine;

/// <summary>
/// Static entry point for initializing the video engine.
/// Must be called once before creating any <see cref="IVideoPlayer"/> instances.
/// </summary>
public static class VideoEngineBootstrap
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static bool isInitialized;
    private static VideoEngineConfig? currentConfig;

    /// <summary>
    /// Gets a value indicating whether the engine has been initialized.
    /// </summary>
    public static bool IsInitialized => isInitialized;

    /// <summary>
    /// Gets the FFmpeg version string after initialization, or <c>null</c> if not yet initialized.
    /// </summary>
    public static string? FFmpegVersion { get; private set; }

    /// <summary>
    /// Gets the folder the FFmpeg libraries were loaded from, or <c>null</c> if not yet initialized.
    /// The FFmpeg command-line tool of the same version is looked for there.
    /// </summary>
    public static string? FFmpegFolder { get; private set; }

    /// <summary>
    /// Gets the current engine configuration, or <c>null</c> if not yet initialized.
    /// </summary>
    public static VideoEngineConfig? Config => currentConfig;

    /// <summary>
    /// Initializes the video engine with default configuration.
    /// </summary>
    public static void Initialize()
    {
        Initialize(new VideoEngineConfig());
    }

    /// <summary>
    /// Initializes the video engine with the specified configuration.
    /// This method is idempotent; subsequent calls are ignored.
    /// </summary>
    /// <param name="config">The engine configuration.</param>
    public static void Initialize(VideoEngineConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var acquired = false;
        try
        {
            InitLock.Wait();
            acquired = true;

            if (isInitialized)
            {
                return;
            }

            // The bindings load exactly one FFmpeg version; say which is needed and what was
            // found instead of letting the loader fail with "avutil.61 not found".
            var check = FFmpegRuntime.Check(config.FFmpegPath);
            if (check.Status != FFmpegStatus.Ready)
            {
                throw new FFmpegNotReadyException(check);
            }

            FFmpegVersion = FFmpegLoader.Initialize(check.Folder, config.FFmpegLogLevel);
            FFmpegFolder = check.Folder;

            currentConfig = config;
            isInitialized = true;
        }
        finally
        {
            if (acquired)
            {
                InitLock.Release();
            }
        }
    }

    /// <summary>
    /// Finds the folder with the FFmpeg the engine needs (<see cref="FFmpegRuntime.Check(string?)"/>);
    /// without one, a folder with another FFmpeg, so that loading it fails with the loader's
    /// message (the engine tests rely on this to tell "not installed" from "wrong version").
    /// </summary>
    /// <returns>The folder, or <c>null</c> when no FFmpeg is installed at all.</returns>
    public static string? FindFFmpegPath()
        => FindFFmpegPath(FFmpegRuntime.Candidates());

    /// <summary>
    /// Returns the folder with the FFmpeg the engine needs among the candidates, else the
    /// first folder with any FFmpeg.
    /// </summary>
    internal static string? FindFFmpegPath(IEnumerable<string> candidates)
    {
        var check = FFmpegRuntime.Check(candidates);
        return check.Folder ?? (check.Found.Count > 0 ? check.Found[0].Folder : null);
    }
}