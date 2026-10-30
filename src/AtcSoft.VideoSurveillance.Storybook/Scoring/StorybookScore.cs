namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// How well a detector did on a scene.
/// </summary>
/// <param name="ScoredFrames">The frames that counted.</param>
/// <param name="Recall">The share of must-box frames in which the object had a box.</param>
/// <param name="Precision">The share of shown boxes that were on an object.</param>
/// <param name="FalseBoxes">Boxes on no object (fragments of an object beyond the first count too).</param>
/// <param name="FalseBoxesPerFrame">False boxes per counted frame.</param>
/// <param name="MeanLatencySeconds">The mean time to the first box, over the objects found.</param>
/// <param name="TrackCount">The number of track ids seen; <see langword="null"/> without track ids.</param>
/// <param name="IdSwitches">Track id changes over all objects; <see langword="null"/> without track ids.</param>
/// <param name="Objects">Per object.</param>
/// <param name="FalseBoxesByDistractor">False boxes on each distractor.</param>
public sealed record StorybookScore(
    int ScoredFrames,
    double Recall,
    double Precision,
    int FalseBoxes,
    double FalseBoxesPerFrame,
    double? MeanLatencySeconds,
    int? TrackCount,
    int? IdSwitches,
    IReadOnlyList<ObjectScore> Objects,
    IReadOnlyDictionary<string, int> FalseBoxesByDistractor)
{
    /// <summary>
    /// Gets the score as a short line for a log or a test's output.
    /// </summary>
    public string Summary
    {
        get
        {
            var latency = MeanLatencySeconds is { } l ? l.ToString("0.00", CultureInfo.InvariantCulture) + " s" : "n/a";
            var tracks = TrackCount?.ToString(CultureInfo.InvariantCulture) ?? "n/a";
            var switches = IdSwitches?.ToString(CultureInfo.InvariantCulture) ?? "n/a";
            return string.Create(
                CultureInfo.InvariantCulture,
                $"recall {Recall:0.000}, precision {Precision:0.000}, false boxes/frame {FalseBoxesPerFrame:0.000} ({FalseBoxes}), latency {latency}, tracks {tracks}, id switches {switches}");
        }
    }
}