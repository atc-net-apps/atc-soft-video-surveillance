namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

using System.IO;

/// <summary>
/// The UI changes the cameras while the recording and segmentation workers read them on other
/// threads: a read never fails because of a change, and what it got is not changed under it.
/// </summary>
public sealed class CameraStorageServiceConcurrencyTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "camera-storage-concurrency-" + Guid.NewGuid().ToString("N"));

    public CameraStorageServiceConcurrencyTests()
        => Directory.CreateDirectory(folder);

    public void Dispose()
        => TestFolders.Delete(folder);

    [Fact]
    public async Task Cameras_Can_Be_Read_On_Other_Threads_While_They_Change()
    {
        var storage = new CameraStorageService(Path.Combine(folder, "cameras.json"));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = stop.Token;

        var writer = Task.Run(
            () =>
            {
                var added = new List<Guid>();
                while (!token.IsCancellationRequested)
                {
                    var camera = new AtcSoft.Video.Surveillance.Wpf.Core.Models.CameraConfiguration();
                    camera.Display.DisplayName = "Camera " + added.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    storage.AddOrUpdateCamera(camera);
                    added.Add(camera.Id);
                    if (added.Count > 5)
                    {
                        storage.DeleteCamera(added[0]);
                        added.RemoveAt(0);
                    }
                }
            },
            CancellationToken.None);

        var readers = Enumerable.Range(0, 3).Select(_ => Task.Run(
            () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var cameras = storage.GetAllCameras();
                    var names = cameras.Select(c => c.Display.DisplayName).ToList();
                    names.Should().HaveCount(cameras.Count, "the list a reader got does not change under it");
                    _ = storage.GetAllLayouts().Count;
                }
            },
            CancellationToken.None));

        var all = new List<Task>(readers) { writer };
        var act = () => Task.WhenAll(all);

        await act.Should().NotThrowAsync();
    }
}