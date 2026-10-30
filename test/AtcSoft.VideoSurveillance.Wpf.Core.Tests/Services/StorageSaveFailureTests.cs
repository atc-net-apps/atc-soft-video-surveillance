namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// A save that fails (a full disk, a folder that cannot be written) is in the log, where it
/// was only in the debugger's output before, so a lost change can be traced.
/// </summary>
public sealed class StorageSaveFailureTests : IDisposable
{
    private readonly string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-save-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
        => TestFolders.Delete(folder);

    [Fact]
    public void A_Camera_Save_That_Fails_Is_Logged()
    {
        var logger = new CollectingLogger<CameraStorageService>();
        var storage = new CameraStorageService(UnwritablePath("cameras.json"), logger);

        storage.Save();

        logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains("cameras.json", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Settings_Save_That_Fails_Is_Logged()
    {
        var logger = new CollectingLogger<ApplicationSettingsService>();
        var settings = new ApplicationSettingsService(UnwritablePath("settings.json"), logger);

        settings.Save();

        logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains("settings.json", StringComparison.Ordinal));
    }

    // A path below a file: no folder can be made there, so nothing can be written.
    private string UnwritablePath(string fileName)
    {
        System.IO.Directory.CreateDirectory(folder);
        var blocker = System.IO.Path.Combine(folder, "a-file");
        System.IO.File.WriteAllText(blocker, "x");
        return System.IO.Path.Combine(blocker, fileName);
    }
}