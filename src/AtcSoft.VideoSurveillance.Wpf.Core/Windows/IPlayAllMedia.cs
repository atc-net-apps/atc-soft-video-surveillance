namespace AtcSoft.VideoSurveillance.Wpf.Core.Windows;

/// <summary>
/// The player of one tile of *play all from here*: a <see cref="MediaElement"/> in the window, a
/// fake in tests.
/// </summary>
public interface IPlayAllMedia
{
    /// <summary>Occurs when an opened recording shows its first picture at the asked position.</summary>
    event EventHandler? Opened;

    /// <summary>Occurs when the recording has played to its end.</summary>
    event EventHandler? Ended;

    /// <summary>Occurs when the recording cannot be played.</summary>
    event EventHandler? Failed;

    /// <summary>Gets how far into the recording the player is.</summary>
    TimeSpan Position { get; }

    /// <summary>Opens a recording and plays it from a position.</summary>
    /// <param name="address">The recording.</param>
    /// <param name="position">How far into it.</param>
    void Open(
        Uri address,
        TimeSpan position);

    /// <summary>Plays.</summary>
    void Play();

    /// <summary>Pauses.</summary>
    void Pause();

    /// <summary>Stops and lets go of the recording.</summary>
    void Close();
}