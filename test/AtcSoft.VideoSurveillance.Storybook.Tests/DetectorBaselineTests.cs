namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// Each detector on every scene, with the scene's own settings, at the server's 4 pictures a second and CameraWall's 15: today's
/// (<see cref="FrameDifferenceMotionDetector"/>) and the new one (<see cref="BuiltInMotionDetector"/>). The scores are kept in
/// <c>baseline.json</c>; a test fails when a score gets worse, so every change to detection is
/// measured. Set <c>ATCSOFT_BBOX_UPDATE_BASELINE=1</c> to write the current scores as the new
/// baseline, on purpose, after an improvement. One class per detector and rate, so they run in
/// parallel.
/// </summary>
public abstract class DetectorBaselineTests(
    string detectorName,
    int analysesPerSecond)
{
    private const string UpdateVariable = "ATCSOFT_BBOX_UPDATE_BASELINE";
    private const double Tolerance = 0.005;
    internal const string FrameDifference = "frameDifference";
    internal const string BuiltIn = "builtIn";

    private static readonly Lock FileLock = new();

    public static TheoryData<string> Scenes()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Scenes"), "*.storybook.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Scenes), MemberType = typeof(DetectorBaselineTests))]
    public void A_Detector_Does_No_Worse_Than_Its_Baseline(string sceneFile)
    {
        var scene = StorybookReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "Scenes", sceneFile));
        IMotionDetector detector = detectorName == BuiltIn
            ? new BuiltInMotionDetector(scene.Detector.Settings())
            : new FrameDifferenceMotionDetector(scene.Detector.Settings());

        var score = EncodedScenes.Run(sceneFile, scene, detector, new StorybookRunOptions(AnalysesPerSecond: analysesPerSecond)).Score;

        var key = $"{Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(sceneFile))}@{analysesPerSecond}" + (detectorName == BuiltIn ? "/builtIn" : string.Empty);
        TestContext.Current.TestOutputHelper?.WriteLine($"{key}: {score.Summary}");
        foreach (var item in score.Objects)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {item.Id}: recall {item.Recall:0.000}, latency {item.LatencySeconds?.ToString("0.00", CultureInfo.InvariantCulture) ?? "never"}"));
        }

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Write(key, score);
            return;
        }

        var baseline = Read(key);
        baseline.Should().NotBeNull($"no baseline for {key}: run the tests once with {UpdateVariable}=1 and commit baseline.json");
        score.Recall.Should().BeGreaterThanOrEqualTo(baseline!.Recall - Tolerance, $"recall of {key} got worse than the baseline");
        score.FalseBoxesPerFrame.Should().BeLessThanOrEqualTo(baseline.FalseBoxesPerFrame + Tolerance, $"false boxes of {key} got worse than the baseline");
    }

    private static string SourceFile()
    {
        // The project folder, found from the build output, so an update writes the file that is committed.
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "AtcSoft.VideoSurveillance.Storybook.Tests.csproj")))
        {
            folder = folder.Parent;
        }

        return Path.Combine(folder?.FullName ?? AppContext.BaseDirectory, "baseline.json");
    }

    private static BaselineEntry? Read(string key)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "baseline.json");
        if (!File.Exists(file))
        {
            return null;
        }

        var entries = JsonSerializer.Deserialize<Dictionary<string, BaselineEntry>>(File.ReadAllText(file), Json.Options);
        return entries?.GetValueOrDefault(key);
    }

    private static void Write(
        string key,
        StorybookScore score)
    {
        lock (FileLock)
        {
            var file = SourceFile();
            var entries = File.Exists(file)
                ? JsonSerializer.Deserialize<SortedDictionary<string, BaselineEntry>>(File.ReadAllText(file), Json.Options) ?? new SortedDictionary<string, BaselineEntry>(StringComparer.Ordinal)
                : new SortedDictionary<string, BaselineEntry>(StringComparer.Ordinal);
            entries[key] = new BaselineEntry(
                Math.Round(score.Recall, 4),
                Math.Round(score.Precision, 4),
                Math.Round(score.FalseBoxesPerFrame, 4),
                score.MeanLatencySeconds is { } latency ? Math.Round(latency, 3) : null);
            File.WriteAllText(file, JsonSerializer.Serialize(entries, Json.Options).ReplaceLineEndings("\n"));
        }
    }

    private sealed record BaselineEntry(
        double Recall,
        double Precision,
        double FalseBoxesPerFrame,
        double? MeanLatencySeconds);

    private static class Json
    {
        public static JsonSerializerOptions Options { get; } = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
    }
}