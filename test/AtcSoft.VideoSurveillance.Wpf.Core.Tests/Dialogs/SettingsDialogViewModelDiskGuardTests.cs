namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using ApplicationSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.ApplicationSettingsService;

/// <summary>
/// The disk-space guard runs whatever the cleanup schedule is, so its settings stay editable
/// when the scheduled cleanup is off.
/// </summary>
public sealed class SettingsDialogViewModelDiskGuardTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-settings-guard-" + Guid.NewGuid().ToString("N"));
    private readonly ApplicationSettingsService settingsService;

    public SettingsDialogViewModelDiskGuardTests()
    {
        System.IO.Directory.CreateDirectory(directory);
        settingsService = new ApplicationSettingsService(System.IO.Path.Combine(directory, "settings.json"));
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(directory))
        {
            TestFolders.Delete(directory);
        }
    }

    [Fact]
    public void The_Free_Space_Kept_Can_Be_Set_With_The_Scheduled_Cleanup_Off()
    {
        var sut = new SettingsDialogViewModel(settingsService)
        {
            SelectedCleanupSchedule = "Disabled",
            IsDiskSpaceGuardEnabled = true,
        };

        sut.IsDiskSpaceGuardControlEnabled.Should().BeTrue();
    }

    [Fact]
    public void The_Free_Space_Kept_Cannot_Be_Set_With_The_Guard_Off()
    {
        var sut = new SettingsDialogViewModel(settingsService)
        {
            IsDiskSpaceGuardEnabled = false,
        };

        sut.IsDiskSpaceGuardControlEnabled.Should().BeFalse();
    }
}