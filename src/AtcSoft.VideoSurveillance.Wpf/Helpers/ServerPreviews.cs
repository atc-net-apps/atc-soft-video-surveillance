namespace AtcSoft.VideoSurveillance.Wpf.Helpers;

using ApiPreviewSheet = global::VideoSurveillance.Generated.Recordings.Models.RecordingPreviewSheet;
using CoreRecordingPreviewEvent = AtcSoft.VideoSurveillance.Models.RecordingPreviewEvent;
using CoreRecordingPreviews = AtcSoft.VideoSurveillance.Models.RecordingPreviews;
using PreviewTiles = AtcSoft.VideoSurveillance.Wpf.Core.Services.RecordingPreviewTiles;
using RecordingPreviewLoad = AtcSoft.VideoSurveillance.Wpf.Core.Services.RecordingPreviewLoad;
using RecordingPreviewLoader = AtcSoft.VideoSurveillance.Wpf.Core.Services.RecordingPreviewLoader;

/// <summary>
/// The preview pictures of a recording as the server has them, for the timeline under the player:
/// where each tile is (<c>GET /recordings/previews</c>), then the JPEG, fetched with the session's
/// media token like a thumbnail.
/// </summary>
public static class ServerPreviews
{
    // One client for every download: a file of the recordings needs no headers of its own.
    private static readonly HttpClient Downloads = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>
    /// Gets the loader of a recording's previews.
    /// </summary>
    /// <param name="gateway">The server.</param>
    /// <param name="fileUrl">Turns a file's path below the recordings root into its address, with the media token.</param>
    /// <param name="filePath">The recording's path, as the listing gives it.</param>
    public static RecordingPreviewLoader Loader(
        GatewayService gateway,
        Func<string, Uri> fileUrl,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(fileUrl);

        return cancellationToken => LoadAsync(gateway, fileUrl, filePath, cancellationToken);
    }

    /// <summary>
    /// Turns the server's answer into the layout of the tiles.
    /// </summary>
    /// <param name="sheet">The server's answer.</param>
    /// <returns>The layout, or <see langword="null"/> when there are no previews.</returns>
    public static CoreRecordingPreviews? ToLayout(ApiPreviewSheet? sheet)
        => sheet is { HasPreviews: true, GridCount: > 0, TileWidth: > 0, TileHeight: > 0, Columns: > 0 }
            ? new CoreRecordingPreviews(
                sheet.IntervalSeconds,
                sheet.TileWidth,
                sheet.TileHeight,
                sheet.Columns,
                sheet.GridCount,
                [.. sheet.Events.Select(e => new CoreRecordingPreviewEvent(e.Seconds, e.Tile))])
            : null;

    private static async Task<RecordingPreviewLoad> LoadAsync(
        GatewayService gateway,
        Func<string, Uri> fileUrl,
        string filePath,
        CancellationToken cancellationToken)
    {
        var answer = await gateway.GetRecordingPreviewsAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (ToLayout(answer) is not { } layout || answer!.ImagePath is not { Length: > 0 } imagePath)
        {
            return new RecordingPreviewLoad(Sheet: null, IsBeingMade: answer?.IsBeingMade == true);
        }

        var jpeg = await Downloads.GetByteArrayAsync(fileUrl(imagePath), cancellationToken).ConfigureAwait(false);
        return new RecordingPreviewLoad(PreviewTiles.FromBytes(layout, jpeg), IsBeingMade: false);
    }
}