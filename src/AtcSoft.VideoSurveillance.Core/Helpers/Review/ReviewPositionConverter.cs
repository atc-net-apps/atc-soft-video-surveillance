namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// A position in a reviewed video as <c>hh:mm:ss.fff</c>.
/// </summary>
internal sealed class ReviewPositionConverter : System.Text.Json.Serialization.JsonConverter<TimeSpan>
{
    public override TimeSpan Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
        => reader.GetString() is { } text && TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new JsonException($"'{reader.GetString()}' is not a time (hh:mm:ss.fff)");

    public override void Write(
        Utf8JsonWriter writer,
        TimeSpan value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture));
    }
}