namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using ApplicationSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.ApplicationSettingsService;

public sealed class SettingsDialogTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-settings-dialog-" + Guid.NewGuid().ToString("N"));

    public SettingsDialogTests()
    {
        System.IO.Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(directory))
        {
            TestFolders.Delete(directory);
        }
    }

    [Fact]
    public void The_Dialog_Can_Be_Created()
    {
        // Loading the XAML resolves every control, resource and property in
        // it, so a control or property that does not exist fails here
        // instead of when a user opens Settings.
        var settingsService = new ApplicationSettingsService(System.IO.Path.Combine(directory, "settings.json"));

        var error = WpfTestThread.Run(() =>
        {
            var dialog = new SettingsDialog(new SettingsDialogViewModel(settingsService));
            dialog.Close();
        });

        error.Should().BeNull();
    }
}