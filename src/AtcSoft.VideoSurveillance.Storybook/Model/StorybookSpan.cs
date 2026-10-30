namespace AtcSoft.VideoSurveillance.Storybook.Model;

/// <summary>
/// A stretch of time with its own truth: an object standing still (<c>dontCare</c>), a lighting
/// step for the whole picture (<c>dontCare</c> without an object), a distractor that must never
/// get a box (<c>mustNotBox</c>).
/// </summary>
public sealed class StorybookSpan
{
    /// <summary>
    /// Gets or sets the object it is about, or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("object")]
    public string? ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the distractor it is about, or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("distractor")]
    public string? DistractorId { get; set; }

    public double From { get; set; }

    public double To { get; set; } = double.MaxValue;

    /// <summary>
    /// Gets or sets the state: <c>mustBox</c>, <c>mustNotBox</c> or <c>dontCare</c>.
    /// </summary>
    public string State { get; set; } = "dontCare";

    public string? Why { get; set; }
}