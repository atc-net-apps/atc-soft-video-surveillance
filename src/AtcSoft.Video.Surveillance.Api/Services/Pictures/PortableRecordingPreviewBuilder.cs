namespace AtcSoft.Video.Surveillance.Api.Services.Pictures;

/// <summary>
/// Makes the preview pictures of a recording where GDI+ is not there (Linux, macOS, the Docker
/// image): the same sheet as the Windows builder, one pass through the file with a small picture at
/// each tile's moment (<see cref="RecordingPreviewLayout.Plan"/>), written as one JPEG beside the
/// video with its index (<see cref="RecordingPreviewFile"/>), with FFmpeg (<see cref="PictureCodec"/>).
/// </summary>
public sealed class PortableRecordingPreviewBuilder : IRecordingPreviewBuilder
{
    /// <summary>
    /// The JPEG quality of the previews, 0 to 100, as on Windows.
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

        var sheet = new byte[sheetWidth * sheetHeight * 4];
        using (var reader = PictureFileReader.Open(
            videoPath,
            previews.TileWidth,
            previews.TileHeight,
            (VideoRotation)MediaProbe.GetRotation(videoPath),
            DecoderThreads))
        {
            Fill(sheet, sheetWidth, previews, moments, reader, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var jpeg = PictureCodec.EncodeJpeg(sheet, sheetWidth, sheetHeight, JpegQuality)
            ?? throw new IOException($"The previews of {videoPath} could not be encoded.");
        RecordingPreviewFile.Write(videoPath, previews, path => File.WriteAllBytes(path, jpeg));
        return previews;
    }

    // The moments come in order, so the file is read once, forward. A moment past the last picture
    // (the file is shorter than it says) gets the last one.
    private static void Fill(
        byte[] sheet,
        int sheetWidth,
        RecordingPreviews previews,
        IReadOnlyList<(double Seconds, int Tile)> moments,
        PictureFileReader reader,
        CancellationToken cancellationToken)
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
                Copy(picture, sheet, sheetWidth, RecordingPreviewLayout.TileRect(previews, tile));
            }
        }
    }

    private static void Copy(
        PictureFrame picture,
        byte[] sheet,
        int sheetWidth,
        (int X, int Y, int Width, int Height) tile)
    {
        var width = Math.Min(picture.Width, tile.Width);
        var height = Math.Min(picture.Height, tile.Height);
        for (var row = 0; row < height; row++)
        {
            Buffer.BlockCopy(picture.Pixels, row * picture.Width * 4, sheet, (((tile.Y + row) * sheetWidth) + tile.X) * 4, width * 4);
        }
    }
}