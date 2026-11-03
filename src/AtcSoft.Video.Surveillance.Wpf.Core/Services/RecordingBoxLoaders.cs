namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

/// <summary>
/// The ways a recording's motion boxes are loaded.
/// </summary>
public static class RecordingBoxLoaders
{
    /// <summary>
    /// How many pictures a second are analysed for the boxes shown over playback: as many as
    /// CameraWall analyses a live camera, so the boxes move as smoothly as they do live.
    /// </summary>
    public const double FramesPerSecond = 15;

    /// <summary>
    /// Runs a recording on this machine through the detector, with its camera's motion settings and
    /// zones (the app's settings when the camera is not known), as *Analyse recording* reads it.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <param name="appSettings">The app's motion settings.</param>
    /// <param name="camera">The camera the recording is of, or <see langword="null"/>.</param>
    /// <returns>The loader.</returns>
    public static RecordingBoxLoader FromFile(
        string videoPath,
        MotionDetectionSettings appSettings,
        AtcSoft.Video.Surveillance.Models.CameraConfiguration? camera)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);
        ArgumentNullException.ThrowIfNull(appSettings);

        return cancellationToken => Task.Run<RecordingBoxes?>(
            () =>
            {
                try
                {
                    var settings = RecordingAnalysis.SettingsFor(camera, appSettings);
                    var rotation = MediaProbe.GetRotation(videoPath);
                    var (displayWidth, displayHeight) = MediaProbe.GetDisplaySize(videoPath);
                    var (width, height) = MotionAnalysisSize.For(settings.AnalysisWidth, displayWidth, displayHeight);
                    using var reader = Atc.Video.Engine.Capture.GreyFrameFileReader.Open(
                        videoPath,
                        width,
                        height,
                        (Atc.Video.Engine.VideoRotation)rotation,
                        RecordingAnalysis.DecoderThreads);
                    return RecordingBoxes.Analyse(
                        reader.ReadFrames(FramesPerSecond, cancellationToken).Select(f => (f.Position, f.Pixels)),
                        width,
                        height,
                        RecordingAnalysis.DetectorFor(camera, appSettings, rotation),
                        cancellationToken);
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or Atc.Video.Engine.FFmpeg.FFmpegException)
                {
                    return null;
                }
            },
            cancellationToken);
    }
}