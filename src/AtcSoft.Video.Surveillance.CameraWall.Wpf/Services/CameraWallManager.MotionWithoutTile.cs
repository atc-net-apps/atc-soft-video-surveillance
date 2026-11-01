namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// A camera that records on motion is watched whether a layout shows it or not. A camera the
/// shown layout leaves out is opened here and parked with its detection running, as a tile parks
/// one it lets go; a tile that shows it later takes the player. Before, a camera that had never had
/// a tile (not in the start-up layout) was not connected at all, and its motion went unrecorded.
/// </summary>
public sealed partial class CameraWallManager
{
    /// <summary>
    /// Opens and parks every camera that records on motion and is neither in the shown layout nor
    /// parked already; lets go of a parked camera that no longer records on motion and is not
    /// recording.
    /// </summary>
    internal void WatchMotionCamerasNoLayoutShows()
    {
        // Once the tiles the layout let go have parked their players (they go when they are
        // unloaded, after the layout is applied): a camera parked by its tile is not opened twice.
        CameraGrid?.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, WatchNow);
    }

    private void WatchNow()
    {
        // Without a layout, every camera is shown.
        if (CameraGrid?.ParkedPlayers is not { } parking || (CurrentLayout ?? SelectedStartupLayout) is not { } layout)
        {
            return;
        }

        var shown = layout.Items.Select(i => i.CameraId).ToHashSet();
        foreach (var camera in storageService.GetAllCameras())
        {
            if (shown.Contains(camera.Id))
            {
                continue;
            }

            var onMotion = camera.Overrides?.Recording.EnableRecordingOnMotion ?? settingsService.Recording.EnableRecordingOnMotion;
            if (!onMotion)
            {
                if (parking.IsParked(camera.Id) && !recordingService.IsRecordingOrResuming(camera.Id))
                {
                    parking.Release(camera.Id);
                }

                continue;
            }

            if (!parking.IsParked(camera.Id))
            {
                WatchWithoutTile(camera, parking);
            }
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The parking owns the player and the pipeline: it closes them when it lets them go.")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "One camera that cannot be opened must not keep the others from being watched.")]
    private void WatchWithoutTile(
        CameraConfiguration camera,
        RecordingPlayerParking parking)
    {
        try
        {
            var options = CameraStreamOptions.For(
                camera,
                camera.Overrides?.Performance.HardwareAcceleration ?? settingsService.Performance.HardwareAcceleration,
                camera.Overrides?.Performance.VideoQuality ?? settingsService.Performance.VideoQuality,
                camera.Overrides?.Recording.TranscodeVideoCodec ?? settingsService.Recording.TranscodeVideoCodec);
            var uri = camera.BuildSourceLocator().Uri;
            var cameraId = camera.Id;
            var motion = CameraMotionSettings.Resolve(camera, settingsService.MotionDetection);
            var zones = camera.MotionZones;

            var player = videoPlayerFactory.Create();
            var pipeline = new VideoEngineMediaPipeline(player);
            pipeline.SetRotation(camera.Display.Rotation);

            // Watched from its first picture on; a tile that takes the player later keeps it watched.
            void OnPlaying(
                object? sender,
                PlayerStateChangedEventArgs e)
            {
                if (e.NewState != PlayerState.Playing)
                {
                    return;
                }

                player.StateChanged -= OnPlaying;
                if (!motionDetectionService.IsDetectionActive(cameraId))
                {
                    motionDetectionService.StartDetection(cameraId, pipeline, motion, zones);
                }
            }

            player.StateChanged += OnPlaying;
            parking.Park(
                cameraId,
                player,
                pipeline,
                recordingService,
                p => p.Open(uri, options),
                keepWhileIdle: true,
                onRelease: () => motionDetectionService.StopDetection(cameraId));
            player.Open(uri, options);
            LogWatchingWithoutTile(camera.Display.DisplayName);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogWatchingWithoutTileFailed(ex, camera.Display.DisplayName);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Camera {CameraName} records on motion and no layout shown has it: it is watched without a tile")]
    private partial void LogWatchingWithoutTile(string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Camera {CameraName} could not be opened to be watched without a tile")]
    private partial void LogWatchingWithoutTileFailed(Exception ex, string cameraName);
}