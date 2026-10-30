namespace AtcSoft.VideoSurveillance.Storybook.Model;

/// <summary>
/// The picture the scene is drawn into.
/// </summary>
public sealed class StorybookVideo
{
    public int Width { get; set; } = 1280;

    public int Height { get; set; } = 720;

    public int Fps { get; set; } = 15;

    public double DurationSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the sensor noise: how far a pixel strays each frame, in grey levels.
    /// </summary>
    public double Noise { get; set; } = 2;

    /// <summary>
    /// Gets or sets the bitrate the scene is encoded at, in kilobits a second, as a camera would
    /// send it; <see langword="null"/> for a scene that is only fed to the detector as drawn. A
    /// scene with a bitrate is scored through the encoder and the decoder, with their artefacts.
    /// </summary>
    public int? BitrateKbps { get; set; }

    /// <summary>
    /// Gets the number of frames.
    /// </summary>
    public int FrameCount
        => (int)Math.Round(DurationSeconds * Fps);
}