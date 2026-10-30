namespace BoundingBoxWpfApp;

/// <summary>
/// What the sample does when it starts: open <c>--storybook file</c> or <c>--recording file</c>,
/// with <c>--rate</c> pictures a second (4 by default, the server's; CameraWall's is 15), or sweep
/// the motion settings over the scenes in <c>--sweep folder</c> and the reviewed recordings in
/// <c>--corpus folder</c> (<c>ATCSOFT_BBOX_CORPUS</c> when not given). The E2E walk uses them
/// instead of the file dialogs.
/// </summary>
/// <param name="StorybookPath">A storybook to open, or <see langword="null"/>.</param>
/// <param name="RecordingPath">A recording to open, or <see langword="null"/>.</param>
/// <param name="AnalysesPerSecond">How many pictures a second are analysed.</param>
/// <param name="SweepFolder">A folder of storybooks to sweep, or <see langword="null"/>.</param>
/// <param name="CorpusFolder">A folder of reviewed recordings to sweep too, or <see langword="null"/>.</param>
/// <param name="LiveRotation">With <c>--live 0|90|180|270</c>, what is opened is also played live, turned so; else <see langword="null"/>.</param>
public sealed record StartupOptions(
    string? StorybookPath,
    string? RecordingPath,
    double AnalysesPerSecond,
    string? SweepFolder = null,
    string? CorpusFolder = null,
    int? LiveRotation = null)
{
    /// <summary>
    /// The variable naming the corpus of reviewed recordings, as for the corpus test.
    /// </summary>
    public const string CorpusVariable = "ATCSOFT_BBOX_CORPUS";

    /// <summary>
    /// The rate when none is given: the server's.
    /// </summary>
    public const double DefaultAnalysesPerSecond = 4;

    /// <summary>
    /// Reads the command line; an unknown argument is passed over.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The options.</returns>
    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? storybook = null;
        string? recording = null;
        string? sweep = null;
        string? corpus = null;
        int? live = null;
        var rate = DefaultAnalysesPerSecond;

        // Each option is a name and the value after it.
        foreach (var (name, value) in args.Zip(args.Skip(1)))
        {
            switch (name)
            {
                case "--storybook":
                    storybook = value;
                    break;
                case "--recording":
                    recording = value;
                    break;
                case "--sweep":
                    sweep = value;
                    break;
                case "--corpus":
                    corpus = value;
                    break;
                case "--live" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var turn) && turn is 0 or 90 or 180 or 270:
                    live = turn;
                    break;
                case "--rate" when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0:
                    rate = parsed;
                    break;
            }
        }

        return new StartupOptions(storybook, recording, rate, sweep, corpus, live);
    }
}