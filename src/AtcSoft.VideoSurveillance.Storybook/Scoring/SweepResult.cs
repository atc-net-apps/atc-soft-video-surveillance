namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// How one combination of settings did over the scenes and the reviewed recordings.
/// </summary>
/// <param name="Settings">The combination.</param>
/// <param name="Scenes">Each scene at each rate.</param>
/// <param name="Corpus">Each reviewed recording.</param>
public sealed record SweepResult(
    SweepSettings Settings,
    IReadOnlyList<SweepSceneResult> Scenes,
    IReadOnlyList<SweepCorpusResult> Corpus)
{
    /// <summary>
    /// Gets how many scene runs meet their expectations, known-bad scenes left out.
    /// </summary>
    public int ScenesMet
        => Scenes.Count(s => !s.IsKnownBad && s.Meets);

    /// <summary>
    /// Gets how many scene runs are judged, known-bad scenes left out.
    /// </summary>
    public int ScenesJudged
        => Scenes.Count(s => !s.IsKnownBad);

    /// <summary>
    /// Gets the mean recall over the judged scene runs.
    /// </summary>
    public double MeanRecall
        => Mean(Scenes.Where(s => !s.IsKnownBad).Select(s => s.Recall), whenNone: 1);

    /// <summary>
    /// Gets the mean false boxes per frame over the judged scene runs.
    /// </summary>
    public double MeanFalseBoxesPerFrame
        => Mean(Scenes.Where(s => !s.IsKnownBad).Select(s => s.FalseBoxesPerFrame), whenNone: 0);

    /// <summary>
    /// Gets the mean recall over the reviewed recordings (1 without any).
    /// </summary>
    public double CorpusRecall
        => Mean(Corpus.Select(c => c.Recall), whenNone: 1);

    /// <summary>
    /// Gets the mean false boxes per picture over the reviewed recordings (0 without any).
    /// </summary>
    public double CorpusFalseBoxesPerPicture
        => Mean(Corpus.Select(c => c.FalseBoxesPerPicture), whenNone: 0);

    /// <summary>
    /// Gets a value indicating whether this is the combination the apps ship with.
    /// </summary>
    public bool IsDefault
        => Settings == SweepSettings.Defaults;

    private static double Mean(
        IEnumerable<double> values,
        double whenNone)
    {
        var list = values.ToList();
        return list.Count == 0 ? whenNone : list.Average();
    }
}