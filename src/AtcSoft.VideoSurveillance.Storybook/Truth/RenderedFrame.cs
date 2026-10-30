namespace AtcSoft.VideoSurveillance.Storybook.Truth;

/// <summary>
/// One drawn frame: its grey pixels and its truth.
/// </summary>
/// <param name="Index">The frame number, from 0.</param>
/// <param name="Time">The time in seconds.</param>
/// <param name="Luma">The grey pixels, row by row.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="Truth">Everything drawn that a detector is scored on.</param>
/// <param name="IgnoreAll">Whether the whole frame is <c>dontCare</c> (a lighting step).</param>
[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "A frame is handed on as it is.")]
public sealed record RenderedFrame(
    int Index,
    double Time,
    byte[] Luma,
    int Width,
    int Height,
    IReadOnlyList<TruthBox> Truth,
    bool IgnoreAll);