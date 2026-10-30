namespace AtcSoft.VideoEngine.Windows.Previews;

using ActivityDownsampler = AtcSoft.VideoSurveillance.Helpers.Activity.ActivityDownsampler;
using ActivityLogReader = AtcSoft.VideoSurveillance.Helpers.Activity.ActivityLogReader;
using Drawing = System.Drawing;
using DrawingImaging = System.Drawing.Imaging;
using PictureFileReader = AtcSoft.VideoEngine.Capture.PictureFileReader;
using PictureFrame = AtcSoft.VideoEngine.Capture.PictureFrame;
using RecordingPathHelper = AtcSoft.VideoSurveillance.Helpers.RecordingPathHelper;
using RecordingPreviewFile = AtcSoft.VideoSurveillance.Helpers.Activity.RecordingPreviewFile;
using RecordingPreviewLayout = AtcSoft.VideoSurveillance.Helpers.Activity.RecordingPreviewLayout;

/// <summary>
/// Makes the preview pictures of a recording file: one pass through the file, a small picture
/// at each tile's moment (<see cref="RecordingPreviewLayout.Plan"/>: the grid, and the second
/// most moved in each event of its activity index), turned the way the picture is displayed,
/// drawn into one JPEG beside the video with its index (<see cref="RecordingPreviewFile"/>).
/// Shared by CameraWall and the server; it draws with GDI+, so it is Windows-only.
/// </summary>
public sealed class RecordingPreviewBuilder : IRecordingPreviewBuilder
{
    /// <summary>
    /// The JPEG quality of the previews, 0 to 100.
    /// </summary>
    public const int JpegQuality = 75;

    /// <summary>
    /// Gets or sets how many threads decode: few, so the work does not crowd out live cameras.
    /// </summary>
    public int DecoderThreads { get; set; } = 2;

    /// <inheritdoc />
    public RecordingPreviews? Build(
        string videoPath,
        CancellationToken cancellationToken = default)
        => Build(videoPath, DecoderThreads, cancellationToken);

    /// <summary>
    /// Makes the previews of a recording and writes them beside it, in place of any earlier ones.
    /// </summary>
    /// <param name="videoPath">The recording; finished, not one still being written.</param>
    /// <param name="decoderThreads">How many threads decode; an app that shows live cameras passes few.</param>
    /// <param name="cancellationToken">Stops the work; nothing is written then.</param>
    /// <returns>The layout written, or <see langword="null"/> for a file without a picture.</returns>
    /// <exception cref="AtcSoft.VideoEngine.FFmpeg.FFmpegException">The file cannot be read.</exception>
    /// <exception cref="IOException">The previews cannot be written.</exception>
    public static RecordingPreviews? Build(
        string videoPath,
        int decoderThreads,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        var (displayWidth, displayHeight) = MediaProbe.GetDisplaySize(videoPath);
        if (displayWidth <= 0 || displayHeight <= 0)
        {
            return null;
        }

        var duration = MediaProbe.GetDuration(videoPath).TotalSeconds;
        var peaks = duration > 0 && ActivityLogReader.Read(RecordingPathHelper.BuildActivityPath(videoPath)) is { } log
            ? RecordingPreviewLayout.PeakSeconds(ActivityDownsampler.ByMax(log, duration, Math.Max((int)Math.Ceiling(duration), 1)), duration)
            : [];
        var (previews, moments) = RecordingPreviewLayout.Plan(duration, displayWidth, displayHeight, peaks);
        var (sheetWidth, sheetHeight) = RecordingPreviewLayout.SheetSize(previews);

        using var sheet = new Drawing.Bitmap(sheetWidth, sheetHeight, DrawingImaging.PixelFormat.Format32bppRgb);
        using (var reader = PictureFileReader.Open(
            videoPath,
            previews.TileWidth,
            previews.TileHeight,
            (VideoRotation)MediaProbe.GetRotation(videoPath),
            decoderThreads))
        {
            Fill(sheet, previews, moments, reader, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        RecordingPreviewFile.Write(videoPath, previews, path => SaveJpeg(sheet, path));
        return previews;
    }

    // The moments come in order, so the file is read once, forward. A moment past the last
    // picture (the file is shorter than it says) gets the last one.
    private static void Fill(
        Drawing.Bitmap sheet,
        RecordingPreviews previews,
        IReadOnlyList<(double Seconds, int Tile)> moments,
        PictureFileReader reader,
        CancellationToken cancellationToken)
    {
        var bits = sheet.LockBits(
            new Drawing.Rectangle(0, 0, sheet.Width, sheet.Height),
            DrawingImaging.ImageLockMode.WriteOnly,
            DrawingImaging.PixelFormat.Format32bppRgb);
        try
        {
            PictureFrame? picture = null;
            var ended = false;
            foreach (var (seconds, tile) in moments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var moment = TimeSpan.FromSeconds(seconds);
                if (!ended && (picture is null || picture.Position < moment))
                {
                    var next = reader.ReadNext(moment);
                    ended = next is null;
                    picture = next ?? picture;
                }

                if (picture is not null)
                {
                    Copy(picture, bits, RecordingPreviewLayout.TileRect(previews, tile));
                }
            }
        }
        finally
        {
            sheet.UnlockBits(bits);
        }
    }

    private static void Copy(
        PictureFrame picture,
        DrawingImaging.BitmapData bits,
        (int X, int Y, int Width, int Height) tile)
    {
        var width = Math.Min(picture.Width, tile.Width);
        var height = Math.Min(picture.Height, tile.Height);
        for (var row = 0; row < height; row++)
        {
            var target = bits.Scan0 + ((tile.Y + row) * bits.Stride) + (tile.X * 4);
            Marshal.Copy(picture.Pixels, row * picture.Width * 4, target, width * 4);
        }
    }

    private static void SaveJpeg(
        Drawing.Bitmap sheet,
        string path)
    {
        var codec = DrawingImaging.ImageCodecInfo.GetImageEncoders()
            .First(c => c.FormatID == DrawingImaging.ImageFormat.Jpeg.Guid);
        using var parameters = new DrawingImaging.EncoderParameters(1);
        parameters.Param[0] = new DrawingImaging.EncoderParameter(DrawingImaging.Encoder.Quality, (long)JpegQuality);
        sheet.Save(path, codec, parameters);
    }
}