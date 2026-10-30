namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// Tries combinations of the motion settings a person can change over every scene (on the fast
/// path, each drawn once per rate) and every reviewed recording (as *Analyse recording* reads it),
/// and ranks them, to see which defaults do best and how the shipped ones compare. A scene keeps
/// its own settings (a door camera's lighting) and gets the combination on top.
/// </summary>
public static class StorybookSweep
{
    /// <summary>
    /// Gets the combinations tried when none are given: sensitivity 10 to 70 in steps of 10, the
    /// smallest object 0.01, 0.02 and 0.05 % of the picture, and the stopped-object time 60 s. The
    /// shipped defaults are among them.
    /// </summary>
    public static IReadOnlyList<SweepSettings> DefaultGrid { get; } =
    [
        .. from sensitivity in new[] { 10, 20, 30, 40, 50, 60, 70 }
           from smallest in new[] { 0.01, 0.02, 0.05 }
           select new SweepSettings(sensitivity, smallest, MotionDetectorOptions.DefaultStoppedObjectSeconds),
    ];

    /// <summary>
    /// Gets the rates tried when none are given: the server's and CameraWall's.
    /// </summary>
    public static IReadOnlyList<double> DefaultRates { get; } = [4, 15];

    /// <summary>
    /// Runs a sweep.
    /// </summary>
    /// <param name="scenes">The scenes.</param>
    /// <param name="corpus">The reviewed recordings; reading them needs the engine started.</param>
    /// <param name="grid">The combinations; <see cref="DefaultGrid"/> when <see langword="null"/>.</param>
    /// <param name="rates">The rates the scenes run at; <see cref="DefaultRates"/> when <see langword="null"/>.</param>
    /// <param name="progress">Told the share done, 0 to 1.</param>
    /// <param name="cancellationToken">Stops the sweep.</param>
    /// <returns>The results, best first (<see cref="Rank"/>).</returns>
    public static IReadOnlyList<SweepResult> Run(
        IReadOnlyList<SweepScene> scenes,
        IReadOnlyList<SweepRecording> corpus,
        IReadOnlyList<SweepSettings>? grid = null,
        IReadOnlyList<double>? rates = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenes);
        ArgumentNullException.ThrowIfNull(corpus);

        grid ??= DefaultGrid;
        rates ??= DefaultRates;
        var sceneResults = grid.Select(_ => new List<SweepSceneResult>()).ToList();
        var corpusResults = grid.Select(_ => new List<SweepCorpusResult>()).ToList();
        var steps = (scenes.Count * rates.Count) + corpus.Count;
        var done = 0;

        foreach (var scene in scenes)
        {
            foreach (var rate in rates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pictures = StorybookRunner.Draw(scene.Scene, new StorybookRunOptions(AnalysesPerSecond: rate));
                var results = new SweepSceneResult[grid.Count];
                Parallel.For(0, grid.Count, new ParallelOptions { CancellationToken = cancellationToken }, g =>
                {
                    var detector = new BuiltInMotionDetector(grid[g].ApplyTo(scene.Scene.Detector.Settings()));
                    var score = StorybookRunner.RunDrawn(scene.Scene, detector, pictures).Score;
                    results[g] = new SweepSceneResult(
                        scene.Name,
                        rate,
                        scene.Scene.Expect.KnownBad is not null,
                        StorybookScorer.Meets(scene.Scene.Expect, score),
                        score.Recall,
                        score.FalseBoxesPerFrame);
                });

                for (var g = 0; g < grid.Count; g++)
                {
                    sceneResults[g].Add(results[g]);
                }

                progress?.Report(++done / (double)steps);
            }
        }

