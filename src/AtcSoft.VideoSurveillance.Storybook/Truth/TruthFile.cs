namespace AtcSoft.VideoSurveillance.Storybook.Truth;

/// <summary>
/// The truth of an encoded scene, kept next to its video (<c>{name}.truth.jsonl</c>): one line a
/// frame, so a player of the video knows where every object was without drawing the scene again.
/// </summary>
public static class TruthFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Gets the truth file that belongs to a video: <c>scene.mkv</c> has <c>scene.truth.jsonl</c>.
    /// </summary>
    /// <param name="videoPath">The video.</param>
    /// <returns>The truth file's path.</returns>
    public static string PathFor(string videoPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);

        return Path.ChangeExtension(videoPath, ".truth.jsonl");
    }

    /// <summary>
    /// Writes the truth of every frame.
    /// </summary>
    /// <param name="path">The truth file.</param>
    /// <param name="frames">The frames, in order.</param>
    public static void Write(
        string path,
        IEnumerable<FrameTruth> frames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(frames);

        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.NewLine = "\n";
        foreach (var frame in frames)
        {
            writer.WriteLine(JsonSerializer.Serialize(frame, Options));
        }
    }

    /// <summary>
    /// Reads the truth of every frame.
    /// </summary>
    /// <param name="path">The truth file.</param>
    /// <returns>The frames, in order.</returns>
    /// <exception cref="StorybookException">A line is no frame's truth.</exception>
    public static IReadOnlyList<FrameTruth> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var frames = new List<FrameTruth>();
        var number = 0;
        foreach (var line in File.ReadLines(path))
        {
            number++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                frames.Add(JsonSerializer.Deserialize<FrameTruth>(line, Options)
                    ?? throw new StorybookException($"{Path.GetFileName(path)} line {number}: empty"));
            }
            catch (JsonException ex)
            {
                throw new StorybookException($"{Path.GetFileName(path)} line {number}: {ex.Message}", ex);
            }
        }

        return frames;
    }
}