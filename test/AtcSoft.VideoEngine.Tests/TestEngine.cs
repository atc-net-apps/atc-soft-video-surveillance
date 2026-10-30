namespace AtcSoft.VideoEngine;

/// <summary>
/// Starts the engine for tests that need the FFmpeg shared libraries.
/// </summary>
internal static class TestEngine
{
    /// <summary>
    /// Skips the test when FFmpeg is not installed on this machine, and
    /// fails it when FFmpeg is installed but does not load. The second case
    /// is what a bindings or FFmpeg version mismatch looks like; skipping it
    /// would leave the whole engine untested behind a green run.
    /// </summary>
    public static void Require()
    {
        var installedAt = VideoEngineBootstrap.FindFFmpegPath();
        if (installedAt is null)
        {
            Assert.SkipUnless(TryInitialize(), "FFmpeg shared libraries are not installed.");
            return;
        }

        try
        {
            VideoEngineBootstrap.Initialize();
        }
        catch (Exception ex)
        {
            Assert.Fail($"FFmpeg is installed at '{installedAt}' but did not load: {ex.Message}");
        }

        TestContext.Current.TestOutputHelper?.WriteLine($"FFmpeg {VideoEngineBootstrap.FFmpegVersion} from {installedAt}");
    }

    private static bool TryInitialize()
    {
        try
        {
            VideoEngineBootstrap.Initialize();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}