namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

/// <summary>
/// Gets a recording's motion boxes, picture by picture, for showing them over its playback.
/// CameraWall runs the recording through the detector with its camera's settings
/// (<see cref="RecordingBoxLoaders.FromFile"/>).
/// </summary>
/// <param name="cancellationToken">Cancels the analysis (the player closed).</param>
/// <returns>The boxes, or <see langword="null"/> when the recording cannot be read.</returns>
public delegate Task<RecordingBoxes?> RecordingBoxLoader(
    CancellationToken cancellationToken);