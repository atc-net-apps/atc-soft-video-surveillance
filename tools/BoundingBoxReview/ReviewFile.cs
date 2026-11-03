namespace BoundingBoxReview;

/// <summary>
/// A review folder's <c>boxes.json</c>: the recording, the settings it was analysed with and, per
/// picture, the detector's boxes and the person's.
/// </summary>
public sealed class ReviewFile
{
    public const string FileName = "boxes.json";

    public const string OriginalFolder = "original";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string Recording { get; set; } = string.Empty;

    public DateTimeOffset Made { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public double EverySeconds { get; set; }

    public double AnalysesPerSecond { get; set; }

    public int AnalysisWidth { get; set; }

    public int AnalysisHeight { get; set; }

    public MotionDetectionSettings Settings { get; set; } = new();

    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Read and written as JSON.")]
    public IList<ReviewPicture> Pictures { get; set; } = [];

    public static ReviewFile Read(string folder)
        => JsonSerializer.Deserialize<ReviewFile>(File.ReadAllText(Path.Combine(folder, FileName)), Options)
           ?? throw new InvalidDataException($"{FileName} in {folder} is empty.");

    public void Write(string folder)
    {
        // Through a temporary file, so a crash never leaves half a review.
        var path = Path.Combine(folder, FileName);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, Options));
        File.Move(temporary, path, overwrite: true);
    }
}