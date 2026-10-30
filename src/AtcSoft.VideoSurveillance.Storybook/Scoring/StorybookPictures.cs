namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// A scene's pictures, drawn once at the analysis size (<see cref="StorybookRunner.Draw"/>), to run
/// several detectors over.
/// </summary>
public sealed class StorybookPictures
{
    internal StorybookPictures(
        IReadOnlyList<StorybookRunner.AnalysisPicture> pictures,
        int analysisWidth,
        int analysisHeight)
    {
        Pictures = pictures;
        AnalysisWidth = analysisWidth;
        AnalysisHeight = analysisHeight;
    }

    /// <summary>
    /// Gets how many pictures there are.
    /// </summary>
    public int Count
        => Pictures.Count;

    /// <summary>
    /// Gets the width the pictures are drawn at.
    /// </summary>
    public int AnalysisWidth { get; }

    /// <summary>
    /// Gets the height the pictures are drawn at.
    /// </summary>
    public int AnalysisHeight { get; }

    internal IReadOnlyList<StorybookRunner.AnalysisPicture> Pictures { get; }
}