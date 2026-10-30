namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// *Analyse recording*: gives a recording made without an activity index (before the index
/// existed, or with it turned off) the same index as a recording analysed while it was made, by
/// running the motion detector over its pictures. The pictures come from the video engine; this
/// part knows nothing of video.
/// </summary>
public static class RecordingAnalysis
{
    /// <summary>
    /// How many pictures a second are analysed: as many as for the index of a recording in progress.
    /// </summary>
    public const double FramesPerSecond = 4;

    /// <summary>
    /// How many threads decode a recording for its analysis: a few, so the recording and the live
    /// view of the cameras go on as before.
    /// </summary>
    public const int DecoderThreads = 2;

    /// <summary>
    /// How long the detector only learns the scene at the start of a recording, with no box: it
    /// starts cold on every file, and until the background is learnt the scene itself gives wrong
    /// boxes. Something already moving then is boxed from this time on.
    /// </summary>
    public const double WarmUpSeconds = 3;

    /// <summary>
    /// Gets the settings an older recording of a camera is analysed with: the camera's own, or the
    /// app's when the camera is not known (any more).
    /// </summary>
    /// <param name="camera">The camera the recording is of, or <see langword="null"/>.</param>
    /// <param name="appSettings">The app's motion settings.</param>
    public static MotionDetectionSettings SettingsFor(
        CameraConfiguration? camera,
        MotionDetectionSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(appSettings);

        return camera is null
            ? appSettings
            : MotionDetectionPolicy.Resolve(camera, appSettings);
    }

    /// <summary>
    /// Gets the camera a recording is of: the one whose folder (by its name and id) the recording
    /// is in under the recordings root.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <param name="recordingRoot">The recordings root.</param>
    /// <param name="cameras">The cameras.</param>
    /// <returns>The camera, or <see langword="null"/> when none has that folder (any more).</returns>
    public static CameraConfiguration? CameraOf(
        string videoPath,
        string? recordingRoot,
        IEnumerable<CameraConfiguration> cameras)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);
        ArgumentNullException.ThrowIfNull(cameras);

        var folder = string.IsNullOrEmpty(recordingRoot) ? null : RecordingPathHelper.GetCameraFolderFromPath(videoPath, recordingRoot);
        return folder is null
            ? null
            : cameras.FirstOrDefault(c => string.Equals(
                RecordingPathHelper.GetCameraFolderName(c.Display.DisplayName, c.Id),
                folder,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the zones an older recording of a camera is analysed with: the camera's, drawn at its
    /// rotation now, turned to the rotation the recording was made at, so they lie on the same part
    /// of the scene in its pictures.
    /// </summary>
    /// <param name="camera">The camera the recording is of, or <see langword="null"/> for none.</param>
    /// <param name="recordingRotation">The recording's rotation in degrees clockwise (its display matrix).</param>
    public static IReadOnlyList<MotionZone> ZonesFor(
        CameraConfiguration? camera,
        int recordingRotation)
        => camera is null
            ? []
            : MotionDetection.MotionZoneGeometry.Turn(camera.MotionZones, camera.Display.Rotation, ToRotation(recordingRotation));

    /// <summary>
    /// Gets the detector an older recording of a camera is analysed with: the camera's settings
    /// and its zones (see <see cref="SettingsFor"/> and <see cref="ZonesFor"/>), learning the scene
    /// for <see cref="WarmUpSeconds"/> first.
    /// </summary>
    /// <param name="camera">The camera the recording is of, or <see langword="null"/>.</param>
    /// <param name="appSettings">The app's motion settings.</param>
    /// <param name="recordingRotation">The recording's rotation in degrees clockwise.</param>
    public static IMotionDetector DetectorFor(
        CameraConfiguration? camera,
        MotionDetectionSettings appSettings,
        int recordingRotation)
        => new MotionDetection.BuiltInMotionDetector(SettingsFor(camera, appSettings), zones: ZonesFor(camera, recordingRotation))
        {
            WarmUpSeconds = WarmUpSeconds,
        };

    /// <summary>
    /// Gets when a recording started: the time in its name, read in the zone it was written in,
    /// else when the file was made.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <param name="nameZone">The zone of the time in the name: CameraWall writes local time, the server UTC.</param>
    public static DateTimeOffset StartOf(
        string videoPath,
        TimeZoneInfo nameZone)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);
        ArgumentNullException.ThrowIfNull(nameZone);

        return RecordingPathHelper.TryParseTimestamp(Path.GetFileName(videoPath), out var named)
            ? LocalTimeline.InZone(named, nameZone)
            : new DateTimeOffset(File.GetCreationTimeUtc(videoPath), TimeSpan.Zero);
    }

    private static CameraRotation ToRotation(int degrees)
        => ((((degrees / 90) % 4) + 4) % 4) switch
        {
            1 => CameraRotation.Rotate90,
            2 => CameraRotation.Rotate180,
            3 => CameraRotation.Rotate270,
            _ => CameraRotation.None,
        };

    /// <summary>
    /// Analyses a recording's pictures and writes its activity index beside it. The index is
    /// written to a temporary file first, so a recording is never left with half an index.
    /// </summary>
    /// <param name="pictures">The recording's pictures from its start, grey at the analysis size, with how far in each is.</param>
    /// <param name="width">The analysis width.</param>
    /// <param name="height">The analysis height.</param>
    /// <param name="detector">The motion detector, with the camera's settings.</param>
    /// <param name="videoPath">The recording.</param>
    /// <param name="start">When the recording started.</param>
    /// <param name="cancellationToken">Stops the analysis; nothing is written then.</param>
    /// <returns>How many pictures were analysed.</returns>
    public static int Run(
        IEnumerable<(TimeSpan Position, byte[] Pixels)> pictures,
        int width,
        int height,
        IMotionDetector detector,
        string videoPath,
        DateTimeOffset start,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pictures);
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        var indexPath = RecordingPathHelper.BuildActivityPath(videoPath);
        var partPath = indexPath + ".part";
        var count = 0;
        try
        {
            using (var writer = ActivityLogWriter.Create(partPath, start))
            {
                detector.Reset();
                foreach (var (position, pixels) in pictures)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var time = start + position;
                    writer.Add(time, detector.Process(new MotionFrame(pixels, width, height, time)));
                    count++;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partPath, indexPath, overwrite: true);
            return count;
        }
        catch
        {
            File.Delete(partPath);
            throw;
        }
    }
}