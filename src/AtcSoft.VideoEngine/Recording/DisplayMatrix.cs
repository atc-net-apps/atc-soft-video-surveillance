// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
namespace AtcSoft.VideoEngine.Recording;

/// <summary>
/// Reads and writes the display-matrix side data that tells a player how to
/// rotate a stream at playback. This is the only rotation flag players
/// honour: a "rotate" metadata tag is dropped by the MP4 muxer and written
/// as plain text by the Matroska muxer.
/// </summary>
internal static unsafe class DisplayMatrix
{
    private const int MatrixSizeInBytes = 9 * sizeof(int);

    /// <summary>
    /// Flags the stream to be shown rotated clockwise by the given angle.
    /// Does nothing for an upright stream.
    /// </summary>
    /// <param name="codecpar">The output stream's codec parameters.</param>
    /// <param name="clockwiseDegrees">The clockwise rotation: 0, 90, 180 or 270.</param>
    public static void Write(
        AVCodecParameters* codecpar,
        int clockwiseDegrees)
    {
        var degrees = Normalize(clockwiseDegrees);
        if (codecpar is null || degrees == 0)
        {
            return;
        }

        var sideData = av_packet_side_data_new(
            &codecpar->coded_side_data,
            &codecpar->nb_coded_side_data,
            AVPacketSideDataType.Displaymatrix,
            MatrixSizeInBytes,
            0);

        if (sideData is null)
        {
            return;
        }

        // av_display_rotation_get reports counter-clockwise degrees and is
        // the negation of what av_display_rotation_set is given, so a
        // clockwise angle is passed as-is.
        av_display_rotation_set((int*)sideData->data, degrees);
    }

    /// <summary>
    /// Reads how far the stream is flagged to be rotated clockwise.
    /// </summary>
    /// <param name="codecpar">The stream's codec parameters.</param>
    /// <returns>0, 90, 180 or 270; 0 when the stream carries no display matrix.</returns>
    public static int ReadClockwiseDegrees(AVCodecParameters* codecpar)
    {
        if (codecpar is null)
        {
            return 0;
        }

        var sideData = av_packet_side_data_get(
            codecpar->coded_side_data,
            codecpar->nb_coded_side_data,
            AVPacketSideDataType.Displaymatrix);

        if (sideData is null || sideData->data is null || (int)sideData->size < MatrixSizeInBytes)
        {
            return 0;
        }

        var counterClockwise = av_display_rotation_get((int*)sideData->data);
        if (double.IsNaN(counterClockwise))
        {
            return 0;
        }

        return Normalize((int)Math.Round(-counterClockwise));
    }

    private static int Normalize(int degrees)
    {
        var normalized = ((degrees % 360) + 360) % 360;
        return normalized is 90 or 180 or 270 ? normalized : 0;
    }
}