using CameraLayoutItem = AtcSoft.VideoSurveillance.Models.CameraLayoutItem;

namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// One CameraWall run in its own data folder (ATCSOFT_DATA_PATH): en-US, a log file, no
/// recording, and a start-up layout with an offline camera and, with FFmpeg, a live one.
/// </summary>
internal sealed class CameraWallRun : IDisposable
{
    public const string MainWindowTitle = "AtcSoft Camera Wall";
    public const string LayoutName = "E2E";

    /// <summary>
    /// The cameras' password; it must never be in cameras.json in plain text.
    /// </summary>
    public const string CameraPassword = "e2e-camera-secret";

    private readonly LiveTestCamera? liveCamera;

    /// <param name="environment">More environment variables for the app, such as a limited FFmpeg search.</param>
    public CameraWallRun(
        IReadOnlyDictionary<string, string>? environment = null)
    {
        DataFolder = E2ESettings.NewFolder("camera-wall");
        LogFolder = Path.Combine(DataFolder, "logs");

        liveCamera = TestCameras.StartLiveCamera(
            E2ESettings.GetFreePort(),
            Path.Combine(E2ESettings.RunFolder, "camera-wall-test-camera.log"));
        HasLiveCamera = liveCamera is not null;
        Seed(liveCamera?.Port);

        var process = ChildProcess.Start(
            E2ESettings.CameraWallExe,
            [],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ATCSOFT_DATA_PATH"] = DataFolder,
            }.With(environment),
            Path.Combine(E2ESettings.RunFolder, "camera-wall-output.log"));
        App = new DesktopApp(process, Path.Combine(E2ESettings.RunFolder, "camera-wall-ui"));
    }

    public string DataFolder { get; }

    public string LogFolder { get; }

    public bool HasLiveCamera { get; }

    public DesktopApp App { get; }

    public void Dispose()
    {
        App.Dispose();
        App.Process.Dispose();
        liveCamera?.Dispose();
    }

    private void Seed(int? livePort)
    {
        var settings = new ApplicationSettingsService(Path.Combine(DataFolder, "settings.json"));
        settings.General.Language = "1033";
        settings.General.ConnectCamerasOnStartup = true;
        settings.Recording.RecordingPath = Path.Combine(DataFolder, "recordings");
        settings.Recording.EnableRecordingOnConnect = false;
        settings.Recording.EnableRecordingOnMotion = false;
        settings.CameraDisplay.SnapshotPath = Path.Combine(DataFolder, "snapshots");
        settings.Advanced.EnableDebugLogging = true;
        settings.Advanced.LogPath = LogFolder;
        settings.Save();

        var storage = new CameraStorageService(Path.Combine(DataFolder, "cameras.json"));
        var layout = new CameraLayout { Name = LayoutName };
        var order = 0;

        var layoutOptions = RecordingLayoutOptions.From(settings.Recording);
        foreach (var (name, port) in Cameras(livePort))
        {
            var camera = new CameraConfiguration();
            camera.Connection.IpAddress = "127.0.0.1";
            camera.Connection.Port = port;
            camera.Connection.Protocol = CameraProtocol.Http;
            camera.Connection.Path = TestCameras.StreamPath;
            camera.Display.DisplayName = name;
            camera.Authentication.UserName = "e2e";
            camera.Authentication.Password = CameraPassword;
            storage.AddOrUpdateCamera(camera);

            // A recording per camera, so the recordings browser lists rows with thumbnails.
            var video = RecordingPathHelper.BuildRecordingPath(
                settings.Recording.RecordingPath,
                RecordingPathHelper.GetCameraFolderName(name, camera.Id),
                DateTime.Now.AddMinutes(-5),
                "mp4",
                layoutOptions);
            if (TestCameras.WriteRecording(video, RecordingPathHelper.BuildThumbnailPath(video, settings.Recording.RecordingPath, layoutOptions)))
            {
                // And its activity, so the player shows a timeline with an event.
                TestCameras.WriteActivity(video, DateTimeOffset.Now.AddMinutes(-5));
            }

            // An older recording without an index, for "Analyze recording".
            if (name == TestCameras.DeadCameraName)
            {
                var older = RecordingPathHelper.BuildRecordingPath(
                    settings.Recording.RecordingPath,
                    RecordingPathHelper.GetCameraFolderName(name, camera.Id),
                    DateTime.Now.AddDays(-1),
                    "mp4",
                    layoutOptions);
                TestCameras.WriteRecording(older, RecordingPathHelper.BuildThumbnailPath(older, settings.Recording.RecordingPath, layoutOptions));
            }

            layout.Items.Add(new CameraLayoutItem { CameraId = camera.Id, OrderNumber = order++ });
        }

        storage.AddOrUpdateLayout(layout);
        storage.StartupLayoutId = layout.Id;
        storage.Save();
    }

    private static IEnumerable<(string Name, int Port)> Cameras(int? livePort)
    {
        if (livePort is { } port)
        {
            yield return (TestCameras.LiveCameraName, port);
        }

        yield return (TestCameras.DeadCameraName, TestCameras.DeadPort);
    }
}