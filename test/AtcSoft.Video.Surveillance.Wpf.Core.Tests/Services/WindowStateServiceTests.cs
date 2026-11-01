namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

/// <summary>
/// A main window opens where and as big as it was left, from a file of its app's own.
/// </summary>
public sealed class WindowStateServiceTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "window-state-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
        => TestFolders.Delete(folder);

    [Fact]
    public void A_Window_Opens_Where_And_As_Big_As_It_Was_Left()
    {
        var file = Path.Combine(folder, "window-state.json");
        double width = 0, height = 0, left = 0, top = 0;
        double savedLeft = 0, savedTop = 0;
        var startup = WindowStartupLocation.CenterScreen;

        var error = WpfTestThread.Run(() =>
        {
            // On the screen whatever its size (a build server's is small): a window that does not
            // fit is opened in the middle instead, as it should be.
            savedLeft = SystemParameters.VirtualScreenLeft + 40;
            savedTop = SystemParameters.VirtualScreenTop + 30;
            var closed = new Window { Width = 400, Height = 300, Left = savedLeft, Top = savedTop };
            var saving = new WindowStateService(file);
            saving.CaptureFrom(closed);
            saving.Save();

            var opened = new Window { Width = 1500, Height = 1000 };
            var loading = new WindowStateService(file);
            loading.Load();
            loading.ApplyTo(opened);

            width = opened.Width;
            height = opened.Height;
            left = opened.Left;
            top = opened.Top;
            startup = opened.WindowStartupLocation;
        });

        error.Should().BeNull();
        width.Should().Be(400);
        height.Should().Be(300);
        left.Should().Be(savedLeft);
        top.Should().Be(savedTop);
        startup.Should().Be(WindowStartupLocation.Manual);
    }

    [Fact]
    public void A_Window_Without_A_Saved_State_Opens_In_The_Middle_Of_The_Screen()
    {
        var startup = WindowStartupLocation.Manual;

        var error = WpfTestThread.Run(() =>
        {
            var opened = new Window();
            var loading = new WindowStateService(Path.Combine(folder, "none.json"));
            loading.Load();
            loading.ApplyTo(opened);
            startup = opened.WindowStartupLocation;
        });

        error.Should().BeNull();
        startup.Should().Be(WindowStartupLocation.CenterScreen);
    }
}