        foreach (var recording in corpus)
        {
            var results = new SweepCorpusResult[grid.Count];
            Parallel.For(0, grid.Count, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 4) }, g =>
            {
                var (_, score) = ReviewRunner.Run(recording.VideoPath, recording.Review, settings: grid[g].ApplyTo(recording.Review.Detector.Settings), cancellationToken: cancellationToken);
                results[g] = new SweepCorpusResult(recording.Review.Recording.File, score.Recall, score.FalseBoxesPerPicture);
            });

            for (var g = 0; g < grid.Count; g++)
            {
                corpusResults[g].Add(results[g]);
            }

            progress?.Report(++done / (double)steps);
        }

        return Rank(grid.Select((settings, g) => new SweepResult(settings, sceneResults[g], corpusResults[g])));
    }

    /// <summary>
    /// Ranks results, best first: the most scene runs meeting their expectations, then the best
    /// recall on the reviewed recordings and the fewest false boxes there, then the best mean recall
    /// and the fewest false boxes over the scenes; the shipped defaults first on a tie.
    /// </summary>
    /// <param name="results">The results.</param>
    /// <returns>The results, best first.</returns>
    public static IReadOnlyList<SweepResult> Rank(
        IEnumerable<SweepResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return
        [
            .. results
                .OrderByDescending(r => r.ScenesMet)
                .ThenByDescending(r => Math.Round(r.CorpusRecall, 3))
                .ThenBy(r => Math.Round(r.CorpusFalseBoxesPerPicture, 3))
                .ThenByDescending(r => Math.Round(r.MeanRecall, 3))
                .ThenBy(r => Math.Round(r.MeanFalseBoxesPerFrame, 3))
                .ThenByDescending(r => r.IsDefault),
        ];
    }

    /// <summary>
    /// Writes ranked results as a Markdown report: the ranking, where the shipped defaults stand,
    /// and the scenes where the best combination and the defaults differ.
    /// </summary>
    /// <param name="ranked">The results, best first.</param>
    /// <returns>The report.</returns>
    public static string Report(IReadOnlyList<SweepResult> ranked)
    {
        ArgumentNullException.ThrowIfNull(ranked);

        var text = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;
        text.AppendLine("# Motion settings sweep").AppendLine();
        if (ranked.Count == 0)
        {
            return text.AppendLine("Nothing was tried.").ToString();
        }

        var best = ranked[0];
        var defaults = ranked.FirstOrDefault(r => r.IsDefault);
        text.AppendLine(culture, $"{ranked.Count} combinations over {best.Scenes.Count} scene runs and {best.Corpus.Count} reviewed recordings.").AppendLine();
        text.AppendLine(defaults is null
            ? "The shipped defaults were not among them."
            : ReferenceEquals(defaults, best)
                ? "The shipped defaults rank first."
                : string.Create(culture, $"The shipped defaults rank {ranked.ToList().IndexOf(defaults) + 1}; the best is {best.Settings}."));
        text.AppendLine();
        text.AppendLine("| # | Settings | Scenes met | Mean recall | False boxes/frame | Corpus recall | Corpus false boxes/picture |");
        text.AppendLine("|---|---|---|---|---|---|---|");
        for (var i = 0; i < ranked.Count; i++)
        {
            var r = ranked[i];
            text.AppendLine(culture, $"| {i + 1} | {r.Settings}{(r.IsDefault ? " (defaults)" : string.Empty)} | {r.ScenesMet} of {r.ScenesJudged} | {r.MeanRecall:0.000} | {r.MeanFalseBoxesPerFrame:0.000} | {r.CorpusRecall:0.000} | {r.CorpusFalseBoxesPerPicture:0.000} |");
        }

        if (defaults is not null && !ReferenceEquals(defaults, best))
        {
            var changed = best.Scenes.Zip(defaults.Scenes).Where(p => p.First.Meets != p.Second.Meets || Math.Abs(p.First.Recall - p.Second.Recall) > 0.005 || Math.Abs(p.First.FalseBoxesPerFrame - p.Second.FalseBoxesPerFrame) > 0.005).ToList();
            text.AppendLine().AppendLine("## Where the best and the defaults differ").AppendLine();
            if (changed.Count == 0)
            {
                text.AppendLine("Nowhere by more than 0.005.");
            }
            else
            {
                text.AppendLine("| Scene | Rate | Best: met, recall, false/frame | Defaults: met, recall, false/frame |");
                text.AppendLine("|---|---|---|---|");
                foreach (var (b, d) in changed)
                {
                    text.AppendLine(culture, $"| {b.Scene}{(b.IsKnownBad ? " (known bad)" : string.Empty)} | {b.AnalysesPerSecond}/s | {Yes(b.Meets)}, {b.Recall:0.000}, {b.FalseBoxesPerFrame:0.000} | {Yes(d.Meets)}, {d.Recall:0.000}, {d.FalseBoxesPerFrame:0.000} |");
                }
            }
        }

        return text.ToString();
    }

    private static string Yes(bool value)
        => value ? "yes" : "no";
}