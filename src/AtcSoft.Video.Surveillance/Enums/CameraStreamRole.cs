namespace AtcSoft.Video.Surveillance.Enums;

/// <summary>
/// Which of a network camera's streams to open. A camera without a sub-stream
/// (<see cref="Models.Settings.ConnectionSettings.HasSubStream"/> is false) opens its main
/// stream for either role.
/// </summary>
public enum CameraStreamRole
{
    /// <summary>
    /// The main stream, at full resolution: full screen and recordings.
    /// </summary>
    Main = 0,

    /// <summary>
    /// The sub-stream, at a low resolution: the live grid and motion detection.
    /// </summary>
    Sub = 1,
}
