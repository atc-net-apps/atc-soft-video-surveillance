namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Shows where a recording and its thumbnail end up for a choice of folder
/// layout. The paths are written by the server (RecordingPathHelper in
/// Core); this is the web client's copy of the same rule, kept equal by a
/// test.
/// </summary>
public static class RecordingLayoutExample
{
    private const string ThumbnailsFolder = "thumbnails";

    /// <summary>
    /// Builds the example paths, relative to the recording folder.
    /// </summary>
    public static (string Video, string Thumbnail) Build(
        string cameraName,
        DateTime timestamp,
        string? format,
        bool useDailySubfolders,
        bool separateThumbnailFolder)
    {
        var extension = string.IsNullOrWhiteSpace(format) ? "mp4" : format.TrimStart('.').ToLowerInvariant();
        var name = string.Create(CultureInfo.InvariantCulture, $"{cameraName}_{timestamp:yyyyMMdd_HHmmss_fff}");

        var folder = useDailySubfolders
            ? string.Create(CultureInfo.InvariantCulture, $"{cameraName}\\{timestamp:yyyy-MM-dd}")
            : cameraName;

        var thumbnailFolder = separateThumbnailFolder
            ? $"{ThumbnailsFolder}\\{folder}"
            : folder;

        return ($"{folder}\\{name}.{extension}", $"{thumbnailFolder}\\{name}.png");
    }

    /// <summary>
    /// Builds the example for today at a fixed time of day, so the text does
    /// not change every time it is shown.
    /// </summary>
    public static (string Video, string Thumbnail) Build(
        string? format,
        bool useDailySubfolders,
        bool separateThumbnailFolder)
        => Build(
            "Cam1",
            DateTime.Today.AddHours(10).AddMinutes(15),
            format,
            useDailySubfolders,
            separateThumbnailFolder);
}