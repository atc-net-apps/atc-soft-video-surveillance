namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// Scores the boxes a detector showed against a scene's truth. Per frame, boxes are matched to
/// objects greedily, best overlap first: a box matches an object at the expected overlap
/// (<see cref="StorybookExpect.MatchIou"/>) or when it holds the object's centre. Each object
/// takes one box; a second box on the same object counts as false, since the goal is one box
/// per object.
/// </summary>
public static class StorybookScorer
{
    /// <summary>
    /// Gets whether a score meets a scene's expectations: its recall, its false boxes per frame
    /// and, where it gives them, its number of tracks and id switches.
    /// </summary>
    /// <param name="expect">The scene's expectations.</param>
    /// <param name="score">The score.</param>
    public static bool Meets(
        StorybookExpect expect,
        StorybookScore score)
    {
        ArgumentNullException.ThrowIfNull(expect);
        ArgumentNullException.ThrowIfNull(score);

        return score.Recall >= expect.MinRecall &&
               score.FalseBoxesPerFrame <= expect.MaxFalseBoxesPerFrame &&
               (expect.Tracks is not { } tracks || (score.TrackCount == tracks && score.Objects.All(o => o.IdSwitches <= expect.MaxIdSwitchesPerObject)));
    }

    /// <summary>
    /// Scores a run.
    /// </summary>
    /// <param name="frames">The analysed frames, in time order.</param>
    /// <param name="expect">The scene's expectations (overlap, latency allowance).</param>
    /// <returns>The score.</returns>
    public static StorybookScore Score(
        IReadOnlyList<ScoredFrame> frames,
        StorybookExpect expect)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(expect);

        var firstMustBox = new Dictionary<string, double>(StringComparer.Ordinal);
        var firstHit = new Dictionary<string, double>(StringComparer.Ordinal);
        var mustFrames = new Dictionary<string, int>(StringComparer.Ordinal);
        var hitFrames = new Dictionary<string, int>(StringComparer.Ordinal);
        var lastTrack = new Dictionary<string, int>(StringComparer.Ordinal);
        var switches = new Dictionary<string, int>(StringComparer.Ordinal);
        var byDistractor = new Dictionary<string, int>(StringComparer.Ordinal);
        var trackIds = new HashSet<int>();
        var anyWithoutTrack = false;
        var scored = 0;
        var truePositives = 0;
        var falseBoxes = 0;

        foreach (var frame in frames)
        {
            foreach (var detected in frame.Detected)
            {
                if (detected.TrackId is { } id)
                {
                    trackIds.Add(id);
                }
                else
                {
                    anyWithoutTrack = true;
                }
            }

            foreach (var truth in frame.Truth.Where(t => !t.IsDistractor && t.State == TruthState.MustBox))
            {
                firstMustBox.TryAdd(truth.Id, frame.Time);
                mustFrames.TryAdd(truth.Id, 0);
                hitFrames.TryAdd(truth.Id, 0);
            }

            if (frame.IgnoreAll)
            {
                continue;
            }

            scored++;
            var objects = frame.Truth.Where(t => !t.IsDistractor && t.State != TruthState.MustNotBox).ToList();
            var matches = Match(frame.Detected, objects, expect.MatchIou);

            for (var d = 0; d < frame.Detected.Count; d++)
            {
                if (matches.ContainsKey(d))
                {
                    continue;
                }

                falseBoxes++;
                foreach (var distractor in frame.Truth.Where(t => t.IsDistractor && Overlaps(frame.Detected[d].Box, t.Box)))
                {
                    byDistractor[distractor.Id] = byDistractor.GetValueOrDefault(distractor.Id) + 1;
                }
            }

            foreach (var (d, o) in matches)
            {
                var truth = objects[o];
                if (truth.State != TruthState.MustBox)
                {
                    continue;
                }

                truePositives++;
                firstHit.TryAdd(truth.Id, frame.Time);
                if (frame.Detected[d].TrackId is { } trackId)
                {
                    if (lastTrack.TryGetValue(truth.Id, out var last) && last != trackId)
                    {
                        switches[truth.Id] = switches.GetValueOrDefault(truth.Id) + 1;
                    }

                    lastTrack[truth.Id] = trackId;
                }
            }

            var matchedObjects = matches.Values.ToHashSet();
            for (var o = 0; o < objects.Count; o++)
            {
                var truth = objects[o];
                if (truth.State != TruthState.MustBox || frame.Time < firstMustBox[truth.Id] + expect.MaxLatencySeconds)
                {
                    continue;
                }

                mustFrames[truth.Id]++;
                if (matchedObjects.Contains(o))
                {
                    hitFrames[truth.Id]++;
                }
            }
        }

        var hasTracks = trackIds.Count > 0 && !anyWithoutTrack;
        var perObject = firstMustBox.Keys
            .Order(StringComparer.Ordinal)
            .Select(id => new ObjectScore(
                id,
                mustFrames[id],
                hitFrames[id],
                firstHit.TryGetValue(id, out var hit) ? Math.Max(0, hit - firstMustBox[id]) : null,
                hasTracks ? switches.GetValueOrDefault(id) : null))
            .ToList();

        var totalMust = perObject.Sum(o => o.MustBoxFrames);
        var latencies = perObject.Where(o => o.LatencySeconds is not null).Select(o => o.LatencySeconds!.Value).ToList();

        return new StorybookScore(
            scored,
            totalMust == 0 ? 1 : perObject.Sum(o => o.HitFrames) / (double)totalMust,
            truePositives + falseBoxes == 0 ? 1 : truePositives / (double)(truePositives + falseBoxes),
            falseBoxes,
            scored == 0 ? 0 : falseBoxes / (double)scored,
            latencies.Count == 0 ? null : latencies.Average(),
            hasTracks ? trackIds.Count : null,
            hasTracks ? switches.Values.Sum() : null,
            perObject,
            byDistractor);
    }

    // Greedy, best first: detected box index -> object index.
    private static Dictionary<int, int> Match(
        IReadOnlyList<DetectedBox> detected,
        List<TruthBox> objects,
        double matchIou)
    {
        var pairs = new List<(int D, int O, double Score)>();
        for (var d = 0; d < detected.Count; d++)
        {
            for (var o = 0; o < objects.Count; o++)
            {
                var box = detected[d].Box;
                var truth = objects[o].Box;
                var iou = box.IntersectionOverUnion(truth);
                var holdsCentre = box.Contains(truth.CenterX, truth.CenterY);
                if (iou >= matchIou || holdsCentre)
                {
                    pairs.Add((d, o, iou + (holdsCentre ? 0.01 : 0)));
                }
            }
        }

        var matches = new Dictionary<int, int>();
        var taken = new HashSet<int>();
        foreach (var (d, o, _) in pairs.OrderByDescending(p => p.Score))
        {
            if (!matches.ContainsKey(d) && taken.Add(o))
            {
                matches[d] = o;
            }
        }

        return matches;
    }

    private static bool Overlaps(
        PixelBox box,
        PixelBox region)
        => box.IntersectionOverUnion(region) > 0 || region.Contains(box.CenterX, box.CenterY);
}