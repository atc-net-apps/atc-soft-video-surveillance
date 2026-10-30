namespace AtcSoft.VideoSurveillance.Api.Mapping;

internal static class AppSettingsTestExtensions
{
    /// <summary>
    /// What a client sends back: the settings it read, as an update, the way they go over the wire.
    /// </summary>
    public static UpdateSettingsRequest AsUpdateRequest(
        this AppSettings settings)
        => JsonSerializer.Deserialize<UpdateSettingsRequest>(JsonSerializer.Serialize(settings))!;
}