namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// Reads and writes a recording's review (<c>{video}.review.json</c>, see
/// <see cref="RecordingPathHelper.BuildReviewPath"/>). Times are written as <c>hh:mm:ss.fff</c>
/// and verdicts as their names, so a review can be read and edited by hand; a file that is not a
/// valid review is refused with what is wrong.
/// </summary>
public static class RecordingReviewFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new ReviewPositionConverter(),
        },
    };

    /// <summary>
    /// Reads a review.
    /// </summary>
    /// <param name="path">The review file.</param>
    /// <returns>The review.</returns>
    /// <exception cref="InvalidDataException">The file is not a valid review; the message says why.</exception>
    public static RecordingReview Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        return Parse(File.ReadAllText(path), path);
    }

    /// <summary>
    /// Reads a review from its text.
    /// </summary>
    /// <param name="json">The review as JSON.</param>
    /// <param name="source">Where it came from, for the message of an error.</param>
    /// <returns>The review.</returns>
    /// <exception cref="InvalidDataException">The text is not a valid review; the message says why.</exception>
    public static RecordingReview Parse(
        string json,
        string source = "review")
    {
        ArgumentNullException.ThrowIfNull(json);

        RecordingReview? review;
        try
        {
            review = JsonSerializer.Deserialize<RecordingReview>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{source}: {ex.Message}", ex);
        }

        if (review is null)
        {
            throw new InvalidDataException($"{source}: empty");
        }

        var errors = Validate(review).ToList();
        return errors.Count == 0
            ? review
            : throw new InvalidDataException($"{source}: " + string.Join("; ", errors));
    }

    /// <summary>
    /// Writes a review as JSON.
    /// </summary>
    /// <param name="review">The review.</param>
    /// <returns>The JSON.</returns>
    /// <exception cref="InvalidDataException">The review is not valid; the message says why.</exception>
    public static string ToJson(RecordingReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        var errors = Validate(review).ToList();
        return errors.Count == 0
            ? JsonSerializer.Serialize(review, Options)
            : throw new InvalidDataException(string.Join("; ", errors));
    }

    /// <summary>
    /// Writes a review to a file of its own first, then puts it in place, so a reader never sees half
    /// a review.
    /// </summary>
    /// <param name="path">The review file.</param>
    /// <param name="review">The review.</param>
    /// <exception cref="InvalidDataException">The review is not valid; the message says why.</exception>
    public static void Write(
        string path,
        RecordingReview review)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var json = ToJson(review);
        var part = path + ".part";
        File.WriteAllText(part, json);
        File.Move(part, path, overwrite: true);
    }

    /// <summary>
    /// Gets the SHA-256 of a file, in lower-case hex, as a review names its recording.
    /// </summary>
    /// <param name="path">The file.</param>
    public static string Sha256Of(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static IEnumerable<string> Validate(RecordingReview review)
    {
        if (review.Version != RecordingReview.FormatVersion)
        {
            yield return $"version: {review.Version} is not supported ({RecordingReview.FormatVersion} is)";
        }

        if (string.IsNullOrWhiteSpace(review.Recording?.File))
        {
            yield return "recording.file: missing";
        }

        if (review.Recording?.Sha256 is not { Length: 64 } sha || !sha.All(char.IsAsciiHexDigitLower))
        {
            yield return "recording.sha256: 64 lower-case hex digits";
        }

        if (review.Detector?.Settings is null)
        {
            yield return "detector.settings: missing";
        }

        for (var i = 0; i < review.Spans.Count; i++)
        {
            var span = review.Spans[i];
            if (!ReviewVerdicts.All.Contains(span.Verdict, StringComparer.Ordinal))
            {
                yield return $"spans[{i}].verdict: '{span.Verdict}' is not one of {string.Join(", ", ReviewVerdicts.All)}";
            }

            if (span.From < TimeSpan.Zero || span.To <= span.From)
            {
                yield return $"spans[{i}]: from {span.From} to {span.To} is not a stretch";
            }

            if (span.Count is < 1)
            {
                yield return $"spans[{i}].count: at least 1";
            }

            if (span.Verdict == ReviewVerdicts.FalseBox && span.Count is not null)
            {
                yield return $"spans[{i}].count: a false box has no count";
            }

            if (span.Box is { } box && (box.X is < 0 or > 1 || box.Y is < 0 or > 1 || box.W is <= 0 or > 1 || box.H is <= 0 or > 1 || box.X + box.W > 1.0001 || box.Y + box.H > 1.0001))
            {
                yield return $"spans[{i}].box: inside the picture, as shares from 0 to 1";
            }
        }
    }
}