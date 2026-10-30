namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// Where the preview pictures of a recording are in their JPEG (<c>{video}.previews.jpg</c>): small
/// tiles of the same size, row by row, <see cref="Columns"/> to a row. First the grid, a tile every
/// <see cref="IntervalSeconds"/> from the start; then a tile for each event, of the moment most
/// moved in it. A timeline shows the tile of the place under the mouse, and on an event its own.
/// </summary>
/// <param name="IntervalSeconds">How far apart the grid's tiles are, in seconds.</param>
/// <param name="TileWidth">The width of a tile, in pixels, as displayed.</param>
/// <param name="TileHeight">The height of a tile, in pixels, as displayed.</param>
/// <param name="Columns">How many tiles a row of the JPEG has.</param>
/// <param name="GridCount">How many tiles the grid has: tile <c>i</c> is <c>i</c> times the interval into the recording.</param>
/// <param name="Events">The events' tiles, from the start, after the grid.</param>
public sealed record RecordingPreviews(
    double IntervalSeconds,
    int TileWidth,
    int TileHeight,
    int Columns,
    int GridCount,
    IReadOnlyList<RecordingPreviewEvent> Events);