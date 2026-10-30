namespace AtcSoft.VideoEngine.Windows;

/// <summary>
/// Starts the engine for tests that need the FFmpeg shared libraries: skips the test when FFmpeg
/// is not installed, fails it when it is installed but does not load (as the engine's own tests).
/// </summary>
internal static class TestEngine
{
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