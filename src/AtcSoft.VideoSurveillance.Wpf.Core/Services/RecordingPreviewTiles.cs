namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// The preview pictures of a recording, loaded: the JPEG of tiles and where each tile is in it
/// (<see cref="RecordingPreviews"/>). A timeline takes the tile of the place under the mouse.
/// </summary>
public sealed class RecordingPreviewTiles
{
    private readonly Dictionary<int, BitmapSource> tiles = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingPreviewTiles"/> class.
    /// </summary>
    /// <param name="layout">Where each tile is.</param>
    /// <param name="image">The JPEG of tiles, frozen.</param>
    public RecordingPreviewTiles(
        RecordingPreviews layout,
        BitmapSource image)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Image = image ?? throw new ArgumentNullException(nameof(image));
    }

    /// <summary>
    /// Gets where each tile is.
    /// </summary>
    public RecordingPreviews Layout { get; }

    /// <summary>
    /// Gets the JPEG of tiles.
    /// </summary>
    public BitmapSource Image { get; }

    /// <summary>
    /// Makes a sheet of a JPEG's bytes, on any thread: the picture is decoded at once and frozen.
    /// </summary>
    /// <param name="layout">Where each tile is.</param>
    /// <param name="jpeg">The JPEG.</param>
    /// <returns>The sheet, or <see langword="null"/> when the bytes are not a picture.</returns>
    public static RecordingPreviewTiles? FromBytes(
        RecordingPreviews layout,
        byte[] jpeg)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(jpeg);

        try
        {
            using var stream = new MemoryStream(jpeg);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return new RecordingPreviewTiles(layout, image);
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a tile, cut out of the JPEG.
    /// </summary>
    /// <param name="tile">The tile's number.</param>
    /// <returns>The tile, frozen; <see langword="null"/> when it is not in the JPEG.</returns>
    public BitmapSource? Tile(int tile)
    {
        if (tiles.TryGetValue(tile, out var cut))
        {
            return cut;
        }

        var (x, y, width, height) = RecordingPreviewLayout.TileRect(Layout, tile);
        if (tile < 0 || x + width > Image.PixelWidth || y + height > Image.PixelHeight)
        {
            return null;
        }

        cut = new CroppedBitmap(Image, new Int32Rect(x, y, width, height));
        cut.Freeze();
        tiles[tile] = cut;
        return cut;
    }
}