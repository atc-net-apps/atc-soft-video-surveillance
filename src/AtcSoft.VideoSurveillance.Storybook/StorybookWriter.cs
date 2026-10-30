namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// Changes a storybook file in place, keeping the rest of it as it is: the scenes are written as
/// .NET writes indented JSON, so only the changed part differs.
/// </summary>
public static class StorybookWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Writes detector settings into a storybook (its <c>detector</c> section): only what differs
    /// from the defaults, and no section when nothing does. The scene's baseline scores are then
    /// those of the new settings, and an encoded scene is encoded again (its hash changes).
    /// </summary>
    /// <param name="path">The storybook file.</param>
    /// <param name="settings">The settings.</param>
    /// <exception cref="StorybookException">The file is no storybook, or the result would not be one.</exception>
    public static void SaveDetector(
        string path,
        MotionDetectionSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(settings);

        var text = File.ReadAllText(path);
        File.WriteAllText(path, WithDetector(text, settings, Path.GetFileName(path)));
    }

    /// <summary>
    /// Gets a storybook's text with other detector settings (see <see cref="SaveDetector"/>).
    /// </summary>
    /// <param name="json">The storybook's text.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="source">What to call it in an error.</param>
    /// <returns>The new text.</returns>
    /// <exception cref="StorybookException">The text is no storybook, or the result would not be one.</exception>
    public static string WithDetector(
        string json,
        MotionDetectionSettings settings,
        string source = "storybook")
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(settings);

        StorybookReader.Read(json, source);
        if (System.Text.Json.Nodes.JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) is not System.Text.Json.Nodes.JsonObject root)
        {
            throw new StorybookException($"{source}: not an object");
        }

        var detector = StorybookDetector.From(settings);
        var section = new System.Text.Json.Nodes.JsonObject();
        if (detector.Sensitivity is { } sensitivity)
        {
            section["sensitivity"] = sensitivity;
        }

        if (detector.SmallestObjectPercent is { } smallest)
        {
            section["smallestObjectPercent"] = smallest;
        }

        if (detector.StoppedObjectSeconds is { } stopped)
        {
            section["stoppedObjectSeconds"] = stopped;
        }

        if (detector.IgnoreLightingChanges is { } ignore)
        {
            section["ignoreLightingChanges"] = ignore;
        }

        // In place when it is there; else before what is expected, where S14 has it.
        var at = root.IndexOf("detector");
        root.Remove("detector");
        if (section.Count > 0)
        {
            if (at < 0)
            {
                var expect = root.IndexOf("expect");
                at = expect < 0 ? root.Count : expect;
            }

            root.Insert(at, "detector", section);
        }

        // The file's own line endings, whatever the writer's are on this machine.
        var result = root.ToJsonString(WriteOptions).ReplaceLineEndings(json.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n");

        if (json.EndsWith('\n'))
        {
            result += json.EndsWith("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        }

        StorybookReader.Read(result, source);
        return result;
    }
}